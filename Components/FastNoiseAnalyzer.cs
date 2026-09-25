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
              "A rapid pseudo-acoustic noise analysis component. Maps decibel (dB) decay over distance and occlusions onto arbitrary meshes.",
              "Enzyme", "Environmental")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("FastNoiseAnalyzer.png");

        public override Guid ComponentGuid => new Guid("11112222-3333-4444-5555-666677778888");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Global execution toggle switch", GH_ParamAccess.item, false);
            pManager.AddMeshParameter("AnalysisMeshes", "Target", "Building masses or terrain to project heatmap onto", GH_ParamAccess.list);
            pManager.AddMeshParameter("ContextBuildings", "HardCtx", "Solid meshes for hard sound occlusion (-15dB)", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddMeshParameter("SoftContext", "SoftCtx", "Vegetation/porous meshes for minor occlusion (-5dB)", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddPointParameter("Emitters", "Emitters", "Noise source points (e.g., roads, machinery)", GH_ParamAccess.list);
            pManager.AddNumberParameter("EmitterPower", "Power(dB)", "Decibel level per emitter at 1m (default 85dB)", GH_ParamAccess.list, 85.0);
            pManager.AddPointParameter("AnalysisPoints", "PtsIn", "Specific discrete test locations (e.g. windows) to test individually", GH_ParamAccess.list);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("SensorOffset", "Offset", "Distance to offset sensors from surfaces to avoid self-occlusion (m)", GH_ParamAccess.item, 0.2);
            pManager.AddColourParameter("CustomColors", "Colors", "Color spectrum override (Quiet -> Loud)", GH_ParamAccess.list);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("NoiseMeshes", "Meshes", "Vertex-colored meshes representing the noise heatmap", GH_ParamAccess.list);
            pManager.AddPointParameter("MeshPoints", "MeshPts", "The evaluated vertex coordinates of the analysis meshes", GH_ParamAccess.list);
            pManager.AddNumberParameter("MeshValues", "Mesh_dB", "The decibel values matching the MeshPoints", GH_ParamAccess.list);
            pManager.AddPointParameter("AnalysisPtsOut", "PtsOut", "Pass-through for AnalysisPoints input", GH_ParamAccess.list);
            pManager.AddNumberParameter("AnalysisPtsValues", "Pts_dB", "Raw dB values precisely at the AnalysisPoints", GH_ParamAccess.list);
            pManager.AddTextParameter("DashboardData", "Dashboard", "JSON legend data", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Disclaimer on methodology and pseudo-acoustic limitations", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool run = false;
            if (!DA.GetData(0, ref run)) return;

            List<Mesh> targetMeshes = new List<Mesh>();
            if (!DA.GetDataList(1, targetMeshes)) return;
            if (targetMeshes.Count == 0) return;

            List<Mesh> hardContext = new List<Mesh>();
            DA.GetDataList(2, hardContext);

            List<Mesh> softContext = new List<Mesh>();
            DA.GetDataList(3, softContext);

            List<Point3d> emitters = new List<Point3d>();
            if (!DA.GetDataList(4, emitters)) return;
            if (emitters.Count == 0) return;

            List<double> power = new List<double>();
            DA.GetDataList(5, power);
            if (power.Count == 0) power.Add(85.0);

            List<Point3d> testPointsIn = new List<Point3d>();
            DA.GetDataList(6, testPointsIn);

            double offset = 0.2;
            DA.GetData(7, ref offset);

            List<Color> colors = new List<Color>();
            DA.GetDataList(8, colors);

            string disclaimer = "METHODOLOGY & INACCURACIES:\n" +
                                "- Propagation uses standard Free-Field Inverse-Square Law (-20*log10(r)).\n" +
                                "- Addition is strictly energetic (logarithmic summation).\n" +
                                "- Hard Occlusion (Buildings/Terrain): strict -15dB penalty.\n" +
                                "- Soft Occlusion (Vegetation): minor -5dB penalty.\n" +
                                "- NO complex bouncing, diffraction (Fresnel zones), reverberation, or material absorption is computed.\n" +
                                "- Best used for rapid early-stage urban blocking, not for certified acoustic engineering.";
            DA.SetData(6, disclaimer);

            if (!run)
            {
                this.Message = $"{this.NickName}\nOFF";
                return;
            }

            List<Mesh> collidersHard = new List<Mesh>(hardContext);
            List<Mesh> collidersSoft = new List<Mesh>(softContext);

            Func<Point3d, double> CalculateDB = (pt) =>
            {
                double totalEnergy = 0.0;
                for (int k = 0; k < emitters.Count; k++)
                {
                    double d = pt.DistanceTo(emitters[k]);
                    if (d < 1.0) d = 1.0;
                    
                    double pwr = power[k % power.Count];
                    double rawDb = pwr - 20 * Math.Log10(d);

                    Ray3d toEmitter = new Ray3d(pt, emitters[k] - pt);
                    bool hitHard = false;
                    bool hitSoft = false;
                    
                    foreach (var m in collidersHard)
                    {
                        double thit = Rhino.Geometry.Intersect.Intersection.MeshRay(m, toEmitter);
                        if (thit >= 0.0 && thit < d)
                        {
                            hitHard = true;
                            break;
                        }
                    }
                    
                    if (hitHard)
                    {
                        rawDb -= 15.0;
                    }
                    else if (collidersSoft.Count > 0)
                    {
                        foreach (var m in collidersSoft)
                        {
                            double thit = Rhino.Geometry.Intersect.Intersection.MeshRay(m, toEmitter);
                            if (thit >= 0.0 && thit < d)
                            {
                                hitSoft = true;
                                break;
                            }
                        }
                        if (hitSoft) rawDb -= 5.0;
                    }

                    if (rawDb > 0)
                        totalEnergy += Math.Pow(10, rawDb / 10.0);
                }
                return totalEnergy > 0 ? 10 * Math.Log10(totalEnergy) : 0;
            };

            List<double[]> allMeshVals = new List<double[]>();
            double globalMinDb = double.MaxValue;
            double globalMaxDb = double.MinValue;
            double sumDb = 0;
            int totalVerts = 0;
            List<Mesh> outMeshes = new List<Mesh>();

            foreach (Mesh m in targetMeshes)
            {
                if (m == null || !m.IsValid) continue;
                Mesh workMesh = m.DuplicateMesh();
                workMesh.Normals.ComputeNormals();
                outMeshes.Add(workMesh);

                double[] dbVals = new double[workMesh.Vertices.Count];
                
                Parallel.For(0, workMesh.Vertices.Count, i =>
                {
                    Point3d pt = workMesh.Vertices[i];
                    Vector3f normal = workMesh.Normals[i];
                    Point3d offsetPt = pt + new Vector3d(normal.X, normal.Y, normal.Z) * offset;
                    dbVals[i] = CalculateDB(offsetPt);
                });

                allMeshVals.Add(dbVals);

                for (int i = 0; i < dbVals.Length; i++)
                {
                    if (dbVals[i] < globalMinDb) globalMinDb = dbVals[i];
                    if (dbVals[i] > globalMaxDb) globalMaxDb = dbVals[i];
                    sumDb += dbVals[i];
                    totalVerts++;
                }
            }

            if (globalMinDb == double.MaxValue) globalMinDb = 0;
            if (globalMaxDb == double.MinValue) globalMaxDb = 0;
            double avgDb = totalVerts > 0 ? sumDb / totalVerts : 0;

            if (colors == null || colors.Count == 0)
            {
                colors = new List<Color> { Color.LimeGreen, Color.Yellow, Color.Orange, Color.Red, Color.DarkRed };
            }

            for (int k = 0; k < outMeshes.Count; k++)
            {
                Mesh m = outMeshes[k];
                double[] dbVals = allMeshVals[k];
                m.VertexColors.Clear();
                
                for (int i = 0; i < dbVals.Length; i++)
                {
                    double val = dbVals[i];
                    double t = (globalMaxDb - globalMinDb) == 0 ? 0 : (val - globalMinDb) / (globalMaxDb - globalMinDb);
                    t = Math.Max(0.0, Math.Min(1.0, t));
                    m.VertexColors.Add(GetColorInterpolated(colors, t));
                }
            }

            double[] testDbValues = new double[testPointsIn.Count];
            if (testPointsIn.Count > 0)
            {
                Parallel.For(0, testPointsIn.Count, i =>
                {
                    testDbValues[i] = CalculateDB(testPointsIn[i]);
                });
            }

            var dashData = new
            {
                Title = "Noise Analysis (dB)",
                LegendType = "Gradient",
                Colors = colors.Select(c => $"rgba({c.R},{c.G},{c.B},1)").ToList(),
                Labels = new List<string> { $"{globalMinDb:F1} dB", $"{globalMaxDb:F1} dB" },
                Metrics = new List<object>
                {
                    new { Name = "Max Noise", Value = $"{globalMaxDb:F1} dB" },
                    new { Name = "Min Noise", Value = $"{globalMinDb:F1} dB" },
                    new { Name = "Average", Value = $"{avgDb:F1} dB" }
                }
            };
            string jsonOut = JsonConvert.SerializeObject(dashData);

            List<Point3d> allMeshPts = new List<Point3d>();
            List<double> allMeshDb = new List<double>();
            
            for (int k = 0; k < outMeshes.Count; k++)
            {
                allMeshPts.AddRange(outMeshes[k].Vertices.Select(v => new Point3d(v.X, v.Y, v.Z)));
                allMeshDb.AddRange(allMeshVals[k]);
            }

            DA.SetDataList(0, outMeshes);
            DA.SetDataList(1, allMeshPts);
            DA.SetDataList(2, allMeshDb);
            DA.SetDataList(3, testPointsIn);
            DA.SetDataList(4, testDbValues.ToList());
            DA.SetData(5, jsonOut);
            
            sw.Stop();
            this.Message = $"NoiseEnv\n{sw.Elapsed.TotalMilliseconds:F2} ms\n---\nMax: {globalMaxDb:F1} dB\nMin: {globalMinDb:F1} dB";
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

        private void AutoWireDefaultInputs(GH_Document document)
        {
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 0, false, 200, -80);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 2.0, 0.2, 200, 40);
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
