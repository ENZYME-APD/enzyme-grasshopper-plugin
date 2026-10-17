using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Newtonsoft.Json;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class FastNoiseAnalyzer : GH_Component
    {
        // Representative frequency for broadband A-weighted traffic noise
        private const double FreqHz = 500.0;
        private const double Lambda = 343.0 / FreqHz;          // wavelength (m)
        private const double AirAbsDbPerM = 0.0028;             // ISO 9613-1, 500 Hz, 20 C, 70% RH
        private const double CullBelowMaxDb = 45.0;             // emitters this far below the loudest free-field contribution skip ray tests

        public FastNoiseAnalyzer()
          : base("Fast Noise Analyzer", "NoiseEnv",
              "Rapid engineering-style noise mapping (ISO 9613-2 inspired): geometric spreading, air absorption, ground effect, Maekawa barrier diffraction and foliage attenuation, mapped onto arbitrary meshes.",
              "Enzyme", "Site Analysis")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("FastNoiseAnalyzer.png");

        public override Guid ComponentGuid => new Guid("11112222-3333-4444-5555-666677778888");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Global execution toggle switch", GH_ParamAccess.item, false);
            pManager.AddMeshParameter("AnalysisMeshes", "Target", "Building masses or terrain to project heatmap onto", GH_ParamAccess.list);
            pManager.AddMeshParameter("ContextBuildings", "HardCtx", "Solid meshes acting as acoustic barriers (buildings, walls, terrain berms). Attenuation via Maekawa diffraction over the obstacle top.", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddMeshParameter("SoftContext", "SoftCtx", "Vegetation volumes. Attenuation depends on path length through foliage (ISO 9613-2 Annex A).", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddPointParameter("Emitters", "Emitters", "Noise source points (e.g. output of Noise Emitter From Curves, machinery)", GH_ParamAccess.list);
            pManager.AddNumberParameter("EmitterPower", "Power(dB)", "Source level Lp at 1 m per emitter point [dB(A)] (default 85). Re-uses the last value if the list is shorter.", GH_ParamAccess.list, 85.0);
            pManager.AddPointParameter("AnalysisPoints", "PtsIn", "Specific discrete test locations (e.g. windows) to test individually", GH_ParamAccess.list);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("SensorOffset", "Offset", "Distance to offset sensors from surfaces to avoid self-occlusion (m)", GH_ParamAccess.item, 0.2);
            pManager.AddColourParameter("CustomColors", "Colors", "Color spectrum override (Quiet -> Loud)", GH_ParamAccess.list);
            pManager[8].Optional = true;
            pManager.AddMeshParameter("Ground", "Ground", "Optional terrain mesh used to compute source/receiver heights for the ground effect. Without it, ground effect is skipped.", GH_ParamAccess.item);
            pManager[9].Optional = true;
            pManager.AddNumberParameter("GroundFactor", "G", "Ground absorption factor: 0 = hard (asphalt, water, paving), 1 = soft (grass, fields). Used only when Ground is supplied.", GH_ParamAccess.item, 0.5);
            pManager[10].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("NoiseMeshes", "Meshes", "Vertex-colored meshes representing the noise heatmap", GH_ParamAccess.list);
            pManager.AddPointParameter("MeshPoints", "MeshPts", "The evaluated vertex coordinates of the analysis meshes", GH_ParamAccess.list);
            pManager.AddNumberParameter("MeshValues", "Mesh_dB", "Sound pressure level [dB(A)] at the MeshPoints", GH_ParamAccess.list);
            pManager.AddPointParameter("AnalysisPtsOut", "PtsOut", "Pass-through for AnalysisPoints input", GH_ParamAccess.list);
            pManager.AddNumberParameter("AnalysisPtsValues", "Pts_dB", "Sound pressure level [dB(A)] at the AnalysisPoints", GH_ParamAccess.list);
            pManager.AddTextParameter("DashboardData", "Dashboard", "JSON legend data", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Methodology, results summary and limitations", GH_ParamAccess.item);
        }

        // ------------------------------------------------------------------
        // Height raster of hard obstacles (top surface), used to find the
        // diffraction path over barriers.
        // ------------------------------------------------------------------
        private sealed class HeightField
        {
            public double X0, Y0, Res;
            public int Nx, Ny;
            public float[] H; // NaN = no obstacle

            public double At(double x, double y)
            {
                int ix = (int)Math.Floor((x - X0) / Res);
                int iy = (int)Math.Floor((y - Y0) / Res);
                if (ix < 0 || iy < 0 || ix >= Nx || iy >= Ny) return double.NaN;
                return H[ix + iy * Nx];
            }

            public static HeightField Build(Mesh m)
            {
                BoundingBox bb = m.GetBoundingBox(true);
                double area = Math.Max(1.0, (bb.Max.X - bb.Min.X) * (bb.Max.Y - bb.Min.Y));
                double res = Math.Max(0.5, Math.Sqrt(area / 250000.0));
                var hf = new HeightField
                {
                    X0 = bb.Min.X - res,
                    Y0 = bb.Min.Y - res,
                    Res = res
                };
                hf.Nx = (int)Math.Ceiling((bb.Max.X - bb.Min.X) / res) + 2;
                hf.Ny = (int)Math.Ceiling((bb.Max.Y - bb.Min.Y) / res) + 2;
                hf.H = new float[hf.Nx * hf.Ny];
                double zTop = bb.Max.Z + 10.0;
                MeshPrep.WarmUp(m);

                Parallel.For(0, hf.Ny, iy =>
                {
                    for (int ix = 0; ix < hf.Nx; ix++)
                    {
                        double x = hf.X0 + (ix + 0.5) * res;
                        double y = hf.Y0 + (iy + 0.5) * res;
                        double t = Intersection.MeshRay(m, new Ray3d(new Point3d(x, y, zTop), -Vector3d.ZAxis));
                        hf.H[ix + iy * hf.Nx] = t >= 0 ? (float)(zTop - t) : float.NaN;
                    }
                });
                return hf;
            }
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

            Mesh ground = null;
            DA.GetData(9, ref ground);

            double groundG = 0.5;
            DA.GetData(10, ref groundG);
            groundG = Math.Max(0.0, Math.Min(1.0, groundG));

            if (!run)
            {
                DA.SetData(6, BuildInfo(null));
                Hud.Off(this);
                return;
            }

            // ---- Merge colliders once (one accelerated ray query instead of one per mesh) ----
            Mesh hardAll = new Mesh();
            foreach (var m in hardContext) if (m != null && m.IsValid) hardAll.Append(m);
            bool hasHard = hardAll.Faces.Count > 0;

            Mesh softAll = new Mesh();
            foreach (var m in softContext) if (m != null && m.IsValid) softAll.Append(m);
            bool hasSoft = softAll.Faces.Count > 0;
            MeshPrep.WarmUp(hardAll, softAll); // shared by every receiver thread below

            HeightField hf = hasHard ? HeightField.Build(hardAll) : null;

            bool hasGround = ground != null && ground.IsValid && ground.Faces.Count > 0;
            double groundTopZ = hasGround ? ground.GetBoundingBox(true).Max.Z + 100.0 : 0.0;
            Func<Point3d, double> heightAboveGround = (p) =>
            {
                double t = Intersection.MeshRay(ground, new Ray3d(new Point3d(p.X, p.Y, groundTopZ), -Vector3d.ZAxis));
                if (t < 0) return double.NaN;
                return Math.Max(0.0, p.Z - (groundTopZ - t));
            };

            int E = emitters.Count;
            double[] pwr = new double[E];
            double[] emitterH = new double[E];
            for (int k = 0; k < E; k++)
            {
                pwr[k] = power[Math.Min(k, power.Count - 1)];
                emitterH[k] = hasGround ? heightAboveGround(emitters[k]) : double.NaN;
            }

            long rayTests = 0, culled = 0;

            Func<Point3d, double> CalculateDB = (pt) =>
            {
                double recH = hasGround ? heightAboveGround(pt) : double.NaN;

                // 1) Free-field levels (spreading + air absorption) for culling
                double[] lff = new double[E];
                double[] dist = new double[E];
                double lmax = double.MinValue;
                for (int k = 0; k < E; k++)
                {
                    double d = pt.DistanceTo(emitters[k]);
                    dist[k] = d;
                    double dc = Math.Max(1.0, d);
                    lff[k] = pwr[k] - 20.0 * Math.Log10(dc) - AirAbsDbPerM * dc;
                    if (lff[k] > lmax) lmax = lff[k];
                }

                double totalEnergy = 0.0;
                long localRays = 0, localCulled = 0;

                for (int k = 0; k < E; k++)
                {
                    double level = lff[k];
                    double d = dist[k];

                    if (level < lmax - CullBelowMaxDb || d < 1e-6)
                    {
                        // Negligible contribution: add free-field energy without ray tests (conservative)
                        localCulled++;
                        totalEnergy += Math.Pow(10, level / 10.0);
                        continue;
                    }

                    // 2) Ground effect, ISO 9613-2 eq.(10), scaled by ground factor G
                    double aGr = 0.0;
                    if (hasGround && groundG > 0 && !double.IsNaN(recH) && !double.IsNaN(emitterH[k]))
                    {
                        double hm = 0.5 * (recH + emitterH[k]);
                        double dd = Math.Max(1.0, d);
                        aGr = groundG * Math.Max(0.0, 4.8 - (2.0 * hm / dd) * (17.0 + 300.0 / dd));
                    }

                    Point3d src = emitters[k];
                    Vector3d dir = src - pt;
                    dir.Unitize();
                    Ray3d ray = new Ray3d(pt, dir); // unitized: t is in metres

                    // 3) Hard barriers: Maekawa diffraction over the obstacle tops
                    double aBar = 0.0;
                    bool blocked = false;
                    if (hasHard)
                    {
                        localRays++;
                        double thit = Intersection.MeshRay(hardAll, ray);
                        if (thit >= 0.0 && thit < d - 1e-3)
                        {
                            blocked = true;
                            aBar = BarrierAttenuation(pt, src, hf);
                        }
                    }

                    // ISO 9613-2: when screened, barrier replaces ground attenuation (A_bar = Dz - A_gr >= 0)
                    double att = blocked ? Math.Max(aBar, aGr) : aGr;

                    // 4) Foliage (only meaningful if path is not already screened by a building)
                    if (hasSoft && !blocked)
                    {
                        localRays++;
                        double tSoft = Intersection.MeshRay(softAll, ray);
                        if (tSoft >= 0.0 && tSoft < d - 1e-3)
                        {
                            double df = FoliagePathLength(softAll, pt, src);
                            att += FoliageAttenuation(df);
                        }
                    }

                    level -= att;
                    totalEnergy += Math.Pow(10, level / 10.0);
                }

                System.Threading.Interlocked.Add(ref rayTests, localRays);
                System.Threading.Interlocked.Add(ref culled, localCulled);
                return totalEnergy > 0 ? 10 * Math.Log10(totalEnergy) : 0;
            };

            List<double[]> allMeshVals = new List<double[]>();
            double globalMinDb = double.MaxValue;
            double globalMaxDb = double.MinValue;
            double sumDb = 0, sumEnergy = 0;
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
                    sumEnergy += Math.Pow(10, dbVals[i] / 10.0);
                    totalVerts++;
                }
            }

            if (globalMinDb == double.MaxValue) globalMinDb = 0;
            if (globalMaxDb == double.MinValue) globalMaxDb = 0;
            double avgDb = totalVerts > 0 ? sumDb / totalVerts : 0;
            double energeticAvgDb = totalVerts > 0 && sumEnergy > 0 ? 10 * Math.Log10(sumEnergy / totalVerts) : 0;

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
                Title = "Noise Analysis (dB(A))",
                Type = "Gradient",
                Colors = colors.Select(c => new { R = c.R, G = c.G, B = c.B }).ToList(),
                Labels = new List<string> { $"{globalMinDb:F1} dB(A)", $"{globalMaxDb:F1} dB(A)" },
                Metrics = new List<object>
                {
                    new { Name = "Max Noise", Value = $"{globalMaxDb:F1} dB(A)" },
                    new { Name = "Min Noise", Value = $"{globalMinDb:F1} dB(A)" },
                    new { Name = "Energetic Avg", Value = $"{energeticAvgDb:F1} dB(A)" },
                    new { Name = "Arithmetic Avg", Value = $"{avgDb:F1} dB(A)" }
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

            sw.Stop();

            var stats = new RunStats
            {
                Receivers = totalVerts + testPointsIn.Count,
                Emitters = E,
                RayTests = rayTests,
                Culled = culled,
                MaxDb = globalMaxDb,
                MinDb = globalMinDb,
                EnergeticAvg = energeticAvgDb,
                ArithmeticAvg = avgDb,
                HasHard = hasHard,
                HasSoft = hasSoft,
                HasGround = hasGround,
                G = groundG,
                HeightFieldRes = hf != null ? hf.Res : 0,
                Ms = sw.Elapsed.TotalMilliseconds
            };

            DA.SetDataList(0, outMeshes);
            DA.SetDataList(1, allMeshPts);
            DA.SetDataList(2, allMeshDb);
            DA.SetDataList(3, testPointsIn);
            DA.SetDataList(4, testDbValues.ToList());
            DA.SetData(5, jsonOut);
            DA.SetData(6, BuildInfo(stats));

            Hud.Done(this, sw.Elapsed.TotalMilliseconds, $"Max: {globalMaxDb:F1} dB(A)\nMin: {globalMinDb:F1} dB(A)");
        }

        // ------------------------------------------------------------------
        // Maekawa / ISO 9613-2 screening: Dz = 10 log10(3 + 20 N), N = 2 delta / lambda.
        // delta = extra path length over the obstacle tops, found as the upper convex
        // hull ("rubber band") of the height profile between receiver and source.
        // ------------------------------------------------------------------
        private static double BarrierAttenuation(Point3d rec, Point3d src, HeightField hf)
        {
            const double fallbackDb = 5.0; // line of sight blocked but no top found (e.g. overhangs)
            if (hf == null) return fallbackDb;

            double dx = src.X - rec.X, dy = src.Y - rec.Y;
            double horiz = Math.Sqrt(dx * dx + dy * dy);
            if (horiz < 1e-6) return fallbackDb;

            int n = (int)Math.Max(4, Math.Min(400, Math.Ceiling(horiz / hf.Res)));

            // Profile in (s, z): s = horizontal distance from receiver
            var prof = new List<KeyValuePair<double, double>>(n + 2);
            prof.Add(new KeyValuePair<double, double>(0.0, rec.Z));
            for (int i = 1; i < n; i++)
            {
                double t = (double)i / n;
                double h = hf.At(rec.X + dx * t, rec.Y + dy * t);
                if (double.IsNaN(h)) continue;
                double zLos = rec.Z + (src.Z - rec.Z) * t;
                if (h > zLos) prof.Add(new KeyValuePair<double, double>(horiz * t, h));
            }
            prof.Add(new KeyValuePair<double, double>(horiz, src.Z));
            if (prof.Count == 2) return fallbackDb;

            // Upper convex hull (Andrew's monotone chain, points already sorted by s)
            var hull = new List<KeyValuePair<double, double>>();
            foreach (var p in prof)
            {
                while (hull.Count >= 2)
                {
                    var a = hull[hull.Count - 2];
                    var b = hull[hull.Count - 1];
                    double cross = (b.Key - a.Key) * (p.Value - a.Value) - (b.Value - a.Value) * (p.Key - a.Key);
                    if (cross >= 0) hull.RemoveAt(hull.Count - 1); else break;
                }
                hull.Add(p);
            }

            double pathLen = 0.0;
            for (int i = 1; i < hull.Count; i++)
            {
                double ds = hull[i].Key - hull[i - 1].Key;
                double dz = hull[i].Value - hull[i - 1].Value;
                pathLen += Math.Sqrt(ds * ds + dz * dz);
            }
            double direct = Math.Sqrt(horiz * horiz + (src.Z - rec.Z) * (src.Z - rec.Z));
            double delta = pathLen - direct;
            if (delta <= 0) return fallbackDb;

            double N = 2.0 * delta / Lambda;
            double dz2 = 10.0 * Math.Log10(3.0 + 20.0 * N);
            int edges = hull.Count - 2;
            double cap = edges >= 2 ? 25.0 : 20.0; // ISO 9613-2 limits: single 20 dB, double 25 dB
            return Math.Min(cap, Math.Max(0.0, dz2));
        }

        // Total straight-line path length (m) through the foliage volumes.
        private static double FoliagePathLength(Mesh soft, Point3d rec, Point3d src)
        {
            Point3d[] hits = Intersection.MeshLine(soft, new Line(rec, src));
            if (hits == null || hits.Length == 0) return 0.0;
            var ds = hits.Select(h => h.DistanceTo(rec)).OrderBy(x => x).ToList();
            double total = rec.DistanceTo(src);
            // Odd count: receiver or source inside foliage; close the open interval at the source
            if (ds.Count % 2 == 1) ds.Add(total);
            double len = 0.0;
            for (int i = 0; i + 1 < ds.Count; i += 2) len += ds[i + 1] - ds[i];
            return len;
        }

        // ISO 9613-2 Annex A, Table A.1 (500 Hz band): 10-20 m -> 1 dB, 20-200 m -> 0.05 dB/m
        private static double FoliageAttenuation(double df)
        {
            if (df < 10.0) return 0.0;
            if (df < 20.0) return 1.0;
            return 0.05 * Math.Min(200.0, df);
        }

        private sealed class RunStats
        {
            public int Receivers, Emitters;
            public long RayTests, Culled;
            public double MaxDb, MinDb, EnergeticAvg, ArithmeticAvg, G, HeightFieldRes, Ms;
            public bool HasHard, HasSoft, HasGround;
        }

        private static string BuildInfo(RunStats s)
        {
            string summary = s == null ? "Status: OFF (set Run = True)\n\n" :
                "RESULTS:\n" +
                $"- Receivers: {s.Receivers:N0} | Emitters: {s.Emitters:N0} | Time: {s.Ms:F0} ms\n" +
                $"- Ray tests: {s.RayTests:N0} | Culled source-receiver pairs (<-{CullBelowMaxDb:F0} dB): {s.Culled:N0}\n" +
                $"- Max: {s.MaxDb:F1} dB(A) | Min: {s.MinDb:F1} dB(A)\n" +
                $"- Energetic average: {s.EnergeticAvg:F1} dB(A) | Arithmetic average of dB values: {s.ArithmeticAvg:F1} dB(A)\n" +
                $"- Barriers: {(s.HasHard ? $"on (height raster {s.HeightFieldRes:F2} m)" : "none")} | Foliage: {(s.HasSoft ? "on" : "none")} | Ground effect: {(s.HasGround ? $"on (G = {s.G:F2})" : "off (no Ground mesh)")}\n\n";

            return
                "FAST NOISE ANALYZER (ISO 9613-2 INSPIRED)\n" +
                "========================================\n\n" +
                summary +
                "METHOD (per source-receiver pair, energetic summation over all sources):\n" +
                "1. Geometric spreading: Lp = Lp(1 m) - 20*log10(d) (point source, d >= 1 m).\n" +
                "   Roads should come from 'Noise Emitter From Curves', which calibrates point levels as a line source.\n" +
                $"2. Air absorption: {AirAbsDbPerM * 1000:F1} dB/km (ISO 9613-1, 500 Hz, 20 C, 70% RH).\n" +
                "3. Ground effect (only with Ground mesh): ISO 9613-2 eq.10, A_gr = G * max(0, 4.8 - (2 h_m / d)(17 + 300 / d)).\n" +
                "4. Barriers (HardCtx): Maekawa, Dz = 10*log10(3 + 20 N), N = 2*delta/lambda (lambda = 0.69 m @ 500 Hz).\n" +
                "   delta is the extra path over the obstacle tops ('rubber band' over the roof profile). Capped at 20 dB (single) / 25 dB (multiple edges).\n" +
                "   When screened, the barrier replaces the ground term (ISO: A_bar = Dz - A_gr).\n" +
                "5. Foliage (SoftCtx): ISO 9613-2 Annex A, by path length through vegetation: <10 m: 0 dB, 10-20 m: 1 dB, 20-200 m: 0.05 dB/m.\n" +
                "6. Sources more than 45 dB below the loudest free-field contribution are added unscreened (negligible, conservative).\n\n" +
                "UNITS:\n" +
                "- All values are dB(A), assuming A-weighted source levels. Single-band (500 Hz) approximation of broadband traffic noise.\n\n" +
                "LIMITATIONS:\n" +
                "- No reflections (facade / street-canyon reflections typically add +1 to +3 dB), no reverberation.\n" +
                "- Diffraction only over the top of obstacles; lateral paths around building corners are ignored (can over-attenuate behind narrow towers).\n" +
                "- No meteorological effects (wind / temperature gradients).\n" +
                "- Terrain only screens sound if it is also supplied as HardCtx.\n" +
                "- Suitable for early-stage design comparison, not for certified acoustic assessments.";
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
