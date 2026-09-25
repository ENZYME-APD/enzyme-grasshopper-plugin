using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Newtonsoft.Json;

namespace Enzyme.Components
{
    public class FastNoiseAnalyzer : GH_Component
    {
        public FastNoiseAnalyzer()
          : base("Fast Noise Analyzer", "NoiseEnv",
              "A rapid pseudo-acoustic noise analysis component. Maps decibel (dB) decay over distance and hard occlusions.",
              "Enzyme", "Environmental")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null; // Let GH use default or add IconLoader later

        public override Guid ComponentGuid => new Guid("11112222-3333-4444-5555-666677778888");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Global execution toggle switch", GH_ParamAccess.item, false);
            pManager.AddMeshParameter("TerrainMesh", "Terrain", "The underlying site topography", GH_ParamAccess.item);
            pManager.AddMeshParameter("ContextBuildings", "Context", "Lightweight mesh context structures for sound occlusion", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddPointParameter("Emitters", "Emitters", "Noise source points (e.g., roads, machinery)", GH_ParamAccess.list);
            pManager.AddNumberParameter("EmitterPower", "Power(dB)", "Decibel level per emitter at 1m (default 85dB)", GH_ParamAccess.list, 85.0);
            pManager.AddPointParameter("AnalysisPoints", "PtsIn", "Specific discrete test locations (e.g. windows/facades) to test individually", GH_ParamAccess.list);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("AnalysisHeight", "Height", "Pedestrian offset from terrain (m)", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("GridSpacing", "Grid", "Resolution of pixel elements (m)", GH_ParamAccess.item, 5.0);
            pManager.AddColourParameter("CustomColors", "Colors", "Color spectrum override (Quiet -> Loud)", GH_ParamAccess.list);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("NoiseHeatmap", "Heatmap", "Flat, crisp pixel-tile matrix mapping noise", GH_ParamAccess.item);
            pManager.AddMeshParameter("PlainMesh", "PlainMesh", "Original base grid without colors", GH_ParamAccess.item);
            pManager.AddPointParameter("TagPoints", "TagPts", "Anchor coordinates for Text Tag", GH_ParamAccess.list);
            pManager.AddNumberParameter("NoiseValues", "dB", "Raw unformatted decibel values matching TagPoints", GH_ParamAccess.list);
            
            pManager.AddPointParameter("AnalysisPtsOut", "PtsOut", "Pass-through for AnalysisPoints input", GH_ParamAccess.list);
            pManager.AddNumberParameter("AnalysisPtsValues", "Pts_dB", "Raw dB values precisely at the AnalysisPoints", GH_ParamAccess.list);
            
            pManager.AddTextParameter("DashboardData", "Dashboard", "JSON legend data", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Disclaimer on methodology and pseudo-acoustic limitations", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            if (!DA.GetData(0, ref run)) return;

            Mesh terrain = null;
            if (!DA.GetData(1, ref terrain)) return;

            List<Mesh> context = new List<Mesh>();
            DA.GetDataList(2, context);

            List<Point3d> emitters = new List<Point3d>();
            if (!DA.GetDataList(3, emitters)) return;
            if (emitters.Count == 0) return;

            List<double> power = new List<double>();
            DA.GetDataList(4, power);
            if (power.Count == 0) power.Add(85.0);

            List<Point3d> testPointsIn = new List<Point3d>();
            DA.GetDataList(5, testPointsIn);

            double height = 1.5;
            DA.GetData(6, ref height);

            double spacing = 5.0;
            DA.GetData(7, ref spacing);

            List<Color> colors = new List<Color>();
            DA.GetDataList(8, colors);

            string disclaimer = "METHODOLOGY & INACCURACIES:\n" +
                                "- Propagation uses standard Free-Field Inverse-Square Law (-20*log10(r)).\n" +
                                "- Addition is strictly energetic (logarithmic summation).\n" +
                                "- Occlusion is binary raycasting: strict -15dB penalty if line of sight is obstructed.\n" +
                                "- NO complex bouncing, diffraction (Fresnel zones), reverberation, or material absorption is computed.\n" +
                                "- Best used for rapid early-stage urban blocking, not for final certified acoustic engineering.";
            DA.SetData(7, disclaimer);

            if (!run)
            {
                this.Message = "OFF";
                return;
            }

            this.Message = "Computing...";

            // Optimize BVH context
            List<Mesh> colliders = new List<Mesh>(context);
            if (terrain != null) colliders.Add(terrain);
            

            // 1. GENERATE PIXEL GRID
            BoundingBox bbox = terrain.GetBoundingBox(false);
            int nx = (int)Math.Ceiling((bbox.Max.X - bbox.Min.X) / spacing);
            int ny = (int)Math.Ceiling((bbox.Max.Y - bbox.Min.Y) / spacing);

            List<Point3d> gridCenters = new List<Point3d>();
            List<Point3d[]> quads = new List<Point3d[]>();

            double halfSpace = spacing * 0.5;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    double cx = bbox.Min.X + i * spacing + halfSpace;
                    double cy = bbox.Min.Y + j * spacing + halfSpace;

                    Ray3d downRay = new Ray3d(new Point3d(cx, cy, bbox.Max.Z + 100), Vector3d.ZAxis * -1);
                    double t = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, downRay);

                    if (t >= 0.0)
                    {
                        Point3d hit = downRay.PointAt(t);
                        Point3d center = new Point3d(hit.X, hit.Y, hit.Z + height);
                        gridCenters.Add(center);

                        Point3d[] quad = new Point3d[4];
                        quad[0] = new Point3d(cx - halfSpace, cy - halfSpace, center.Z);
                        quad[1] = new Point3d(cx + halfSpace, cy - halfSpace, center.Z);
                        quad[2] = new Point3d(cx + halfSpace, cy + halfSpace, center.Z);
                        quad[3] = new Point3d(cx - halfSpace, cy + halfSpace, center.Z);
                        quads.Add(quad);
                    }
                }
            }

