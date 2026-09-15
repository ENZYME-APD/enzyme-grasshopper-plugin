using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class TerrainColorizer : GH_Component
    {
        public TerrainColorizer()
          : base("Terrain Colorizer", "Terrain Color",
              "Colors a terrain mesh by elevation and generates contours.",
              "Enzyme", "Site Analysis")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "T", "Input terrain mesh", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "C", "List of gradient colors (from low to high). Leaves empty for default palette.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Use Slope Color", "USC", "Toggle steep slope coloring", GH_ParamAccess.item, false);
            pManager.AddColourParameter("Slope Color", "SC", "Color applied to sheer cliffs/slopes", GH_ParamAccess.item, Color.DarkGray);
            pManager.AddNumberParameter("Slope Angle", "SA", "Angle in degrees where slope color starts", GH_ParamAccess.item, 30.0);
            pManager.AddNumberParameter("Contour Step", "CS", "Interval for normal contour lines", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Main Step", "MS", "Interval for main contour lines", GH_ParamAccess.item, 5.0);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Colored Terrain", "T", "Terrain mesh with vertex colors", GH_ParamAccess.item);
            pManager.AddCurveParameter("Contours", "Ct", "Normal contours", GH_ParamAccess.list);
            pManager.AddCurveParameter("Main Contours", "MC", "Main contours", GH_ParamAccess.list);
        }

        private void AutoWireDefaults(GH_Document document)
        {
            Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 2, false, 210, -20);
            Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 3, System.Drawing.Color.DarkGray, 210, 20);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 90.0, 30.0, 330, 60);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 5, 0.1, 10.0, 1.0, 330, 100);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 6, 1.0, 50.0, 5.0, 330, 140);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                AutoWireDefaults(document);
            }
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Auto-Fill Defaults", (s, e) =>
            {
                if (OnPingDocument() != null)
                {
                    AutoWireDefaults(OnPingDocument());
                }
            });
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData(0, ref mesh) || mesh == null || !mesh.IsValid) return;

            List<Color> colors = new List<Color>();
            DA.GetDataList(1, colors);

            bool useSlope = false;
            DA.GetData(2, ref useSlope);

            Color slopeColor = Color.DarkGray;
            DA.GetData(3, ref slopeColor);

            double slopeAngle = 30.0;
            DA.GetData(4, ref slopeAngle);

            double cStep = 1.0;
            DA.GetData(5, ref cStep);

            double mStep = 5.0;
            DA.GetData(6, ref mStep);

            if (colors == null || colors.Count == 0)
            {
                colors = new List<Color>
                {
                    Color.FromArgb(34, 139, 34),    // Forest Green
                    Color.FromArgb(154, 205, 50),   // Yellow Green
                    Color.FromArgb(218, 165, 32),   // Goldenrod
                    Color.FromArgb(139, 69, 19),    // Saddle Brown
                    Color.White                     // Snow
                };
            }

            Mesh outMesh = mesh.DuplicateMesh();
            BoundingBox bbox = outMesh.GetBoundingBox(true);
            double minZ = bbox.Min.Z;
            double maxZ = bbox.Max.Z;
            double hRange = Math.Max(0.001, maxZ - minZ);

            double slopeRad = Math.Max(0.0, Math.Min(slopeAngle, 90.0)) * (Math.PI / 180.0);
            double thresholdZ = Math.Cos(slopeRad);
            double falloffRange = 0.20;

            outMesh.FaceNormals.ComputeFaceNormals();
            outMesh.Normals.ComputeNormals();
            outMesh.VertexColors.Clear();

            for (int i = 0; i < outMesh.Vertices.Count; i++)
            {
                Point3d pt = outMesh.Vertices[i];
                double tHeight = (pt.Z - minZ) / hRange;
                Color baseColor = GetGradientColor(tHeight, colors);

                if (useSlope && outMesh.Normals.Count > i)
                {
                    Vector3f normal = outMesh.Normals[i];
                    float nz = Math.Abs(normal.Z);
                    if (nz < thresholdZ)
                    {
                        double blendFactor = Math.Min((thresholdZ - nz) / falloffRange, 1.0);
                        Color finalColor = BlendColors(baseColor, slopeColor, blendFactor);
                        outMesh.VertexColors.Add(finalColor);
                    }
                    else
                    {
                        outMesh.VertexColors.Add(baseColor);
                    }
                }
                else
                {
                    outMesh.VertexColors.Add(baseColor);
                }
            }

            List<Curve> normContours = new List<Curve>();
            List<Curve> mainContours = new List<Curve>();

            if (cStep > 0.0)
            {
                double startZ = Math.Floor(minZ / cStep) * cStep;
                Point3d p0 = new Point3d(0, 0, startZ);
                Point3d p1 = new Point3d(0, 0, maxZ + cStep);
                Curve[] contours = Mesh.CreateContourCurves(outMesh, p0, p1, cStep, 0.01);

                if (contours != null)
                {
                    foreach (var crv in contours)
                    {
                        double ptZ = crv.PointAtStart.Z;
                        double rem = Math.Abs(ptZ % mStep);
                        if (rem < 0.001 || Math.Abs(rem - mStep) < 0.001)
                            mainContours.Add(crv);
                        else
                            normContours.Add(crv);
                    }
                }
            }

            DA.SetData(0, outMesh);
            DA.SetDataList(1, normContours);
            DA.SetDataList(2, mainContours);
        }

        private Color GetGradientColor(double t, List<Color> colors)
        {
            if (colors == null || colors.Count == 0) return Color.White;
            if (colors.Count == 1) return colors[0];

            t = Math.Max(0.0, Math.Min(1.0, t));
            double idx = t * (colors.Count - 1);
            int i = (int)Math.Floor(idx);
            double frac = idx - i;

            if (i >= colors.Count - 1) return colors.Last();

            Color c1 = colors[i];
            Color c2 = colors[i + 1];

            int r = (int)(c1.R + (c2.R - c1.R) * frac);
            int g = (int)(c1.G + (c2.G - c1.G) * frac);
            int b = (int)(c1.B + (c2.B - c1.B) * frac);

            return Color.FromArgb(r, g, b);
        }

        private Color BlendColors(Color c1, Color c2, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            int r = (int)(c1.R + (c2.R - c1.R) * t);
            int g = (int)(c1.G + (c2.G - c1.G) * t);
            int b = (int)(c1.B + (c2.B - c1.B) * t);
            return Color.FromArgb(r, g, b);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("731CD54F-9A69-4F6F-ACA2-F167A386E093"); }
        }
    }
}
