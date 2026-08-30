using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class DataVisualizer : GH_Component
    {
        public DataVisualizer()
          : base("Data Visualizer", "DataVis",
              "Visualizes points and data values as a fast gradient mesh (Bars, Dots, or Spheres).",
              "Enzyme", "LEAP")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "List of 3D Points", GH_ParamAccess.list);
            pManager.AddNumberParameter("Values", "V", "List of data values matching the points", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "Gradient color palette", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Domain", "D", "Target domain for geometry size (Radius/Height)", GH_ParamAccess.item, new Interval(0.5, 5.0));
            pManager.AddIntegerParameter("Type", "T", "Visual Type (0: Bar, 1: Flat Dot, 2: Sphere)", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Bar Thickness", "W", "Thickness for Bar Chart (Type 0 only)", GH_ParamAccess.list);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        private bool hasSources = false;
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                var defaultColors = new Color[] {
                    Color.FromArgb(0, 50, 150),
                    Color.FromArgb(0, 180, 200),
                    Color.FromArgb(150, 220, 100),
                    Color.FromArgb(255, 200, 50),
                    Color.FromArgb(255, 50, 0)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 2, defaultColors, 150, 30);
                
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 3, "0.5 To 5.0", 300, -180, 100, 30);
                
                string[] keys = new string[] { "Bar Chart", "Flat Dot", "Sphere" };
                string[] values = new string[] { "0", "1", "2" };
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 4, keys, values, 200, -220);
                
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.1, 5.0, 0.5, 330, -140);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Visualization", "M", "A single joined mesh representing the data (for fast viewport rendering)", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> pts = new List<Point3d>();
            if (!DA.GetDataList(0, pts)) return;

            List<double> vals = new List<double>();
            if (!DA.GetDataList(1, vals)) return;

            List<Color> colors = new List<Color>();
            DA.GetDataList(2, colors);

            Interval targetDomain = new Interval(0.5, 5.0);
            DA.GetData(3, ref targetDomain);

            int type = 2;
            DA.GetData(4, ref type);

            List<double> thicknesses = new List<double>();
            if (!DA.GetDataList(5, thicknesses) || thicknesses.Count == 0)
            {
                thicknesses.Add(0.5);
            }

            if (pts.Count == 0 || vals.Count == 0) return;
            if (colors.Count == 0) colors.Add(Color.White);

            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            foreach (double v in vals)
            {
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }

            if (Math.Abs(maxVal - minVal) < 1e-9) maxVal = minVal + 1.0;

            Mesh masterMesh = new Mesh();

            for (int i = 0; i < pts.Count; i++)
            {
                if (i >= vals.Count) break;

                Point3d p = pts[i];
                double v = vals[i];

                double normalized = (v - minVal) / (maxVal - minVal);
                if (normalized < 0.0) normalized = 0.0;
                if (normalized > 1.0) normalized = 1.0;

                double mappedSize = targetDomain.T0 + normalized * (targetDomain.T1 - targetDomain.T0);
                Color c = GetInterpolatedColor(normalized, colors);

                double currentThickness = thicknesses[i % thicknesses.Count];
                Mesh m = CreateGeometry(type, p, mappedSize, currentThickness);
                
                // Assign vertex colors
                for (int j = 0; j < m.Vertices.Count; j++)
                {
                    m.VertexColors.Add(c);
                }

                masterMesh.Append(m);
            }

            DA.SetData(0, masterMesh);
        }

        private Color GetInterpolatedColor(double t, List<Color> palette)
        {
            if (palette.Count == 1) return palette[0];
            
            double scaledT = t * (palette.Count - 1);
            int index = (int)scaledT;
            
            if (index >= palette.Count - 1) return palette[palette.Count - 1];
            if (index < 0) return palette[0];
            
            double remainder = scaledT - index;
            
            Color c1 = palette[index];
            Color c2 = palette[index + 1];
            
            int r = (int)(c1.R + (c2.R - c1.R) * remainder);
            int g = (int)(c1.G + (c2.G - c1.G) * remainder);
            int b = (int)(c1.B + (c2.B - c1.B) * remainder);
            
            return Color.FromArgb(r, g, b);
        }

        private Mesh CreateGeometry(int type, Point3d center, double size, double thickness)
        {
            Mesh m = new Mesh();
            
            if (type == 0) // Bar Chart (Hex prism)
            {
                double r = thickness;
                double h = size;
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3.0;
                    double dx = Math.Cos(angle) * r;
                    double dy = Math.Sin(angle) * r;
                    m.Vertices.Add(center.X + dx, center.Y + dy, center.Z);
                    m.Vertices.Add(center.X + dx, center.Y + dy, center.Z + h);
                }
                m.Vertices.Add(center.X, center.Y, center.Z);
                m.Vertices.Add(center.X, center.Y, center.Z + h);

                int bc = 12;
                int tc = 13;
                for (int i = 0; i < 6; i++)
                {
                    int next = (i + 1) % 6;
                    m.Faces.AddFace(i * 2, next * 2, next * 2 + 1, i * 2 + 1);
                    m.Faces.AddFace(bc, next * 2, i * 2);
                    m.Faces.AddFace(tc, i * 2 + 1, next * 2 + 1);
                }
            }
            else if (type == 1) // Flat Dot (Hexagon)
            {
                double r = size;
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3.0;
                    double dx = Math.Cos(angle) * r;
                    double dy = Math.Sin(angle) * r;
                    m.Vertices.Add(center.X + dx, center.Y + dy, center.Z);
                }
                m.Vertices.Add(center);
                for (int i = 0; i < 6; i++)
                {
                    m.Faces.AddFace(6, i, (i + 1) % 6);
                }
            }
            else // Sphere (Icosahedron)
            {
                double t = (1.0 + Math.Sqrt(5.0)) / 2.0;
                double length = Math.Sqrt(1 + t * t);
                double f = size / length;
                double tf = t * f;
                double rf = 1.0 * f;

                m.Vertices.Add(new Point3d(-rf + center.X, tf + center.Y, center.Z));
                m.Vertices.Add(new Point3d(rf + center.X, tf + center.Y, center.Z));
                m.Vertices.Add(new Point3d(-rf + center.X, -tf + center.Y, center.Z));
                m.Vertices.Add(new Point3d(rf + center.X, -tf + center.Y, center.Z));

                m.Vertices.Add(new Point3d(center.X, -rf + center.Y, tf + center.Z));
                m.Vertices.Add(new Point3d(center.X, rf + center.Y, tf + center.Z));
                m.Vertices.Add(new Point3d(center.X, -rf + center.Y, -tf + center.Z));
                m.Vertices.Add(new Point3d(center.X, rf + center.Y, -tf + center.Z));

                m.Vertices.Add(new Point3d(tf + center.X, center.Y, -rf + center.Z));
                m.Vertices.Add(new Point3d(tf + center.X, center.Y, rf + center.Z));
                m.Vertices.Add(new Point3d(-tf + center.X, center.Y, -rf + center.Z));
                m.Vertices.Add(new Point3d(-tf + center.X, center.Y, rf + center.Z));

                m.Faces.AddFace(0, 11, 5);
                m.Faces.AddFace(0, 5, 1);
                m.Faces.AddFace(0, 1, 7);
                m.Faces.AddFace(0, 7, 10);
                m.Faces.AddFace(0, 10, 11);

                m.Faces.AddFace(1, 5, 9);
                m.Faces.AddFace(5, 11, 4);
                m.Faces.AddFace(11, 10, 2);
                m.Faces.AddFace(10, 7, 6);
                m.Faces.AddFace(7, 1, 8);

                m.Faces.AddFace(3, 9, 4);
                m.Faces.AddFace(3, 4, 2);
                m.Faces.AddFace(3, 2, 6);
                m.Faces.AddFace(3, 6, 8);
                m.Faces.AddFace(3, 8, 9);

                m.Faces.AddFace(4, 9, 5);
                m.Faces.AddFace(2, 4, 11);
                m.Faces.AddFace(6, 2, 10);
                m.Faces.AddFace(8, 6, 7);
                m.Faces.AddFace(9, 8, 1);
            }

            return m;
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("11223344-5566-7788-99AA-BBCCDDEEFF00"); }
        }
    }
}
