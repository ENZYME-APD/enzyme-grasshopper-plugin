using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Diagnostics;
using Grasshopper.Kernel;
using Enzyme; // for IconLoader

namespace Enzyme.Components
{
    public class HeightMapAnalysisMesh : GH_Component
    {
        public HeightMapAnalysisMesh()
            : base("Height Map Analysis Mesh", "HeightMap",
                "Analyzes a mesh topography and provides a colored mesh displaying height distribution",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                Bitmap icon = IconLoader.Load("height_terrain_icon.png");
                if (icon == null)
                {
                    this.Message = "Icon missing";
                }
                return icon;
            }
        }

        public override Guid ComponentGuid => new Guid("B2C8F3D5-A7E1-4D9B-9F6C-E5D8A3B7C2F1");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 2, true, 210, 0);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -15);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "Input mesh topography", GH_ParamAccess.item);
            pManager.AddColourParameter("Color Gradient", "Color Gradient", "Colors for height gradient (minimum 2 colors)", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Flip Colors", "Flip Colors", "Flip the color gradient direction", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Height Mesh", "Height Mesh", "Colored mesh showing height distribution", GH_ParamAccess.item);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "Legend of colors and their corresponding heights", GH_ParamAccess.item);
                    pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Input variables
            Rhino.Geometry.Mesh mesh = null;
            var colors = new System.Collections.Generic.List<Color>();
            bool flipColors = true;

            // Get input data
            if (!DA.GetData(0, ref mesh)) return;
            if (!DA.GetDataList(1, colors)) return;
            if (!DA.GetData(2, ref flipColors)) return;

            // Validate input
            if (mesh == null || !mesh.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid mesh input");
                return;
                            
            }

            if (colors.Count < 2)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least two colors are required for the gradient");
                return;
            }

            // Create a copy of the mesh to work with
            Rhino.Geometry.Mesh analysisMesh = mesh.DuplicateMesh();

            // Find the height range of the mesh
            double minZ = double.MaxValue;
            double maxZ = double.MinValue;

            foreach (Rhino.Geometry.Point3f vertex in analysisMesh.Vertices)
            {
                minZ = Math.Min(minZ, vertex.Z);
                maxZ = Math.Max(maxZ, vertex.Z);
            }

            // Calculate the height range
            double heightRange = maxZ - minZ;

            if (heightRange <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh has no height variation");
                heightRange = 1.0; // Prevent division by zero
            }

            // Create the colored mesh based on height
            Rhino.Geometry.Mesh coloredMesh = ColorMeshByHeight(analysisMesh, colors, minZ, maxZ, flipColors);

            // Create legend data
            var legendData = CreateHeightLegendData(colors, minZ, maxZ, flipColors);

            stopwatch.Stop();
            double executionTime = stopwatch.Elapsed.TotalSeconds;

            // Set output data
            DA.SetData(0, coloredMesh);
            DA.SetData(1, legendData);

            Message = $"Ht. variation: {minZ:F2} to {maxZ:F2} ({heightRange:F2})";
            Message += $"\nNo. of Colors: {colors.Count}";
            Message += $"\nTime: {executionTime:F3}s";
                    DA.SetData(2, "HEIGHT MAP ANALYSIS\n" + "\n" + "HOW IT WORKS:\n" + "Sorts all mesh vertices by their Z-elevation and maps them to a customizable color gradient from the lowest to the highest point.\n\n" + "INTERPRETATION & IMPORTANCE:\n" + "Provides a quick, intuitive read of the site's macro-topography. Helps in zoning the site (e.g., placing critical infrastructure above the flood plain or historical high-water marks).");
        }

        private Rhino.Geometry.Mesh ColorMeshByHeight(Rhino.Geometry.Mesh mesh, System.Collections.Generic.List<Color> colors, double minZ, double maxZ, bool flipColors)
        {
            // Create a copy of the mesh
            Rhino.Geometry.Mesh coloredMesh = mesh.DuplicateMesh();

            // Initialize vertex colors
            coloredMesh.VertexColors.CreateMonotoneMesh(Color.White);

            // Get the number of colors in the gradient
            int colorCount = colors.Count;

            // If flip colors is true, reverse the color array
            if (flipColors)
            {
                colors.Reverse();
            }

            // Calculate the height range
            double heightRange = maxZ - minZ;

            // Color each vertex based on its height
            for (int i = 0; i < coloredMesh.Vertices.Count; i++)
            {
                // Get the vertex height
                double height = coloredMesh.Vertices[i].Z;

                // Calculate the normalized height (0 to 1)
                double normalizedHeight = (height - minZ) / heightRange;

                // Determine which segment of the gradient this height falls into
                double segmentSize = 1.0 / (colorCount - 1);
                int segmentIndex = Math.Min((int)(normalizedHeight / segmentSize), colorCount - 2);

                // Calculate the position within the segment (0 to 1)
                double segmentPosition = (normalizedHeight - segmentIndex * segmentSize) / segmentSize;

                // Interpolate between the two colors in this segment
                Color startColor = colors[segmentIndex];
                Color endColor = colors[segmentIndex + 1];
                Color vertexColor = InterpolateColor(startColor, endColor, segmentPosition);

                // Set the vertex color
                coloredMesh.VertexColors[i] = vertexColor;
            }

            return coloredMesh;
        }

        private Color InterpolateColor(Color color1, Color color2, double t)
        {
            int r = (int)(color1.R * (1 - t) + color2.R * t);
            int g = (int)(color1.G * (1 - t) + color2.G * t);
            int b = (int)(color1.B * (1 - t) + color2.B * t);
            return Color.FromArgb(r, g, b);
        }

        private string CreateHeightLegendData(System.Collections.Generic.List<Color> colors, double minZ, double maxZ, bool flipColors)
        {
            var jColors = new JArray();
            foreach (var c in colors) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
            var legendObj = new JObject
            {
                ["Type"] = "Blocks",
                ["Title"] = "Height Map Analysis",
                ["Colors"] = jColors,
                ["Labels"] = new JArray($"{minZ:F1}m", $"{maxZ:F1}m"),
                ["SubLabels"] = new JArray($"Relief: {(maxZ - minZ):F1}m")
            };
            return legendObj.ToString();
        }
    }
}