            // ACOUSTIC FUNCTION
            Func<Point3d, double> CalculateDB = (pt) =>
            {
                double totalEnergy = 0.0;
                for (int k = 0; k < emitters.Count; k++)
                {
                    double d = pt.DistanceTo(emitters[k]);
                    if (d < 1.0) d = 1.0;
                    
                    double pwr = power[k % power.Count];
                    double rawDb = pwr - 20 * Math.Log10(d);

                    // Raycast
                    Ray3d toEmitter = new Ray3d(pt, emitters[k] - pt);
                    bool occluded = false;
                    foreach (var m in colliders)
                    {
                        double thit = Rhino.Geometry.Intersect.Intersection.MeshRay(m, toEmitter);
                        if (thit >= 0.0 && thit < d)
                        {
                            occluded = true;
                            break;
                        }
                    }

                    if (occluded) rawDb -= 15.0; // Hard penalty

                    if (rawDb > 0)
                        totalEnergy += Math.Pow(10, rawDb / 10.0);
                }
                return totalEnergy > 0 ? 10 * Math.Log10(totalEnergy) : 0;
            };

            // 2. PARALLEL COMPUTE MESH
            double[] dbValues = new double[gridCenters.Count];
            Parallel.For(0, gridCenters.Count, i =>
            {
                dbValues[i] = CalculateDB(gridCenters[i]);
            });

            // 3. PARALLEL COMPUTE TEST POINTS
            double[] testDbValues = new double[testPointsIn.Count];
            if (testPointsIn.Count > 0)
            {
                Parallel.For(0, testPointsIn.Count, i =>
                {
                    testDbValues[i] = CalculateDB(testPointsIn[i]);
                });
            }

