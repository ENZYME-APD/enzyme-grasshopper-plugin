using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Enzyme.Utils
{
    /// <summary>2D vector / point (plan).</summary>
    public struct V2
    {
        public double X, Y;
        public V2(double x, double y) { X = x; Y = y; }
        public static V2 operator +(V2 a, V2 b) => new V2(a.X + b.X, a.Y + b.Y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.X - b.X, a.Y - b.Y);
        public static V2 operator *(V2 a, double s) => new V2(a.X * s, a.Y * s);
        public double Length => Math.Sqrt(X * X + Y * Y);
        public V2 Unit() { double l = Length; return l > 1e-12 ? new V2(X / l, Y / l) : new V2(1, 0); }
        public V2 Left => new V2(-Y, X);
        public V2 Right => new V2(Y, -X);
        public double Dot(V2 o) => X * o.X + Y * o.Y;
        public double Angle => Math.Atan2(Y, X);
        public override string ToString() => X.ToString("F3", CultureInfo.InvariantCulture) + "," + Y.ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>A road segment between two junctions, as the topology sees it (already split).</summary>
    public sealed class NetEdgeInput
    {
        public V2 Start, End;
        public V2 StartDir, EndDir;      // unit tangents along the edge direction, at its start and end
        public double Length;
        public RoadTemplate Template;
        public string TemplateName = "", Hierarchy = "local", Name = "", Source = "default";
        public int SourceCurve = -1;
    }

    public sealed class NetArm
    {
        public int Id, Node, Edge;
        public bool AtStart;            // the edge starts at this node (arm direction = edge direction)
        public V2 Dir;                  // unit, pointing away from the node
        public double Heading;          // degrees, CCW from +X
        /// <summary>Template side on the arm's left / right looking away from the node.</summary>
        public List<TemplatePart> LeftParts, RightParts;
        public double Mouth;            // distance from the node to where the junction ends and the corridor starts
        public int Rank;
        public bool Priority;           // runs through the junction
    }

    public sealed class CornerLine
    {
        public string Role;             // "kerb face", "kerb back", "boundary", or "edge 2" ... when templates match
        public int BoundaryA, BoundaryB; // index of the boundary on each arm side (1 = first part edge)
        public double Wa, Wb, Radius;   // offsets on arm A (its left) and arm B (its right), and fillet radius
        public double Ta, Tb;           // distance from the node along each arm to the tangent point
        public V2 TangentA, TangentB, Centre, Mid;
        public bool Straight;           // no arc: the edges run straight on (or meet at a convex angle)
    }

    public sealed class NetCorner
    {
        public int Node, ArmA, ArmB;    // ArmB is the next arm counter-clockwise from ArmA
        public double Angle;            // degrees, CCW from A to B
        public double Radius;           // kerb-face radius
        public bool Straight;
        public List<CornerLine> Lines = new List<CornerLine>();
    }

    /// <summary>A junction tag: a point with User Text near a node.</summary>
    public sealed class JunctionTag
    {
        public V2 P;
        public string Type = "auto";    // auto, priority, signals, roundabout, mini-roundabout, cul-de-sac
        public double Radius = double.NaN; // kerb radius (priority junctions) or outer radius (roundabouts, cul-de-sacs)
        public string Priority = "";    // road_name of the road that runs through
        public string Name = "";
    }

    /// <summary>A circular arc in plan through three points.</summary>
    public struct ArcSpec
    {
        public V2 A, M, B;
        public string Role;             // "kerb face", "boundary", "island"
    }

    public sealed class NetNode
    {
        public int Id;
        public V2 P;
        public string Type;             // dead end, continuation, T, Y, crossroads, multi
        public List<int> Arms = new List<int>();     // counter-clockwise
        public List<int> Corners = new List<int>();
        public int[] Through;           // the two priority arms, or null
        public List<V2> CarriagewayOutline = new List<V2>(); // junction paved area (deg >= 3)
        public string Control = "auto"; // from a junction tag
        public string TagName = "";
        public bool Tagged;
        public double RingRadius;       // roundabout / cul-de-sac outer kerb radius (0 = none)
        public double IslandRadius;     // roundabout central island (0 = none)
        public List<ArcSpec> RingArcs = new List<ArcSpec>();
        public List<V2> IslandOutline = new List<V2>();
        public bool IsRing => RingRadius > 0;
        public string Priority = "";
        public double TagRadius = double.NaN;
    }

    public sealed class RoadNetworkGraph
    {
        public List<NetEdgeInput> Edges = new List<NetEdgeInput>();
        public List<NetNode> Nodes = new List<NetNode>();
        public List<NetArm> Arms = new List<NetArm>();
        public List<NetCorner> Corners = new List<NetCorner>();
        public int[] EdgeStartNode, EdgeEndNode;
        public List<string> Warnings = new List<string>();
        /// <summary>Where each part-edge line of an arm stops: (arm, 'L'/'R' looking away from the node, boundary) -> (distance from node, exact end point).</summary>
        public Dictionary<(int arm, char side, int k), (double t, V2 p)> LineTrims = new Dictionary<(int, char, int), (double, V2)>();

        public static readonly string[] Controls = { "auto", "priority", "signals", "roundabout", "mini-roundabout", "cul-de-sac" };
        public static double DefaultRingRadius(string control) => control == "roundabout" ? 15.0 : control == "mini-roundabout" ? 8.0 : control == "cul-de-sac" ? 9.0 : 0;
        public static double DefaultIslandRadius(string control, double outer) => control == "roundabout" ? Math.Max(1.0, outer - 5.5) : control == "mini-roundabout" ? 2.0 : 0;

        public static readonly string[] Hierarchies = { "main", "secondary", "local" };
        public static int RankOf(string h)
        {
            switch ((h ?? "").Trim().ToLowerInvariant())
            {
                case "main": case "primary": case "arterial": return 3;
                case "secondary": case "collector": case "distributor": return 2;
                case "local": case "access": return 1;
                default: return 0;
            }
        }


        /// <summary>Width from the axis to the kerb face (edge of the carriageway) on one side.</summary>
        public static double CarriagewayWidth(List<TemplatePart> parts)
        {
            double w = 0;
            foreach (var p in parts)
            {
                if (!(p.Type == "lane" || p.Type == "parking" || p.Type == "shoulder" || p.Type == "track" || p.Type == "median")) break;
                w += p.Width;
            }
            // A path-only template (cycle path): its first part is the carriageway.
            if (w == 0 && parts.Count > 0) w = parts[0].Width;
            return w;
        }

        /// <summary>Offsets of each part boundary (1..n) on one side.</summary>
        public static double[] Boundaries(List<TemplatePart> parts)
        {
            var b = new double[parts.Count + 1];
            for (int i = 0; i < parts.Count; i++) b[i + 1] = b[i] + parts[i].Width;
            return b;
        }

        /// <param name="radiusByRank">Corner radius for rank 1 (local), 2 (secondary), 3 (main); index 0 for unranked.</param>
        public static RoadNetworkGraph Build(List<NetEdgeInput> edges, double tol, double[] radiusByRank, double mouthExtra = 1.0, double straightAngle = 170.0, List<JunctionTag> tags = null, double tagReach = 5.0)
        {
            var g = new RoadNetworkGraph { Edges = edges };
            int n = edges.Count;
            g.EdgeStartNode = new int[n];
            g.EdgeEndNode = new int[n];

            // ---- nodes: cluster edge ends within tol
            var pts = new List<V2>();
            var counts = new List<int>();
            Func<V2, int> nodeOf = p =>
            {
                for (int i = 0; i < pts.Count; i++)
                    if ((pts[i] * (1.0 / counts[i]) - p).Length <= tol) { pts[i] = pts[i] + p; counts[i]++; return i; }
                pts.Add(p); counts.Add(1); return pts.Count - 1;
            };
            for (int e = 0; e < n; e++) { g.EdgeStartNode[e] = nodeOf(edges[e].Start); g.EdgeEndNode[e] = nodeOf(edges[e].End); }
            for (int i = 0; i < pts.Count; i++) g.Nodes.Add(new NetNode { Id = i, P = pts[i] * (1.0 / counts[i]) });

            // ---- arms
            for (int e = 0; e < n; e++)
            {
                var ed = edges[e];
                var t = ed.Template;
                foreach (bool atStart in new[] { true, false })
                {
                    var arm = new NetArm
                    {
                        Id = g.Arms.Count, Edge = e, AtStart = atStart,
                        Node = atStart ? g.EdgeStartNode[e] : g.EdgeEndNode[e],
                        Dir = atStart ? ed.StartDir.Unit() : (ed.EndDir * -1).Unit(),
                        Rank = RankOf(ed.Hierarchy),
                    };
                    arm.Heading = Norm360(arm.Dir.Angle * 180 / Math.PI);
                    // Looking away from the node: at the start that is the edge direction (left = template Left);
                    // at the end it is reversed (left = template Right).
                    arm.LeftParts = atStart ? t.Left : t.Right;
                    arm.RightParts = atStart ? t.Right : t.Left;
                    g.Arms.Add(arm);
                    g.Nodes[arm.Node].Arms.Add(arm.Id);
                }
            }

            // ---- junction tags: nearest node within reach
            if (tags != null)
                foreach (var tag in tags)
                {
                    NetNode best = null; double bd = double.MaxValue;
                    foreach (var nd in g.Nodes) { double d = (nd.P - tag.P).Length; if (d < bd) { bd = d; best = nd; } }
                    if (best == null || bd > Math.Max(tagReach, tol)) { g.Warnings.Add($"Junction tag '{tag.Name}' {tag.Type} at {tag.P} is not within {Math.Max(tagReach, tol):0.#} m of a node; ignored."); continue; }
                    string ctl = (tag.Type ?? "auto").Trim().ToLowerInvariant().Replace(" ", "-");
                    if (ctl == "turning-head" || ctl == "turning-circle") ctl = "cul-de-sac";
                    if (ctl == "mini") ctl = "mini-roundabout";
                    if (Array.IndexOf(Controls, ctl) < 0) { g.Warnings.Add($"Junction tag type '{tag.Type}' unknown (use {string.Join(", ", Controls)}); treated as auto."); ctl = "auto"; }
                    if (best.Tagged) g.Warnings.Add($"Node {best.Id} has more than one junction tag; the last one wins.");
                    best.Tagged = true; best.Control = ctl; best.TagName = tag.Name ?? "";
                    best.Priority = tag.Priority ?? ""; best.TagRadius = tag.Radius;
                }

            foreach (var node in g.Nodes)
            {
                node.Arms.Sort((a, b) => g.Arms[a].Heading.CompareTo(g.Arms[b].Heading));
                int deg = node.Arms.Count;
                node.Type = deg == 1 ? "dead end" : deg == 2 ? "continuation" : deg == 3 ? "T" : deg == 4 ? "crossroads" : "multi";

                bool ring = node.Control == "roundabout" || node.Control == "mini-roundabout" || (node.Control == "cul-de-sac" && deg == 1);
                if (node.Control == "cul-de-sac" && deg != 1) g.Warnings.Add($"Node {node.Id} is tagged cul-de-sac but is not a dead end; ignored.");
                if (ring)
                {
                    if (node.Control != "cul-de-sac") node.Type = "roundabout";
                    node.RingRadius = !double.IsNaN(node.TagRadius) && node.TagRadius > 0 ? node.TagRadius : DefaultRingRadius(node.Control);
                    node.IslandRadius = DefaultIslandRadius(node.Control, node.RingRadius);
                    continue;
                }

                if (deg >= 3)
                {
                    // Priority: highest combined rank, then straightest pair.
                    double best = double.MinValue; int[] pair = null;
                    for (int i = 0; i < deg; i++)
                        for (int j = i + 1; j < deg; j++)
                        {
                            var a = g.Arms[node.Arms[i]]; var b = g.Arms[node.Arms[j]];
                            double straight = -a.Dir.Dot(b.Dir); // 1 = straight on
                            double score = (a.Rank + b.Rank) * 10 + straight;
                            if (score > best) { best = score; pair = new[] { a.Id, b.Id }; }
                        }
                    if (!string.IsNullOrWhiteSpace(node.Priority))
                    {
                        var named = node.Arms.Where(id => BaseName(g.Edges[g.Arms[id].Edge].Name).Equals(node.Priority.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                        if (named.Count >= 2) pair = new[] { named[0], named[1] };
                        else g.Warnings.Add($"Node {node.Id}: priority road '{node.Priority}' does not have two arms here; automatic priority kept.");
                    }
                    node.Through = pair;
                    foreach (int id in pair) g.Arms[id].Priority = true;
                    double straightness = -g.Arms[pair[0]].Dir.Dot(g.Arms[pair[1]].Dir);
                    if (deg == 3 && straightness < Math.Cos(35 * Math.PI / 180)) node.Type = "Y";

                    for (int i = 0; i < deg; i++)
                    {
                        var A = g.Arms[node.Arms[i]]; var B = g.Arms[node.Arms[(i + 1) % deg]];
                        double ang = Norm360(B.Heading - A.Heading);
                        if (deg > 1 && ang == 0) ang = 360;
                        int minRank = Math.Min(A.Rank, B.Rank);
                        double r = radiusByRank[Math.Max(0, Math.Min(radiusByRank.Length - 1, minRank))];
                        if (!double.IsNaN(node.TagRadius) && node.TagRadius > 0) r = node.TagRadius;
                        var c = new NetCorner { Node = node.Id, ArmA = A.Id, ArmB = B.Id, Angle = ang, Radius = r, Straight = ang >= straightAngle };
                        c.Lines = CornerLines(node.P, A, B, ang, r, c.Straight);
                        node.Corners.Add(g.Corners.Count);
                        g.Corners.Add(c);
                    }
                }
            }

            // ---- roundabouts and cul-de-sacs
            foreach (var node in g.Nodes.Where(nd => nd.IsRing)) Ring(g, node, radiusByRank, mouthExtra);

            // ---- mouths: past the farthest kerb-face tangent point of the arm's two corners
            foreach (var c in g.Corners)
            {
                if (c.Straight || c.Lines.Count == 0) continue;
                var kf = c.Lines[0];
                g.Arms[c.ArmA].Mouth = Math.Max(g.Arms[c.ArmA].Mouth, kf.Ta + mouthExtra);
                g.Arms[c.ArmB].Mouth = Math.Max(g.Arms[c.ArmB].Mouth, kf.Tb + mouthExtra);
                foreach (var l in c.Lines)
                {
                    g.Arms[c.ArmA].Mouth = Math.Max(g.Arms[c.ArmA].Mouth, l.Ta);
                    g.Arms[c.ArmB].Mouth = Math.Max(g.Arms[c.ArmB].Mouth, l.Tb);
                }
            }
            // Arms of a junction with only straight corners on one side still need a mouth (e.g. the through road of a T).
            foreach (var node in g.Nodes.Where(nd => nd.Arms.Count >= 3 && !nd.IsRing))
            {
                double m = node.Arms.Max(a => g.Arms[a].Mouth);
                foreach (int a in node.Arms)
                    if (g.Arms[a].Mouth <= 0) g.Arms[a].Mouth = m;
            }
            for (int e = 0; e < n; e++)
            {
                var sa = g.Arms[2 * e]; var ea = g.Arms[2 * e + 1];
                if (sa.Mouth + ea.Mouth > edges[e].Length)
                {
                    g.Warnings.Add($"Edge {e} ({edges[e].Name}) is too short for its junctions ({edges[e].Length:F1} m < {sa.Mouth + ea.Mouth:F1} m); mouths shortened.");
                    double f = edges[e].Length / (sa.Mouth + ea.Mouth) * 0.98;
                    sa.Mouth *= f; ea.Mouth *= f;
                }
            }

            // ---- straight corners: tangent points at the arms' mouths
            foreach (var c in g.Corners.Where(cc => cc.Straight))
            {
                var A = g.Arms[c.ArmA]; var B = g.Arms[c.ArmB];
                foreach (var l in c.Lines)
                {
                    l.Ta = A.Mouth; l.Tb = B.Mouth;
                    l.TangentA = g.Nodes[c.Node].P + A.Dir * A.Mouth + A.Dir.Left * l.Wa;
                    l.TangentB = g.Nodes[c.Node].P + B.Dir * B.Mouth + B.Dir.Right * l.Wb;
                    l.Mid = (l.TangentA + l.TangentB) * 0.5; l.Centre = l.Mid;
                }
            }

            // ---- junction carriageway outline
            // ---- line trims (after mouth shortening the straight ones are final)
            foreach (var c in g.Corners)
                foreach (var l in c.Lines)
                {
                    g.LineTrims[(c.ArmA, 'L', l.BoundaryA)] = (l.Ta, l.TangentA);
                    g.LineTrims[(c.ArmB, 'R', l.BoundaryB)] = (l.Tb, l.TangentB);
                }

            foreach (var node in g.Nodes.Where(nd => nd.Arms.Count >= 3 && !nd.IsRing))
            {
                var outline = node.CarriagewayOutline;
                for (int k = 0; k < node.Corners.Count; k++)
                {
                    var c = g.Corners[node.Corners[k]];
                    var A = g.Arms[c.ArmA]; var B = g.Arms[c.ArmB];
                    var kf = c.Lines[0];
                    // mouth of A, left kerb face
                    outline.Add(node.P + A.Dir * A.Mouth + A.Dir.Left * kf.Wa);
                    if (!c.Straight)
                    {
                        outline.Add(kf.TangentA);
                        outline.AddRange(ArcPoints(kf.Centre, kf.TangentA, kf.TangentB, 12).Skip(1).Take(11));
                        outline.Add(kf.TangentB);
                    }
                    outline.Add(node.P + B.Dir * B.Mouth + B.Dir.Right * kf.Wb);
                }
                if (outline.Count > 0) outline.Add(outline[0]);
            }
            return g;
        }

        private static List<CornerLine> CornerLines(V2 node, NetArm A, NetArm B, double angDeg, double r, bool straight)
        {
            var lines = new List<CornerLine>();
            var pa = A.LeftParts; var pb = B.RightParts;
            double ca = CarriagewayWidth(pa), cb = CarriagewayWidth(pb);
            var ba = Boundaries(pa); var bb = Boundaries(pb);
            int ia = Array.FindIndex(ba, v => Math.Abs(v - ca) < 1e-9), ib = Array.FindIndex(bb, v => Math.Abs(v - cb) < 1e-9);
            if (ia < 0) ia = 1; if (ib < 0) ib = 1;
            var pairs = new List<(string role, int a, int b)> { ("kerb face", ia, ib) };

            bool same = pa.Count == pb.Count && pa.Zip(pb, (x, y) => x.Type == y.Type && Math.Abs(x.Width - y.Width) < 1e-9).All(v => v);
            if (same)
            {
                for (int k = 1; k < ba.Length; k++) if (k != ia) pairs.Add((k == ba.Length - 1 ? "boundary" : pa[k - 1].Type + " edge", k, k));
            }
            else
            {
                bool kerbA = ia < pa.Count && pa[ia].Type == "kerb", kerbB = ib < pb.Count && pb[ib].Type == "kerb";
                if (kerbA && kerbB) pairs.Add(("kerb back", ia + 1, ib + 1));
                if (ba.Length - 1 > ia && bb.Length - 1 > ib && !(kerbA && kerbB && ba.Length - 1 == ia + 1 && bb.Length - 1 == ib + 1))
                    pairs.Add(("boundary", ba.Length - 1, bb.Length - 1));
            }

            double th = angDeg * Math.PI / 180.0;
            foreach (var (role, ka, kb) in pairs)
            {
                double wa = ba[ka], wb = bb[kb];
                var l = new CornerLine { Role = role, BoundaryA = ka, BoundaryB = kb, Wa = wa, Wb = wb, Straight = straight };
                if (!straight)
                {
                    double delta = 0.5 * ((wa - ca) + (wb - cb));
                    l.Radius = Math.Max(0.5, r - delta);
                    double s = Math.Sin(th), tanHalf = Math.Tan(th / 2);
                    double xa = (wb + wa * Math.Cos(th)) / s;
                    double xb = (wa + wb * Math.Cos(th)) / s;
                    l.Ta = xa + l.Radius / tanHalf;
                    l.Tb = xb + l.Radius / tanHalf;
                    l.TangentA = node + A.Dir * l.Ta + A.Dir.Left * wa;
                    l.TangentB = node + B.Dir * l.Tb + B.Dir.Right * wb;
                    l.Centre = node + A.Dir * l.Ta + A.Dir.Left * (wa + l.Radius);
                    var mid = (l.TangentA + l.TangentB) * 0.5 - l.Centre;
                    l.Mid = l.Centre + mid.Unit() * l.Radius;
                }
                lines.Add(l);
            }
            return lines;
        }

        /// <summary>Points along the short arc from a to b around centre (inclusive).</summary>
        public static List<V2> ArcPoints(V2 centre, V2 a, V2 b, int segments)
        {
            double r = (a - centre).Length;
            double a0 = (a - centre).Angle, a1 = (b - centre).Angle;
            double d = a1 - a0;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d < -Math.PI) d += 2 * Math.PI;
            var list = new List<V2>();
            for (int i = 0; i <= segments; i++)
            {
                double t = a0 + d * i / segments;
                list.Add(centre + new V2(Math.Cos(t), Math.Sin(t)) * r);
            }
            return list;
        }

        public static string BaseName(string edgeName)
        {
            string s = edgeName ?? "";
            int k = s.LastIndexOf(" (", StringComparison.Ordinal);
            return k > 0 && s.EndsWith(")") ? s.Substring(0, k) : s;
        }

        /// <summary>
        /// Roundabout / cul-de-sac: an outer kerb circle around the node, each arm's kerb face and
        /// boundary joined to it with a fillet (concentric for the two lines), circle arcs between
        /// the arms, and a central island for roundabouts.
        /// </summary>
        private static void Ring(RoadNetworkGraph g, NetNode node, double[] radiusByRank, double mouthExtra)
        {
            double R = node.RingRadius;
            int deg = node.Arms.Count;
            var circleT = new Dictionary<(int arm, char side, string role), V2>();
            var filletKF = new Dictionary<(int arm, char side), ArcSpec>();
            foreach (int a in node.Arms)
            {
                var arm = g.Arms[a];
                double r = radiusByRank[Math.Max(0, Math.Min(radiusByRank.Length - 1, arm.Rank))];
                if (node.Control == "roundabout") r = Math.Max(r, 10.0);
                foreach (char side in new[] { 'L', 'R' })
                {
                    var parts = side == 'L' ? arm.LeftParts : arm.RightParts;
                    var n = side == 'L' ? arm.Dir.Left : arm.Dir.Right;
                    double c = CarriagewayWidth(parts);
                    var bnd = Boundaries(parts);
                    int kc = Array.FindIndex(bnd, v => Math.Abs(v - c) < 1e-9); if (kc < 0) kc = 1;
                    var roles = new List<(string role, int k)> { ("kerb face", kc) };
                    if (bnd.Length - 1 > kc) roles.Add(("boundary", bnd.Length - 1));
                    foreach (var (role, k) in roles)
                    {
                        double w = bnd[k], delta = w - c;
                        double Rk = R + delta, rk = Math.Max(0.5, r - delta);
                        double h = (Rk + rk) * (Rk + rk) - (w + rk) * (w + rk);
                        if (h <= 0)
                        {
                            g.Warnings.Add($"Node {node.Id}: {node.Control} radius {R:0.#} m is too small for '{g.Edges[arm.Edge].Name}'; increase junction_radius.");
                            continue;
                        }
                        double x = Math.Sqrt(h);
                        var tLine = node.P + arm.Dir * x + n * w;
                        var C = node.P + arm.Dir * x + n * (w + rk);
                        var tCircle = node.P + (C - node.P) * (Rk / (Rk + rk));
                        var mid = C + (((tLine + tCircle) * 0.5) - C).Unit() * rk;
                        var fillet = new ArcSpec { A = tLine, M = mid, B = tCircle, Role = role };
                        node.RingArcs.Add(fillet);
                        if (role == "kerb face") filletKF[(a, side)] = fillet;
                        circleT[(a, side, role)] = tCircle;
                        g.LineTrims[(a, side, k)] = (x, tLine);
                        if (role == "kerb face") arm.Mouth = Math.Max(arm.Mouth, x + mouthExtra);
                    }
                }
            }
            // circle arcs between consecutive arms, counter-clockwise from A's left to B's right
            var circleKF = new Dictionary<int, ArcSpec>();
            for (int i = 0; i < deg; i++)
            {
                int a = node.Arms[i], b = node.Arms[(i + 1) % deg];
                foreach (string role in new[] { "kerb face", "boundary" })
                {
                    if (!circleT.TryGetValue((a, 'L', role), out V2 p0) || !circleT.TryGetValue((b, 'R', role), out V2 p1)) continue;
                    double a0 = (p0 - node.P).Angle, a1 = (p1 - node.P).Angle;
                    double sweep = a1 - a0;
                    while (sweep <= 1e-9) sweep += 2 * Math.PI;
                    if (sweep > 2 * Math.PI - 1e-6) continue;
                    double rad = (p0 - node.P).Length, am = a0 + sweep / 2;
                    var arc = new ArcSpec { A = p0, M = node.P + new V2(Math.Cos(am), Math.Sin(am)) * rad, B = p1, Role = role };
                    node.RingArcs.Add(arc);
                    if (role == "kerb face") circleKF[i] = arc;
                }
            }
            if (node.IslandRadius > 0)
                for (int i = 0; i <= 48; i++)
                {
                    double t = 2 * Math.PI * i / 48;
                    node.IslandOutline.Add(node.P + new V2(Math.Cos(t), Math.Sin(t)) * node.IslandRadius);
                }

            // carriageway outline, counter-clockwise: across each mouth, out along the left fillet,
            // round the circle, back along the next arm's right fillet.
            var o = node.CarriagewayOutline;
            for (int i = 0; i < deg; i++)
            {
                var A = g.Arms[node.Arms[i]];
                var B = g.Arms[node.Arms[(i + 1) % deg]];
                o.Add(node.P + A.Dir * A.Mouth + A.Dir.Right * CarriagewayWidth(A.RightParts));
                o.Add(node.P + A.Dir * A.Mouth + A.Dir.Left * CarriagewayWidth(A.LeftParts));
                if (filletKF.TryGetValue((A.Id, 'L'), out var fl)) o.AddRange(ArcSampled(fl, 8));
                if (circleKF.TryGetValue(i, out var ca)) o.AddRange(ArcSampled(ca, 24));
                if (filletKF.TryGetValue((B.Id, 'R'), out var fr)) o.AddRange(ArcSampled(new ArcSpec { A = fr.B, M = fr.M, B = fr.A }, 8));
            }
            if (o.Count > 2) o.Add(o[0]);
        }

        /// <summary>Points along an arc through A, M, B (inclusive).</summary>
        public static List<V2> ArcSampled(ArcSpec s, int segments)
        {
            // circumcentre
            double ax = s.A.X, ay = s.A.Y, bx = s.M.X, by = s.M.Y, cx = s.B.X, cy = s.B.Y;
            double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-12) return new List<V2> { s.A, s.B };
            double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
            double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;
            var c = new V2(ux, uy);
            double r = (s.A - c).Length;
            double a0 = (s.A - c).Angle, am = (s.M - c).Angle, a1 = (s.B - c).Angle;
            double ccw = Wrap(a1 - a0), toMid = Wrap(am - a0);
            double sweep = toMid <= ccw ? ccw : ccw - 2 * Math.PI;
            var list = new List<V2>();
            for (int i = 0; i <= segments; i++) { double t = a0 + sweep * i / segments; list.Add(c + new V2(Math.Cos(t), Math.Sin(t)) * r); }
            return list;
        }

        private static double Wrap(double a) { while (a < 0) a += 2 * Math.PI; while (a >= 2 * Math.PI) a -= 2 * Math.PI; return a; }

        private static double Norm360(double d) { d %= 360; return d < 0 ? d + 360 : d; }

        /// <summary>Distance along edge e where its corridor should start / end (between the junction mouths).</summary>
        public double StartStation(int e) => Arms[2 * e].Mouth;
        public double EndStation(int e) => Edges[e].Length - Arms[2 * e + 1].Mouth;

        // ------------------------------------------------------------------ JSON

        public JObject NodeJson(NetNode nd)
        {
            var inv = CultureInfo.InvariantCulture;
            var arms = new JArray();
            foreach (int a in nd.Arms)
            {
                var arm = Arms[a]; var ed = Edges[arm.Edge];
                arms.Add(new JObject
                {
                    ["arm"] = arm.Id, ["edge"] = arm.Edge, ["edge_end"] = arm.AtStart ? "start" : "end",
                    ["name"] = ed.Name, ["template"] = ed.TemplateName, ["hierarchy"] = ed.Hierarchy,
                    ["heading_deg"] = Math.Round(arm.Heading, 2),
                    ["carriageway_left_m"] = Math.Round(CarriagewayWidth(arm.LeftParts), 3),
                    ["carriageway_right_m"] = Math.Round(CarriagewayWidth(arm.RightParts), 3),
                    ["half_width_left_m"] = Math.Round(arm.LeftParts.Sum(p => p.Width), 3),
                    ["half_width_right_m"] = Math.Round(arm.RightParts.Sum(p => p.Width), 3),
                    ["priority"] = arm.Priority,
                    ["mouth_m"] = Math.Round(arm.Mouth, 3),
                    ["mouth_station_m"] = Math.Round(arm.AtStart ? arm.Mouth : ed.Length - arm.Mouth, 3),
                });
            }
            var corners = new JArray();
            foreach (int ci in nd.Corners)
            {
                var c = Corners[ci];
                corners.Add(new JObject
                {
                    ["between_arms"] = new JArray(c.ArmA, c.ArmB),
                    ["angle_deg"] = Math.Round(c.Angle, 2),
                    ["radius_m"] = c.Straight ? 0 : Math.Round(c.Radius, 2),
                    ["straight"] = c.Straight,
                    ["lines"] = new JArray(c.Lines.Select(l => new JObject
                    {
                        ["role"] = l.Role, ["radius_m"] = Math.Round(l.Radius, 3),
                        ["tangent_a_m"] = Math.Round(l.Ta, 3), ["tangent_b_m"] = Math.Round(l.Tb, 3),
                    })),
                });
            }
            double minAngle = nd.Corners.Count > 0 ? nd.Corners.Min(ci => Corners[ci].Angle) : 0;
            return new JObject
            {
                ["id"] = nd.Id, ["type"] = nd.Type, ["x"] = Math.Round(nd.P.X, 3), ["y"] = Math.Round(nd.P.Y, 3),
                ["control"] = nd.Control, ["tagged"] = nd.Tagged, ["name"] = nd.TagName,
                ["ring_radius_m"] = Math.Round(nd.RingRadius, 3), ["island_radius_m"] = Math.Round(nd.IslandRadius, 3),
                ["degree"] = nd.Arms.Count,
                ["through_arms"] = nd.Through == null ? null : new JArray(nd.Through),
                ["min_corner_angle_deg"] = Math.Round(minAngle, 2),
                ["skewed"] = nd.Arms.Count >= 3 && minAngle < 70,
                ["arms"] = arms, ["corners"] = corners,
            };
        }

        public JObject EdgeJson(int e)
        {
            var ed = Edges[e];
            return new JObject
            {
                ["id"] = e, ["name"] = ed.Name, ["template"] = ed.TemplateName, ["hierarchy"] = ed.Hierarchy,
                ["attributes_from"] = ed.Source, ["source_curve"] = ed.SourceCurve,
                ["start_node"] = EdgeStartNode[e], ["end_node"] = EdgeEndNode[e],
                ["length_m"] = Math.Round(ed.Length, 3),
                ["start_station_m"] = Math.Round(StartStation(e), 3),
                ["end_station_m"] = Math.Round(EndStation(e), 3),
                ["corridor_length_m"] = Math.Round(Math.Max(0, EndStation(e) - StartStation(e)), 3),
            };
        }

        public JObject NetworkJson()
        {
            var types = Nodes.GroupBy(nd => nd.Type).ToDictionary(gr => gr.Key, gr => gr.Count());
            return new JObject
            {
                ["nodes"] = new JArray(Nodes.Select(NodeJson)),
                ["edges"] = new JArray(Enumerable.Range(0, Edges.Count).Select(EdgeJson)),
                ["summary"] = new JObject
                {
                    ["edges"] = Edges.Count,
                    ["length_m"] = Math.Round(Edges.Sum(e => e.Length), 1),
                    ["junctions"] = Nodes.Count(nd => nd.Arms.Count >= 3),
                    ["dead_ends"] = Nodes.Count(nd => nd.Arms.Count == 1),
                    ["node_types"] = JObject.FromObject(types),
                },
            };
        }
    }
}
