using Enzyme.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Enzyme.Components
{
    /// <summary>
    /// Solves the topology of a set of road centrelines (split at every crossing, endpoints snapped,
    /// nodes and arms with metadata) and draws the 2D road plan: carriageway, kerb, cycle track,
    /// footway and verge lines with kerb returns at the junctions, mouth lines and hatchable areas.
    /// Road type, hierarchy and name come from User Text on the referenced Rhino curves
    /// (road_template, road_hierarchy, road_name), optional override lists, or the defaults.
    /// The Edge / Node JSON drive Road Corridor (trimmed edges) and later the 3D junctions.
    /// </summary>
    public class RoadNetworkComponent : EnzymeComponent
    {
        protected override bool UsesRunGate => true;

        public RoadNetworkComponent()
          : base("Road Network", "RoadNet",
              "Solves the road network topology (junctions, arms, priority, corner radii, mouths) from centrelines and draws the 2D road plan with kerb returns. Reads road_template / road_hierarchy / road_name User Text from the referenced Rhino curves.",
              "Enzyme", "Masterplan")
        {
        }

        public const string KeyTemplate = "road_template", KeyHierarchy = "road_hierarchy", KeyName = "road_name";

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Centrelines", "C", "Road centrelines. Reference them from Rhino to use their User Text (road_template, road_hierarchy, road_name).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Snap Tolerance", "Tol", "Ends closer than this (m) to another centreline or end are joined.", GH_ParamAccess.item, 1.0);
            pManager.AddTextParameter("Default Template", "DefTpl", "Template for centrelines without one (preset name or JSON).", GH_ParamAccess.item, "residential street");
            pManager.AddTextParameter("Default Hierarchy", "DefH", "Hierarchy for centrelines without one: main, secondary or local.", GH_ParamAccess.item, "local");
            pManager.AddNumberParameter("Corner Radii", "Radii", "Kerb return radius (m) by the lower hierarchy of the two roads at a corner: local, secondary, main.", GH_ParamAccess.list, new List<double> { 6, 9, 12 });
            pManager.AddTextParameter("Custom Templates", "Custom", "Your own templates as JSON, each with a \"name\" that road_template can refer to.", GH_ParamAccess.list);
            pManager.AddTextParameter("Templates", "Tpl", "Optional override per centreline (same order), for centrelines made in Grasshopper that cannot carry User Text. Empty items fall through.", GH_ParamAccess.list);
            pManager.AddTextParameter("Hierarchies", "H", "Optional override per centreline (same order).", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "N", "Optional override per centreline (same order).", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "Run", "False = keep showing the last results (cached).", GH_ParamAccess.item, true);
            for (int i = 1; i <= 9; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Nodes", "Nodes", "Junctions, dead ends and joins.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Edges", "Edges", "Centrelines split at every junction (original 3D curves).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Corridor Edges", "CorrEdges", "Edges trimmed between the junction mouths: feed Auto-Grade Road / Road Corridor so corridors stop where the junction starts.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Kerb Lines", "Kerbs", "2D kerb face and kerb back lines, with kerb returns at the corners.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Edge Lines", "Lines", "2D edges of the other parts (cycle tracks, footways, verges, shoulders) and the road boundary, with returns at the corners where they match.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Mouth Lines", "Mouths", "2D lines across each road where its junction ends.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Junction Areas", "JArea", "2D carriageway outline of each junction (closed).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Road Areas", "RArea", "2D closed strips per edge for hatching: carriageway and each part between the mouths.", GH_ParamAccess.list);
            pManager.AddTextParameter("Area Types", "AType", "Part type of each Road Area (carriageway, kerb, cycle, footway, verge ...).", GH_ParamAccess.list);
            pManager.AddPointParameter("Label Points", "LblPt", "Where to place labels (edge middles, nodes).", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Lbl", "Label text for each label point.", GH_ParamAccess.list);
            pManager.AddTextParameter("Node JSON", "NodeJson", "Per node: type, arms (heading, widths, priority, mouth), corners (angle, radii, tangent points).", GH_ParamAccess.list);
            pManager.AddTextParameter("Edge JSON", "EdgeJson", "Per edge: name, template, hierarchy, where they came from, nodes, length, corridor start / end stations.", GH_ParamAccess.list);
            pManager.AddTextParameter("Network JSON", "NetJson", "The whole graph in one JSON.", GH_ParamAccess.item);
            pManager.AddTextParameter("Attributes Used", "Attr", "Per edge: template | hierarchy | name | source (user text, list or default). Check for untagged roads.", GH_ParamAccess.list);
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON metrics for Analysis Dashboard / Report Metrics.", GH_ParamAccess.item);
        }

        protected override void Solve(IGH_DataAccess DA, System.Diagnostics.Stopwatch sw)
        {
            var goos = new List<GH_Curve>();
            if (!DA.GetDataList(0, goos) || goos.Count == 0) return;
            double tol = 1.0; string defTpl = "residential street", defH = "local";
            var radii = new List<double>(); var custom = new List<string>();
            var oTpl = new List<string>(); var oH = new List<string>(); var oN = new List<string>();
            DA.GetData(1, ref tol); DA.GetData(2, ref defTpl); DA.GetData(3, ref defH);
            DA.GetDataList(4, radii); DA.GetDataList(5, custom);
            DA.GetDataList(6, oTpl); DA.GetDataList(7, oH); DA.GetDataList(8, oN);
            tol = Math.Max(0.001, tol);
            if (radii.Count == 0) radii = new List<double> { 6, 9, 12 };
            while (radii.Count < 3) radii.Add(radii[radii.Count - 1]);
            double[] radiusByRank = { radii[0], radii[0], radii[1], radii[2] };

            // ---- templates
            var library = new Dictionary<string, RoadTemplate>(StringComparer.OrdinalIgnoreCase);
            foreach (string js in custom)
            {
                if (string.IsNullOrWhiteSpace(js)) continue;
                var t = RoadTemplate.Parse(js);
                library[t.Name] = t;
            }
            Func<string, RoadTemplate> resolve = tn =>
            {
                if (library.TryGetValue(tn.Trim(), out var t)) return t;
                return RoadTemplate.Parse(tn);
            };

            // ---- read curves and attributes
            var doc = Rhino.RhinoDoc.ActiveDoc;
            int n = goos.Count;
            var orig = new Curve[n]; var flat = new Curve[n];
            var tplName = new string[n]; var hier = new string[n]; var name = new string[n]; var src = new string[n];
            var project = Transform.PlanarProjection(Plane.WorldXY);
            int untagged = 0;
            for (int i = 0; i < n; i++)
            {
                orig[i] = goos[i]?.Value?.DuplicateCurve();
                if (orig[i] == null) continue;
                flat[i] = orig[i].DuplicateCurve();
                flat[i].Transform(project);

                string ut = null, uh = null, un = null;
                var obj = (goos[i].IsReferencedGeometry && doc != null) ? doc.Objects.FindId(goos[i].ReferenceID) : null;
                if (obj != null)
                {
                    ut = Get(obj.Attributes, KeyTemplate, "template"); uh = Get(obj.Attributes, KeyHierarchy, "hierarchy"); un = Get(obj.Attributes, KeyName, "name");
                }
                ut = ut ?? GetGeo(goos[i].Value, KeyTemplate); uh = uh ?? GetGeo(goos[i].Value, KeyHierarchy); un = un ?? GetGeo(goos[i].Value, KeyName);

                string lt = i < oTpl.Count ? oTpl[i] : null, lh = i < oH.Count ? oH[i] : null, ln = i < oN.Count ? oN[i] : null;
                tplName[i] = Pick(lt, ut, defTpl, out string s1);
                hier[i] = Pick(lh, uh, defH, out string s2).ToLowerInvariant();
                name[i] = Pick(ln, un, "", out _);
                if (string.IsNullOrEmpty(name[i])) name[i] = "Road " + (i + 1);
                src[i] = s1 == s2 ? s1 : s1 + " / " + s2;
                if (s1 == "default" || s2 == "default") untagged++;
                if (RoadNetworkGraph.RankOf(hier[i]) == 0)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Centreline {i} hierarchy '{hier[i]}' is not main / secondary / local; treated as local.");
            }

            // ---- split at crossings and snapped ends
            var cuts = new List<double>[n];
            for (int i = 0; i < n; i++) cuts[i] = new List<double>();
            double dt = doc != null ? doc.ModelAbsoluteTolerance : 0.001;
            int overlaps = 0;
            var boxes = flat.Select(c => c == null ? BoundingBox.Empty : c.GetBoundingBox(false)).ToArray();
            for (int i = 0; i < n; i++)
            {
                if (flat[i] == null) continue;
                var self = Intersection.CurveSelf(flat[i], dt);
                if (self != null) foreach (var ev in self) if (ev.IsPoint) { cuts[i].Add(ev.ParameterA); cuts[i].Add(ev.ParameterB); }
                for (int j = i + 1; j < n; j++)
                {
                    if (flat[j] == null) continue;
                    var bi = boxes[i]; bi.Inflate(tol);
                    if (!Overlap(bi, boxes[j])) continue;
                    var evs = Intersection.CurveCurve(flat[i], flat[j], dt, dt);
                    if (evs == null) continue;
                    foreach (var ev in evs)
                    {
                        if (ev.IsOverlap) { overlaps++; continue; }
                        cuts[i].Add(ev.ParameterA); cuts[j].Add(ev.ParameterB);
                    }
                }
            }
            // ends that stop short of (or overshoot onto) another centreline
            for (int i = 0; i < n; i++)
            {
                if (flat[i] == null || flat[i].IsClosed) continue;
                foreach (var p in new[] { flat[i].PointAtStart, flat[i].PointAtEnd })
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i || flat[j] == null) continue;
                        if (flat[j].ClosestPoint(p, out double t, tol)) cuts[j].Add(t);
                    }
            }
            if (overlaps > 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"{overlaps} overlapping centreline stretch(es) ignored: centrelines should not run on top of each other.");

            var edges = new List<NetEdgeInput>();
            var edgeOrig = new List<Curve>(); var edgeFlat = new List<Curve>();
            int dropped = 0;
            for (int i = 0; i < n; i++)
            {
                if (flat[i] == null) continue;
                var dom = flat[i].Domain;
                var ps = cuts[i].Where(t => t > dom.T0 + 1e-6 && t < dom.T1 - 1e-6).OrderBy(t => t).ToList();
                var uniq = new List<double>();
                foreach (double t in ps) if (uniq.Count == 0 || flat[i].GetLength(new Interval(uniq[uniq.Count - 1], t)) > 1e-3) uniq.Add(t);
                Curve[] fs = uniq.Count > 0 ? flat[i].Split(uniq) : new[] { flat[i] };
                Curve[] os = uniq.Count > 0 ? orig[i].Split(uniq) : new[] { orig[i] };
                if (fs == null || os == null || fs.Length != os.Length) { fs = new[] { flat[i] }; os = new[] { orig[i] }; }
                RoadTemplate tpl = resolve(tplName[i]);
                for (int k = 0; k < fs.Length; k++)
                {
                    double len = fs[k].GetLength();
                    if (len < tol) { dropped++; continue; }
                    var a = fs[k].TangentAtStart; var b = fs[k].TangentAtEnd;
                    edges.Add(new NetEdgeInput
                    {
                        Start = new V2(fs[k].PointAtStart.X, fs[k].PointAtStart.Y), End = new V2(fs[k].PointAtEnd.X, fs[k].PointAtEnd.Y),
                        StartDir = new V2(a.X, a.Y).Unit(), EndDir = new V2(b.X, b.Y).Unit(), Length = len,
                        Template = tpl, TemplateName = tpl.Name, Hierarchy = hier[i], Name = name[i] + (fs.Length > 1 ? " (" + (k + 1) + ")" : ""),
                        Source = src[i], SourceCurve = i,
                    });
                    edgeFlat.Add(fs[k]); edgeOrig.Add(os[k]);
                }
            }
            if (dropped > 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{dropped} piece(s) shorter than the snap tolerance dropped.");
            if (edges.Count == 0) throw new InvalidOperationException("No road edges.");

            var g = RoadNetworkGraph.Build(edges, tol, radiusByRank);
            foreach (string w in g.Warnings) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, w);

            // ---- trim stations per (arm, side, boundary)
            var trim = new Dictionary<(int arm, char side, int k), (double t, V2? p)>();
            foreach (var c in g.Corners)
                foreach (var l in c.Lines)
                {
                    trim[(c.ArmA, 'L', l.BoundaryA)] = (l.Ta, (V2?)l.TangentA);
                    trim[(c.ArmB, 'R', l.BoundaryB)] = (l.Tb, (V2?)l.TangentB);
                }
            Func<NetArm, char, int, (double t, V2? p)> trimOf = (arm, side, k) =>
            {
                if (trim.TryGetValue((arm.Id, side, k), out var v)) return v;
                return (g.Nodes[arm.Node].Arms.Count >= 3 ? arm.Mouth : 0.0, null);
            };

            var kerbLines = new List<Curve>(); var edgeLines = new List<Curve>(); var mouths = new List<Curve>();
            var areas = new List<Curve>(); var areaTypes = new List<string>();
            var corridorEdges = new List<Curve>();
            for (int e = 0; e < edges.Count; e++)
            {
                var fc = edgeFlat[e]; double L = edges[e].Length;
                var sArm = g.Arms[2 * e]; var eArm = g.Arms[2 * e + 1];
                var tplE = edges[e].Template;

                // corridor edge
                double s0 = g.StartStation(e), s1 = g.EndStation(e);
                if (s1 - s0 > 1e-3)
                {
                    fc.LengthParameter(s0, out double t0); fc.LengthParameter(s1, out double t1);
                    var tr = edgeOrig[e].Trim(t0, t1);
                    if (tr != null) corridorEdges.Add(tr);
                }

                foreach (int sign in new[] { -1, 1 })
                {
                    var parts = sign < 0 ? tplE.Left : tplE.Right;
                    var bnd = RoadNetworkGraph.Boundaries(parts);
                    // start arm looks along the edge: its left = edge left; end arm reversed.
                    char sideAtStart = sign < 0 ? 'L' : 'R', sideAtEnd = sign < 0 ? 'R' : 'L';
                    for (int k = 1; k < bnd.Length; k++)
                    {
                        var a = trimOf(sArm, sideAtStart, k); var b = trimOf(eArm, sideAtEnd, k);
                        double from = a.t, to = L - b.t;
                        if (to - from < 1e-3) continue;
                        var pl = OffsetLine(fc, sign * bnd[k], from, to);
                        if (a.p.HasValue) pl[0] = new Point3d(a.p.Value.X, a.p.Value.Y, 0);
                        if (b.p.HasValue) pl[pl.Count - 1] = new Point3d(b.p.Value.X, b.p.Value.Y, 0);
                        string type = parts[k - 1].Type;
                        bool kerb = type == "kerb" || (k < parts.Count && parts[k].Type == "kerb");
                        (kerb ? kerbLines : edgeLines).Add(pl.ToPolylineCurve());
                    }
                }

                // hatch areas between the mouths
                double m0 = sArm.Mouth, m1 = L - eArm.Mouth;
                if (g.Nodes[sArm.Node].Arms.Count < 3) m0 = 0;
                if (g.Nodes[eArm.Node].Arms.Count < 3) m1 = L;
                if (m1 - m0 > 1e-3)
                {
                    double cl = RoadNetworkGraph.CarriagewayWidth(tplE.Left), cr = RoadNetworkGraph.CarriagewayWidth(tplE.Right);
                    areas.Add(Strip(fc, -cl, cr, m0, m1)); areaTypes.Add("carriageway");
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var parts = sign < 0 ? tplE.Left : tplE.Right;
                        var bnd = RoadNetworkGraph.Boundaries(parts);
                        double cw = sign < 0 ? cl : cr;
                        for (int k = 1; k < bnd.Length; k++)
                        {
                            if (bnd[k] <= cw + 1e-9 || parts[k - 1].Width <= 0) continue;
                            areas.Add(Strip(fc, sign * bnd[k - 1], sign * bnd[k], m0, m1)); areaTypes.Add(parts[k - 1].Type);
                        }
                    }
                }
            }

            // ---- corner returns, straight joins, mouths, junction areas
            foreach (var c in g.Corners)
                foreach (var l in c.Lines)
                {
                    Curve crv = c.Straight
                        ? (Curve)new LineCurve(P3(l.TangentA), P3(l.TangentB))
                        : new ArcCurve(new Arc(P3(l.TangentA), P3(l.Mid), P3(l.TangentB)));
                    if (crv.GetLength() < 1e-3) continue;
                    (l.Role.Contains("kerb") ? kerbLines : edgeLines).Add(crv);
                }
            var junctionAreas = new List<Curve>();
            foreach (var nd in g.Nodes.Where(x => x.Arms.Count >= 3))
            {
                if (nd.CarriagewayOutline.Count > 3) junctionAreas.Add(new PolylineCurve(nd.CarriagewayOutline.Select(P3)));
                foreach (int a in nd.Arms)
                {
                    var arm = g.Arms[a]; var fc = edgeFlat[arm.Edge];
                    double s = arm.AtStart ? arm.Mouth : edges[arm.Edge].Length - arm.Mouth;
                    var tpl = edges[arm.Edge].Template;
                    double wl = tpl.HalfWidth(false), wr = tpl.HalfWidth(true);
                    var pl = new Polyline { At(fc, s, -wl), At(fc, s, wr) };
                    mouths.Add(pl.ToPolylineCurve());
                }
            }

            // ---- labels, JSON, dashboard
            var lblPts = new List<Point3d>(); var lbls = new List<string>();
            for (int e = 0; e < edges.Count; e++)
            {
                lblPts.Add(At(edgeFlat[e], edges[e].Length / 2, 0));
                lbls.Add($"{edges[e].Name}\n{edges[e].TemplateName} · {edges[e].Hierarchy}");
            }
            foreach (var nd in g.Nodes.Where(x => x.Arms.Count != 2))
            {
                lblPts.Add(P3(nd.P));
                lbls.Add($"N{nd.Id} {nd.Type}");
            }
            var nodeJson = g.Nodes.Select(nd => g.NodeJson(nd).ToString(Newtonsoft.Json.Formatting.None)).ToList();
            var edgeJson = Enumerable.Range(0, edges.Count).Select(e => g.EdgeJson(e).ToString(Newtonsoft.Json.Formatting.None)).ToList();
            var attrs = edges.Select(ed => $"{ed.TemplateName} | {ed.Hierarchy} | {ed.Name} | {ed.Source}").ToList();
            if (untagged > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{untagged} centreline(s) use a default template or hierarchy (no road_template / road_hierarchy User Text). See Attributes Used.");

            var inv = CultureInfo.InvariantCulture;
            BoundingBox all = BoundingBox.Empty;
            foreach (var c in edgeFlat) all.Union(c.GetBoundingBox(false));
            double km2 = Math.Max(1e-9, (all.Max.X - all.Min.X) * (all.Max.Y - all.Min.Y) / 1e6);
            int junctions = g.Nodes.Count(x => x.Arms.Count >= 3);
            double netLen = edges.Sum(ed => ed.Length);
            var metrics = new JArray
            {
                M("Network length", (netLen / 1000).ToString("F2", inv) + " km"),
                M("Junctions", junctions.ToString(inv)),
                M("T / Y junctions", g.Nodes.Count(x => x.Type == "T" || x.Type == "Y").ToString(inv)),
                M("Crossroads", g.Nodes.Count(x => x.Type == "crossroads").ToString(inv)),
                M("Multi-arm junctions", g.Nodes.Count(x => x.Type == "multi").ToString(inv)),
                M("Dead ends", g.Nodes.Count(x => x.Type == "dead end").ToString(inv)),
                M("Skewed junctions (< 70°)", g.Nodes.Count(x => x.Arms.Count >= 3 && x.Corners.Min(ci => g.Corners[ci].Angle) < 70).ToString(inv)),
                M("Junction density (bounding box)", (junctions / km2).ToString("F0", inv) + " /km²"),
                M("Untagged centrelines", untagged.ToString(inv)),
            };
            var dash = new JObject { ["Type"] = "Metrics", ["Title"] = "Road Network", ["Metrics"] = metrics };

            Hud.Done(this, sw,
                Hud.Metric("Edges", edges.Count),
                Hud.Metric("Junctions", junctions),
                Hud.Metric("Dead ends", g.Nodes.Count(x => x.Type == "dead end")),
                Hud.Metric("Untagged", untagged));

            DA.SetDataList(0, g.Nodes.Select(nd => P3(nd.P)));
            DA.SetDataList(1, edgeOrig);
            DA.SetDataList(2, corridorEdges);
            DA.SetDataList(3, kerbLines);
            DA.SetDataList(4, edgeLines);
            DA.SetDataList(5, mouths);
            DA.SetDataList(6, junctionAreas);
            DA.SetDataList(7, areas);
            DA.SetDataList(8, areaTypes);
            DA.SetDataList(9, lblPts);
            DA.SetDataList(10, lbls);
            DA.SetDataList(11, nodeJson);
            DA.SetDataList(12, edgeJson);
            DA.SetData(13, g.NetworkJson().ToString());
            DA.SetDataList(14, attrs);
            DA.SetData(15, dash.ToString());
        }

        // ---------------- helpers

        private static string Get(Rhino.DocObjects.ObjectAttributes a, params string[] keys)
        {
            foreach (var k in keys) { string v = a.GetUserString(k); if (!string.IsNullOrWhiteSpace(v)) return v.Trim(); }
            return null;
        }
        private static string GetGeo(GeometryBase g, string key)
        {
            string v = g?.GetUserString(key);
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }
        private static string Pick(string list, string userText, string def, out string source)
        {
            if (!string.IsNullOrWhiteSpace(list)) { source = "list"; return list.Trim(); }
            if (!string.IsNullOrWhiteSpace(userText)) { source = "user text"; return userText; }
            source = "default"; return def ?? "";
        }
        private static bool Overlap(BoundingBox a, BoundingBox b)
            => a.Min.X <= b.Max.X && a.Max.X >= b.Min.X && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y;
        private static Point3d P3(V2 v) => new Point3d(v.X, v.Y, 0);
        private static JObject M(string name, string value) => new JObject { ["Name"] = name, ["Value"] = value };

        /// <summary>Plan point at a distance along a flat curve and an offset (right +).</summary>
        private static Point3d At(Curve c, double s, double o)
        {
            c.LengthParameter(Math.Max(0, Math.Min(c.GetLength(), s)), out double t);
            var p = c.PointAt(t); var tg = c.TangentAt(t); tg.Z = 0; tg.Unitize();
            return new Point3d(p.X + tg.Y * o, p.Y - tg.X * o, 0);
        }

        private static Polyline OffsetLine(Curve c, double o, double from, double to)
        {
            int steps = Math.Max(1, (int)Math.Ceiling((to - from) / 1.0));
            var pl = new Polyline();
            for (int i = 0; i <= steps; i++) pl.Add(At(c, from + (to - from) * i / steps, o));
            return pl;
        }

        private static Curve Strip(Curve c, double oA, double oB, double from, double to)
        {
            var a = OffsetLine(c, oA, from, to); var b = OffsetLine(c, oB, from, to);
            b.Reverse();
            var ring = new Polyline(a); ring.AddRange(b); ring.Add(a[0]);
            return ring.ToPolylineCurve();
        }

        protected override Bitmap Icon => Enzyme.IconLoader.Load("RoadNetwork.png");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("5D8B2C6E-1A4F-4E3B-9C7D-0F2E3A4B5C6D");
    }
}
