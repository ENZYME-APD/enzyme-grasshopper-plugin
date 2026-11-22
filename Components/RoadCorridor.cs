using Enzyme.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;

namespace Enzyme.Components
{
    /// <summary>
    /// Sweeps a road template (lanes, kerbs, cycle tracks, footways, verges, ditches, with pavement
    /// layers) along a 3D centreline, daylights cut / fill slopes to the terrain, and measures
    /// earthworks to subgrade with a mass haul. Method after civil corridor modelling (as in the
    /// FreeCAD Road workbench / Open Infra Studio, GPL, not copied): written from scratch.
    /// </summary>
    public class RoadCorridor : EnzymeComponent
    {
        protected override bool UsesRunGate => true;

        public RoadCorridor()
          : base("Road Corridor", "Corridor",
              "Sweeps a road cross-section template along a 3D centreline (e.g. from Auto-Grade Road), daylights cut and fill slopes to the terrain, and reports earthworks to subgrade, pavement quantities and a mass haul.",
              "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Centreline", "C", "3D road centreline. Its Z is the finished road level on the axis (use Auto-Grade Road for a draped, slope-limited profile).", GH_ParamAccess.item);
            pManager.AddMeshParameter("Terrain", "T", "Existing ground mesh.", GH_ParamAccess.item);
            pManager.AddTextParameter("Template", "Tpl", "Preset name (residential street, urban street, rural road, cycle path, access track) or a JSON template. See the Template output for the format.", GH_ParamAccess.item, "residential street");
            pManager.AddNumberParameter("Interval", "Int", "Distance between cross sections (m).", GH_ParamAccess.item, 5.0);
            pManager.AddNumberParameter("Design Speed", "V", "Design speed (km/h), used for superelevation on curves.", GH_ParamAccess.item, 30.0);
            pManager.AddNumberParameter("Max Superelevation", "eMax", "Maximum superelevation (%). 0 = keep the normal crown (typical for urban streets).", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Runoff Length", "Runoff", "Length (m) over which superelevation is introduced.", GH_ParamAccess.item, 30.0);
            pManager.AddNumberParameter("Cut Reuse Factor", "Reuse", "Share of bank cut that can be reused as compacted fill (0 to 1).", GH_ParamAccess.item, 0.9);
            pManager.AddNumberParameter("Free Haul", "FreeHaul", "Free-haul distance (m) for the overhaul figure.", GH_ParamAccess.item, 150.0);
            pManager.AddBooleanParameter("Run", "Run", "False = keep showing the last results (cached).", GH_ParamAccess.item, true);
            for (int i = 2; i <= 9; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Design Surface", "Surface", "Finished road surface with daylight slopes, coloured by part.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Subgrade", "Subgrade", "Underside of the pavement build-up (formation level for earthworks).", GH_ParamAccess.item);
            pManager.AddCurveParameter("Feature Lines", "Lines", "3D lines along every part edge.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Daylight Lines", "Daylight", "Where the cut / fill slopes meet the ground, left and right.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Sections", "Sections", "Design cross section at each station (3D).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Ground Sections", "Ground", "Existing ground along each cross section (3D).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Stations", "Sta", "Chainage of each cross section (m).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Cut Areas", "CutA", "Cut area to subgrade per section (m²).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Fill Areas", "FillA", "Fill area to subgrade per section (m²).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Mass Haul", "MassHaul", "Mass haul diagram drawn below the corridor: mass line and balance (zero) line. X = chainage, Y = scaled volume.", GH_ParamAccess.list);
            pManager.AddTextParameter("Quantities", "Qty", "JSON: earthworks, mass haul, pavement layer volumes, surface areas, kerb length.", GH_ParamAccess.item);
            pManager.AddTextParameter("Template", "Tpl", "The template used, as editable JSON.", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON legend and metrics (for Analysis Dashboard and Report Metrics).", GH_ParamAccess.item);
        }

        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            ["lane"] = Color.FromArgb(70, 70, 70), ["parking"] = Color.FromArgb(95, 95, 95),
            ["kerb"] = Color.FromArgb(185, 185, 185), ["step"] = Color.FromArgb(160, 160, 160),
            ["footway"] = Color.FromArgb(205, 195, 175), ["cycle"] = Color.FromArgb(175, 70, 60),
            ["shoulder"] = Color.FromArgb(150, 140, 115), ["track"] = Color.FromArgb(165, 145, 105),
            ["verge"] = Color.FromArgb(115, 165, 90), ["median"] = Color.FromArgb(115, 165, 90),
            ["ditch"] = Color.FromArgb(95, 145, 175),
            ["cut slope"] = Color.FromArgb(190, 120, 70), ["fill slope"] = Color.FromArgb(215, 195, 125),
        };

        protected override void Solve(IGH_DataAccess DA, System.Diagnostics.Stopwatch sw)
        {
            Curve crv = null; Mesh terrain = null;
            string tplText = "residential street";
            double interval = 5, speed = 30, eMax = 0, runoff = 30, reuse = 0.9, freeHaul = 150;
            if (!DA.GetData(0, ref crv) || crv == null) return;
            if (!DA.GetData(1, ref terrain) || terrain == null) return;
            DA.GetData(2, ref tplText); DA.GetData(3, ref interval); DA.GetData(4, ref speed);
            DA.GetData(5, ref eMax); DA.GetData(6, ref runoff); DA.GetData(7, ref reuse); DA.GetData(8, ref freeHaul);

            RoadTemplate tpl = RoadTemplate.Parse(tplText);
            interval = Math.Max(0.5, interval);
            reuse = Math.Max(0, Math.Min(1.5, reuse));
            double length = crv.GetLength();
            if (length < 2 * interval) interval = Math.Max(0.5, length / 4);

            // Stations
            var stations = new List<double>();
            for (double s = 0; s < length - 1e-6; s += interval) stations.Add(s);
            stations.Add(length);
            int n = stations.Count;

            BoundingBox tb = terrain.GetBoundingBox(false);
            double zTop = Math.Max(tb.Max.Z, crv.GetBoundingBox(false).Max.Z) + 100;
            MeshPrep.WarmUp(terrain);
            Func<double, double, double> groundAt = (x, y) =>
            {
                double t = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(new Point3d(x, y, zTop), -Vector3d.ZAxis));
                return t < 0 ? double.NaN : zTop - t;
            };

            var P = new Point3d[n];
            var R = new Vector3d[n];
            var eRaw = new double[n];
            bool flatCentreline = true;
            for (int i = 0; i < n; i++)
            {
                crv.LengthParameter(stations[i], out double t);
                P[i] = crv.PointAt(t);
                if (Math.Abs(P[i].Z) > 1e-6) flatCentreline = false;
                Vector3d T = crv.TangentAt(t); T.Z = 0;
                if (!T.Unitize()) T = i > 0 ? new Vector3d(-R[i - 1].Y, R[i - 1].X, 0) : Vector3d.XAxis;
                R[i] = new Vector3d(T.Y, -T.X, 0); // right-hand normal
                Vector3d k = crv.CurvatureAt(t); k.Z = 0;
                double kl = k.Length;
                if (kl > 1e-9 && eMax > 0)
                {
                    bool turnsLeft = (k * (-R[i])) > 0;
                    eRaw[i] = (turnsLeft ? 1 : -1) * CorridorMath.Superelevation(1.0 / kl, speed, eMax);
                }
            }
            if (flatCentreline)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Centreline is flat at Z = 0. It must carry the road levels: use Auto-Grade Road or set the profile first.");

            // Superelevation runoff: moving average over the runoff length.
            var eS = new double[n];
            int half = Math.Max(0, (int)Math.Round(runoff / interval / 2));
            for (int i = 0; i < n; i++)
            {
                double sum = 0; int c = 0;
                for (int j = Math.Max(0, i - half); j <= Math.Min(n - 1, i + half); j++) { sum += eRaw[j]; c++; }
                eS[i] = c > 0 ? sum / c : 0;
            }

            // Cross sections (independent: parallel)
            var sections = new CrossSection[n];
            System.Threading.Tasks.Parallel.For(0, n, i =>
            {
                Point3d p = P[i]; Vector3d r = R[i];
                sections[i] = CorridorMath.Build(tpl, stations[i], p.Z, eS[i], o => groundAt(p.X + r.X * o, p.Y + r.Y * o));
            });
            int m = sections[0].Count;
            if (sections.Any(s => s.Count != m)) throw new InvalidOperationException("Sections have different breakpoint counts.");

            Func<int, int, Point3d> pt = (i, j) => new Point3d(P[i].X + R[i].X * sections[i].O[j], P[i].Y + R[i].Y * sections[i].O[j], sections[i].Z[j]);

            // Design surface and subgrade
            var surface = new Mesh();
            var subgrade = new Mesh();
            for (int i = 0; i + 1 < n; i++)
                for (int j = 0; j + 1 < m; j++)
                {
                    Point3d a = pt(i, j), b = pt(i, j + 1), c = pt(i + 1, j + 1), d = pt(i + 1, j);
                    if (a.DistanceTo(b) < 1e-6 && d.DistanceTo(c) < 1e-6) continue;
                    string kind = sections[i].Kind[j];
                    Color col = Colours.TryGetValue(kind, out Color cc) ? cc : Color.Gray;
                    AddQuad(surface, a, b, c, d, col);
                    double dep0 = sections[i].Depth[j], dep1 = sections[i + 1].Depth[j];
                    if (dep0 > 0 || dep1 > 0)
                        AddQuad(subgrade, a - Vector3d.ZAxis * dep0, b - Vector3d.ZAxis * dep0, c - Vector3d.ZAxis * dep1, d - Vector3d.ZAxis * dep1, Color.FromArgb(150, 110, 80));
                }
            surface.Normals.ComputeNormals(); subgrade.Normals.ComputeNormals();

            // Feature lines (skip duplicate step points), daylight lines
            var features = new List<Curve>();
            for (int j = 0; j < m; j++)
            {
                if (j > 0 && Enumerable.Range(0, n).All(i => pt(i, j).DistanceTo(pt(i, j - 1)) < 1e-6)) continue;
                var pl = new Polyline(Enumerable.Range(0, n).Select(i => pt(i, j)));
                if (pl.Length > 1e-6) features.Add(pl.ToPolylineCurve());
            }
            var daylight = new List<Curve>
            {
                new Polyline(Enumerable.Range(0, n).Select(i => pt(i, 0))).ToPolylineCurve(),
                new Polyline(Enumerable.Range(0, n).Select(i => pt(i, m - 1))).ToPolylineCurve(),
            };

            // Sections and ground lines
            var secCurves = new List<Curve>();
            var groundCurves = new List<Curve>();
            for (int i = 0; i < n; i++)
            {
                secCurves.Add(new Polyline(Enumerable.Range(0, m).Select(j => pt(i, j))).ToPolylineCurve());
                var gp = new Polyline();
                double o0 = sections[i].O[0] - 2, o1 = sections[i].O[m - 1] + 2;
                for (double o = o0; o <= o1 + 1e-9; o += 0.5)
                {
                    double x = P[i].X + R[i].X * o, y = P[i].Y + R[i].Y * o;
                    double g = groundAt(x, y);
                    if (!double.IsNaN(g)) gp.Add(x, y, g);
                }
                if (gp.Count > 1) groundCurves.Add(gp.ToPolylineCurve());
            }

            // Volumes, mass haul, quantities
            double[] st = stations.ToArray();
            double[] cutA = sections.Select(s => s.CutArea).ToArray();
            double[] fillA = sections.Select(s => s.FillArea).ToArray();
            double[] cutV = CorridorMath.Volumes(st, cutA), fillV = CorridorMath.Volumes(st, fillA);
            var haul = MassHaul.Compute(st, cutV, fillV, reuse, freeHaul);

            var layerVol = new SortedDictionary<string, double>();
            var surfArea = new SortedDictionary<string, double>();
            double kerbLength = 0;
            for (int i = 1; i < n; i++)
            {
                double ds = st[i] - st[i - 1];
                foreach (var k in sections[i].LayerArea.Keys.Union(sections[i - 1].LayerArea.Keys))
                {
                    sections[i].LayerArea.TryGetValue(k, out double a1); sections[i - 1].LayerArea.TryGetValue(k, out double a0);
                    Add(layerVol, k, 0.5 * (a0 + a1) * ds);
                }
                foreach (var k in sections[i].SurfaceWidth.Keys.Union(sections[i - 1].SurfaceWidth.Keys))
                {
                    if (k == "step") continue;
                    sections[i].SurfaceWidth.TryGetValue(k, out double w1); sections[i - 1].SurfaceWidth.TryGetValue(k, out double w0);
                    Add(surfArea, k, 0.5 * (w0 + w1) * ds);
                }
                int kerbs = 0;
                for (int j = 0; j + 1 < m; j++) if (sections[i].Kind[j] == "kerb" && Math.Abs(sections[i].O[j + 1] - sections[i].O[j]) > 1e-9) kerbs++;
                kerbLength += kerbs * ds;
            }
            int notDaylit = sections.Count(s => !s.LeftDaylit || !s.RightDaylit);
            if (notDaylit > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{notDaylit} section(s) did not reach the ground within {tpl.MaxDaylight:0} m (off the terrain or a very deep cut / high fill). Their volumes are partial.");
            double maxCut = sections.Max(s => s.MaxCut), maxFill = sections.Max(s => s.MaxFill);
            string[] paved = { "lane", "parking", "cycle", "footway", "shoulder", "track", "kerb" };
            double pavedArea = surfArea.Where(kv => paved.Contains(kv.Key)).Sum(kv => kv.Value);

            // Mass haul diagram below the corridor
            var massCurves = new List<Curve>();
            BoundingBox cb = crv.GetBoundingBox(false);
            cb.Union(surface.GetBoundingBox(false));
            double maxAbs = haul.Mass.Select(Math.Abs).DefaultIfEmpty(0).Max();
            double gH = Math.Max(10, 0.25 * length);
            double scaleY = maxAbs > 1e-9 ? gH / maxAbs : 1;
            double gx0 = cb.Min.X, gy0 = cb.Min.Y - gH - 20;
            massCurves.Add(new Polyline(st.Select((s, i) => new Point3d(gx0 + s, gy0 + haul.Mass[i] * scaleY, 0))).ToPolylineCurve());
            massCurves.Add(new Line(new Point3d(gx0, gy0, 0), new Point3d(gx0 + length, gy0, 0)).ToNurbsCurve());
            foreach (double b in haul.BalancePoints)
                massCurves.Add(new Line(new Point3d(gx0 + b, gy0 - gH * 0.1, 0), new Point3d(gx0 + b, gy0 + gH * 0.1, 0)).ToNurbsCurve());

            var inv = CultureInfo.InvariantCulture;
            var qty = new JObject
            {
                ["template"] = tpl.Name,
                ["length_m"] = Math.Round(length, 2),
                ["earthworks"] = new JObject
                {
                    ["cut_bank_m3"] = Math.Round(haul.CutBank, 1),
                    ["fill_compacted_m3"] = Math.Round(haul.Fill, 1),
                    ["cut_reuse_factor"] = reuse,
                    ["net_m3"] = Math.Round(haul.Net, 1),
                    ["net_meaning"] = haul.Net >= 0 ? "surplus to remove (export)" : "shortfall to bring in (import)",
                    ["max_cut_m"] = Math.Round(maxCut, 2),
                    ["max_fill_m"] = Math.Round(maxFill, 2),
                    ["measured_to"] = "subgrade under pavement, finished surface elsewhere; average end area",
                },
                ["mass_haul"] = new JObject
                {
                    ["balance_points_m"] = new JArray(haul.BalancePoints.Select(b => Math.Round(b, 1))),
                    ["haul_m3km"] = Math.Round(haul.Haul / 1000.0, 1),
                    ["average_haul_m"] = Math.Round(haul.AverageHaul, 1),
                    ["free_haul_m"] = freeHaul,
                    ["overhaul_volume_m3"] = Math.Round(haul.OverhaulVolume, 1),
                },
                ["pavement_layers_m3"] = new JObject(layerVol.Select(kv => new JProperty(kv.Key, Math.Round(kv.Value, 1)))),
                ["surface_areas_m2"] = new JObject(surfArea.Select(kv => new JProperty(kv.Key, Math.Round(kv.Value, 1)))),
                ["kerb_length_m"] = Math.Round(kerbLength, 1),
                ["sections"] = n,
                ["sections_not_daylit"] = notDaylit,
            };

            var metrics = new JArray
            {
                M("Corridor length", length.ToString("F0", inv) + " m"),
                M("Cut (bank)", haul.CutBank.ToString("N0", inv) + " m³"),
                M("Fill (compacted)", haul.Fill.ToString("N0", inv) + " m³"),
                M(haul.Net >= 0 ? "Surplus to export" : "Shortfall to import", Math.Abs(haul.Net).ToString("N0", inv) + " m³"),
                M("Average haul", haul.AverageHaul.ToString("F0", inv) + " m"),
                M("Haul beyond free haul", haul.OverhaulVolume.ToString("N0", inv) + " m³"),
                M("Max cut / fill", maxCut.ToString("F1", inv) + " / " + maxFill.ToString("F1", inv) + " m"),
                M("Paved area", pavedArea.ToString("N0", inv) + " m²"),
            };
            double totalArea = surfArea.Values.Sum();
            var colors = new JArray(); var labels = new JArray(); var bands = new JArray();
            foreach (var kv in surfArea)
            {
                Color c = Colours.TryGetValue(kv.Key, out Color cc) ? cc : Color.Gray;
                colors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                labels.Add(Cap(kv.Key));
                bands.Add(new JObject { ["Name"] = Cap(kv.Key), ["Area"] = Math.Round(kv.Value, 1), ["Pct"] = totalArea > 0 ? Math.Round(kv.Value / totalArea * 100, 2) : 0 });
            }
            var dash = new JObject
            {
                ["Type"] = "Blocks", ["Title"] = "Road Corridor (" + tpl.Name + ")",
                ["Colors"] = colors, ["Labels"] = labels, ["Metrics"] = metrics, ["BandAreas"] = bands,
            };

            Hud.Done(this, sw,
                Hud.Metric("Sections", n),
                Hud.Metric("Cut", haul.CutBank, "m³", 0),
                Hud.Metric("Fill", haul.Fill, "m³", 0),
                Hud.Metric(haul.Net >= 0 ? "Export" : "Import", Math.Abs(haul.Net), "m³", 0));

            DA.SetData(0, surface);
            if (subgrade.Faces.Count > 0) DA.SetData(1, subgrade);
            DA.SetDataList(2, features);
            DA.SetDataList(3, daylight);
            DA.SetDataList(4, secCurves);
            DA.SetDataList(5, groundCurves);
            DA.SetDataList(6, st);
            DA.SetDataList(7, cutA);
            DA.SetDataList(8, fillA);
            DA.SetDataList(9, massCurves);
            DA.SetData(10, qty.ToString());
            DA.SetData(11, tpl.ToJson());
            DA.SetData(12, dash.ToString());
        }

        private static void AddQuad(Mesh mesh, Point3d a, Point3d b, Point3d c, Point3d d, Color col)
        {
            int v = mesh.Vertices.Count;
            mesh.Vertices.Add(a); mesh.Vertices.Add(b); mesh.Vertices.Add(c); mesh.Vertices.Add(d);
            for (int k = 0; k < 4; k++) mesh.VertexColors.Add(col);
            mesh.Faces.AddFace(v, v + 1, v + 2, v + 3);
        }

        private static void Add(SortedDictionary<string, double> d, string k, double v) { d.TryGetValue(k, out double c); d[k] = c + v; }
        private static JObject M(string name, string value) => new JObject { ["Name"] = name, ["Value"] = value };
        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        protected override Bitmap Icon => Enzyme.IconLoader.Load("RoadCorridor.png");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("C4E1A9B7-3F2D-4C6E-8A5B-9D0E1F2A3B4C");
    }
}
