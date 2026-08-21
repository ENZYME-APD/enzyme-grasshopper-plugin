using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Diagnostics;
using Grasshopper.Kernel;
using Enzyme; // for IconLoader

namespace Enzyme.Components
{
    public class SlopeAnalysisMesh : GH_Component
    {
        public SlopeAnalysisMesh()
            : base("Slope Analysis Mesh", "SlopeAnalysis",
                "Analyzes a mesh topography and provides a colored mesh displaying the areas with slopes over the threshold",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                Bitmap icon = IconLoader.Load("slope_terrain_icon.png");
                if (icon == null)
                {
                    this.Message = "Icon missing";
                }
                return icon;
            }
        }

        public override Guid ComponentGuid => new Guid("7A8E8AD1-F58A-4E36-BF85-A7D97A506D4A");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 1, System.Drawing.Color.Green, 210, -60);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 2, System.Drawing.Color.Red, 210, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 60, 30.0, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, false, 210, 60);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -15);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "Input mesh topography", GH_ParamAccess.item);
            pManager.AddColourParameter("Start Color", "Start Color", "Color for areas below threshold", GH_ParamAccess.item, Color.Green);
            pManager.AddColourParameter("End Color", "End Color", "Color for areas above threshold", GH_ParamAccess.item, Color.Red);
            pManager.AddNumberParameter("Threshold", "Threshold", "Slope threshold in degrees", GH_ParamAccess.item, 30.0);
            pManager.AddBooleanParameter("Binary Mode", "Binary Mode", "Use binary coloring instead of gradient", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Slope Mesh", "Slope Mesh", "Colored mesh showing slope analysis", GH_ParamAccess.item);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "Legend of colors and their corresponding percentages", GH_ParamAccess.item);
            pManager.AddNumberParameter("Percent Over Threshold", "Percent Over Threshold", "Percentage of faces over the slope threshold", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Input variables
            Rhino.Geometry.Mesh mesh = null;
            Color startColor = Color.Green;
            Color endColor = Color.Red;
            double threshold = 30.0;
            bool binaryMode = false;

            // Get input data
            if (!DA.GetData(0, ref mesh)) return;
            if (!DA.GetData(1, ref startColor)) return;
            if (!DA.GetData(2, ref endColor)) return;
            if (!DA.GetData(3, ref threshold)) return;
            if (!DA.GetData(4, ref binaryMode)) return;

            // Validate input
            if (mesh == null || !mesh.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid mesh input");
                return;
            }

            // Create a copy of the mesh to work with
            Rhino.Geometry.Mesh analysisMesh = mesh.DuplicateMesh();

            // Ensure the mesh has face normals
            analysisMesh.FaceNormals.ComputeFaceNormals();

            // Calculate slopes and color the mesh
            Rhino.Geometry.Mesh coloredMesh = AnalyzeMeshSlopes(analysisMesh, startColor, endColor, threshold, binaryMode, out double percentOverThreshold);

            // Create legend data
            var legendData = CreateLegendData(startColor, endColor, threshold, percentOverThreshold);

            stopwatch.Stop();
            double executionTime = stopwatch.Elapsed.TotalSeconds;

            // Set output data
            DA.SetData(0, coloredMesh);
            DA.SetData(1, legendData);
            DA.SetData(2, percentOverThreshold);

            string mode = binaryMode ? "Binary" : "Gradient";
            Message = $"Mode: {mode}";
            Message += $"\n{Math.Round(percentOverThreshold, 2)}% over {Math.Round(threshold, 2)} threshold";
            Message += $"\nTime: {executionTime:F3}s";
        }

        private Rhino.Geometry.Mesh AnalyzeMeshSlopes(Rhino.Geometry.Mesh mesh, Color startColor, Color endColor, double threshold, bool binaryMode, out double percentOverThreshold)
        {
            // Create a copy of the mesh
            Rhino.Geometry.Mesh coloredMesh = mesh.DuplicateMesh();

            // Initialize vertex colors
            coloredMesh.VertexColors.CreateMonotoneMesh(startColor);

            // Calculate the slope of each face and color accordingly
            int facesOverThreshold = 0;
            int totalFaces = coloredMesh.Faces.Count;

            for (int i = 0; i < totalFaces; i++)
            {
                // Get the face normal
                Rhino.Geometry.Vector3f normal = coloredMesh.FaceNormals[i];

                // Calculate the angle between the normal and the Z-axis (in degrees)
                double angle = Math.Acos(Math.Abs(normal.Z) / normal.Length) * (180.0 / Math.PI);

                // Determine if the face is over the threshold
                bool isOverThreshold = angle > threshold;

                if (isOverThreshold)
                {
                    facesOverThreshold++;
                }

                // Set the color based on the mode
                Color faceColor;
                if (binaryMode)
                {
                    // Binary mode: either start or end color
                    faceColor = isOverThreshold ? endColor : startColor;
                }
                else
                {
                    // Gradient mode: interpolate between start and end color
                    double t = Math.Min(angle / threshold, 1.0);
                    faceColor = InterpolateColor(startColor, endColor, t);
                }

                // Apply the color to the face vertices
                Rhino.Geometry.MeshFace face = coloredMesh.Faces[i];
                coloredMesh.VertexColors[face.A] = faceColor;
                coloredMesh.VertexColors[face.B] = faceColor;
                coloredMesh.VertexColors[face.C] = faceColor;
                if (face.IsQuad)
                {
                    coloredMesh.VertexColors[face.D] = faceColor;
                }
            }

            // Calculate percentage of faces over threshold
            percentOverThreshold = (double)facesOverThreshold / totalFaces * 100.0;

            return coloredMesh;
        }

        private Color InterpolateColor(Color color1, Color color2, double t)
        {
            int r = (int)(color1.R * (1 - t) + color2.R * t);
            int g = (int)(color1.G * (1 - t) + color2.G * t);
            int b = (int)(color1.B * (1 - t) + color2.B * t);
            return Color.FromArgb(r, g, b);
        }

        private string CreateLegendData(Color startColor, Color endColor, double threshold, double percentOverThreshold)
        {
            var legendObj = new JObject
            {
                ["Type"] = "Gradient",
                ["Title"] = $"Slope Analysis (Threshold: {threshold:F1}°)",
                ["Colors"] = new JArray(
                    new JObject { ["R"] = startColor.R, ["G"] = startColor.G, ["B"] = startColor.B },
                    new JObject { ["R"] = endColor.R, ["G"] = endColor.G, ["B"] = endColor.B }
                ),
                ["Labels"] = new JArray("0°", $"{threshold:F1}°+"),
                ["SubLabels"] = new JArray($"{percentOverThreshold:F1}% over threshold")
            };
            return legendObj.ToString();
        }
    }
}