            // Determine Min/Max
            double minDb = dbValues.Length > 0 ? dbValues.Min() : 0;
            double maxDb = dbValues.Length > 0 ? dbValues.Max() : 0;
            double avgDb = dbValues.Length > 0 ? dbValues.Average() : 0;

            if (colors == null || colors.Count == 0)
            {
                colors = new List<Color> { Color.LimeGreen, Color.Yellow, Color.Orange, Color.Red, Color.DarkRed };
            }

            Mesh plainMesh = new Mesh();
            Mesh heatMesh = new Mesh();

            for (int i = 0; i < gridCenters.Count; i++)
            {
                double val = dbValues[i];
                double t = (maxDb - minDb) == 0 ? 0 : (val - minDb) / (maxDb - minDb);
                t = Math.Max(0.0, Math.Min(1.0, t));
                Color c = GetColorInterpolated(colors, t);

                Point3d[] q = quads[i];
                int vBase = heatMesh.Vertices.Count;
                
                plainMesh.Vertices.Add(q[0]); plainMesh.Vertices.Add(q[1]);
                plainMesh.Vertices.Add(q[2]); plainMesh.Vertices.Add(q[3]);
                plainMesh.Faces.AddFace(vBase, vBase + 1, vBase + 2, vBase + 3);

                heatMesh.Vertices.Add(q[0]); heatMesh.Vertices.Add(q[1]);
                heatMesh.Vertices.Add(q[2]); heatMesh.Vertices.Add(q[3]);
                heatMesh.Faces.AddFace(vBase, vBase + 1, vBase + 2, vBase + 3);

                heatMesh.VertexColors.Add(c); heatMesh.VertexColors.Add(c);
                heatMesh.VertexColors.Add(c); heatMesh.VertexColors.Add(c);
            }

            // JSON Dashboard Data
            var dashData = new
            {
                Title = "Noise Analysis (dB)",
                LegendType = "Gradient",
                Colors = colors.Select(c => $"rgba({c.R},{c.G},{c.B},1)").ToList(),
                Labels = new List<string> { $"{minDb:F1} dB", $"{maxDb:F1} dB" },
                Metrics = new List<object>
                {
                    new { Name = "Max Noise", Value = $"{maxDb:F1} dB" },
                    new { Name = "Min Noise", Value = $"{minDb:F1} dB" },
                    new { Name = "Average", Value = $"{avgDb:F1} dB" }
                }
            };
            string jsonOut = JsonConvert.SerializeObject(dashData);

            DA.SetData(0, heatMesh);
            DA.SetData(1, plainMesh);
            DA.SetDataList(2, gridCenters);
            DA.SetDataList(3, dbValues.ToList());
            
            DA.SetDataList(4, testPointsIn);
            DA.SetDataList(5, testDbValues.ToList());

            DA.SetData(6, jsonOut);
            
            this.Message = "Complete";
        }

        private Color GetColorInterpolated(List<Color> palette, double t)
        {
            if (palette.Count == 0) return Color.Black;
            if (palette.Count == 1) return palette[0];

            double scaledT = t * (palette.Count - 1);
            int idx = (int)Math.Floor(scaledT);
            if (idx >= palette.Count - 1) return palette.Last();
            if (idx < 0) return palette.First();

            double frac = scaledT - idx;
            Color c1 = palette[idx];
            Color c2 = palette[idx + 1];

            int r = (int)(c1.R + (c2.R - c1.R) * frac);
            int g = (int)(c1.G + (c2.G - c1.G) * frac);
            int b = (int)(c1.B + (c2.B - c1.B) * frac);
            return Color.FromArgb(255, r, g, b);
        }

        // Standard Auto-Wiring Implementation
        private void AutoWireDefaultInputs(GH_Document document)
        {
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 0, false, 200, -80);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 5.0, 1.5, 200, 40);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 1.0, 20.0, 5.0, 200, 80);
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc != null) AutoWireDefaultInputs(doc);
            });
        }
    }
}
