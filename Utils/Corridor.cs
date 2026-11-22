using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Enzyme.Utils
{
    // =====================================================================================
    // Road corridor: a template cross section swept along a 3D centreline, with daylight
    // slopes to existing ground, pavement layers, cut/fill to subgrade and a mass haul.
    // Rhino-free (ground is a delegate) so it is unit tested.
    // Offsets: negative = left of the direction of travel, positive = right.
    // Slopes in % (negative = falls going away from the axis). Daylight slopes as H:V (2 = 1V:2H).
    // =====================================================================================

    public sealed class PavementLayer
    {
        public string Material;
        public double Thickness;
    }

    public sealed class TemplatePart
    {
        public string Type = "lane";     // lane, shoulder, kerb, cycle, footway, parking, verge, median, ditch
        public double Width;
        public double Slope;              // % ; negative falls away from the axis
        public double Rise;               // vertical step at the start of the part (kerb upstand), m
        public string Condition = "";     // "", "cut" or "fill": part only built where the ground is above / below
        public List<PavementLayer> Layers = new List<PavementLayer>();
        public double Depth => Layers.Sum(l => l.Thickness);
        public bool Superelevates => Type == "lane";

        public JObject ToJson()
        {
            var j = new JObject { ["type"] = Type, ["width"] = Width, ["slope"] = Slope };
            if (Rise != 0) j["rise"] = Rise;
            if (!string.IsNullOrEmpty(Condition)) j["only_in"] = Condition;
            if (Layers.Count > 0) j["layers"] = new JArray(Layers.Select(l => new JArray(l.Material, l.Thickness)));
            return j;
        }
    }

    public sealed class RoadTemplate
    {
        public string Name = "custom";
        public string Notes = "";
        public List<TemplatePart> Right = new List<TemplatePart>();
        public List<TemplatePart> Left = new List<TemplatePart>();
        public double CutSlope = 1.5;     // H:V
        public double FillSlope = 2.0;    // H:V
        public double MaxDaylight = 40.0; // m of slope measured in plan before giving up

        public double HalfWidth(bool right) => (right ? Right : Left).Sum(p => p.Width);

        public static readonly string[] PresetNames = { "residential street", "urban street", "rural road", "cycle path", "access track" };

        /// <summary>
        /// Parses a template. JSON: {"name", "cut_slope", "fill_slope", "max_daylight",
        /// "parts": [...] (both sides, mirrored) or "right": [...], "left": [...]}; each part
        /// {"type","width","slope","rise","only_in","layers":[["asphalt",0.04],...]}.
        /// Or a preset name.
        /// </summary>
        public static RoadTemplate Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Preset("residential street");
            string t = text.Trim();
            if (!t.StartsWith("{")) return Preset(t);
            var j = JObject.Parse(t);
            var r = new RoadTemplate
            {
                Name = (string)j["name"] ?? "custom",
                Notes = (string)j["notes"] ?? "",
                CutSlope = (double?)j["cut_slope"] ?? 1.5,
                FillSlope = (double?)j["fill_slope"] ?? 2.0,
                MaxDaylight = (double?)j["max_daylight"] ?? 40.0,
            };
            if (j["parts"] is JArray both) { r.Right = Parts(both); r.Left = Parts(both); }
            if (j["right"] is JArray rr) r.Right = Parts(rr);
            if (j["left"] is JArray ll) r.Left = Parts(ll);
            if (r.Right.Count == 0 && r.Left.Count == 0) throw new ArgumentException("Template has no parts.");
            if (r.CutSlope <= 0 || r.FillSlope <= 0) throw new ArgumentException("Cut and fill slopes must be positive (H:V).");
            return r;
        }

        private static List<TemplatePart> Parts(JArray a)
        {
            var list = new List<TemplatePart>();
            foreach (JObject p in a)
            {
                var part = new TemplatePart
                {
                    Type = ((string)p["type"] ?? "lane").ToLowerInvariant(),
                    Width = (double?)p["width"] ?? 0,
                    Slope = (double?)p["slope"] ?? 0,
                    Rise = (double?)p["rise"] ?? 0,
                    Condition = ((string)p["only_in"] ?? "").ToLowerInvariant(),
                };
                if (part.Width < 0) throw new ArgumentException("Part width cannot be negative.");
                if (p["layers"] is JArray la)
                    foreach (var l in la)
                    {
                        if (l is JArray pair && pair.Count >= 2)
                            part.Layers.Add(new PavementLayer { Material = (string)pair[0], Thickness = (double)pair[1] });
                        else if (l is JObject lo)
                            part.Layers.Add(new PavementLayer { Material = (string)lo["material"], Thickness = (double)lo["thickness"] });
                    }
                list.Add(part);
            }
            return list;
        }

        public string ToJson()
        {
            var j = new JObject
            {
                ["name"] = Name, ["notes"] = Notes,
                ["cut_slope"] = CutSlope, ["fill_slope"] = FillSlope, ["max_daylight"] = MaxDaylight,
                ["right"] = new JArray(Right.Select(p => p.ToJson())),
                ["left"] = new JArray(Left.Select(p => p.ToJson())),
            };
            return j.ToString();
        }

        // Typical metric dimensions for early design; check against the local street design guide.
        private static List<PavementLayer> Asphalt() => L(("asphalt surface", 0.04), ("asphalt binder", 0.06), ("granular base", 0.25), ("granular sub-base", 0.20));
        private static List<PavementLayer> Footway() => L(("paving", 0.08), ("bedding sand", 0.03), ("granular base", 0.15));
        private static List<PavementLayer> Cycle() => L(("asphalt surface", 0.04), ("granular base", 0.20));
        private static List<PavementLayer> Gravel() => L(("gravel wearing course", 0.10), ("granular sub-base", 0.20));
        private static List<PavementLayer> KerbBed() => L(("kerb and concrete bed", 0.30));
        private static List<PavementLayer> L(params (string m, double t)[] x) => x.Select(v => new PavementLayer { Material = v.m, Thickness = v.t }).ToList();
        private static TemplatePart P(string type, double w, double slope, List<PavementLayer> layers = null, double rise = 0, string only = "")
            => new TemplatePart { Type = type, Width = w, Slope = slope, Rise = rise, Condition = only, Layers = layers ?? new List<PavementLayer>() };

        public static RoadTemplate Preset(string name)
        {
            string key = (name ?? "").Trim().ToLowerInvariant();
            List<TemplatePart> side;
            var r = new RoadTemplate { Name = key };
            switch (key)
            {
                case "residential street":
                    side = new List<TemplatePart> { P("lane", 3.0, -2.5, Asphalt()), P("kerb", 0.15, 0, KerbBed(), 0.12), P("footway", 2.0, 2.0, Footway()), P("verge", 0.5, -4) };
                    r.Notes = "2 x 3.0 m carriageway, 0.12 m kerbs, 2.0 m footways, 0.5 m verges";
                    break;
                case "urban street":
                    side = new List<TemplatePart> { P("lane", 3.25, -2.5, Asphalt()), P("kerb", 0.15, 0, KerbBed(), 0.12), P("cycle", 1.8, 2.0, Cycle()), P("footway", 2.5, 2.0, Footway()), P("verge", 0.5, -4) };
                    r.Notes = "2 x 3.25 m carriageway, raised 1.8 m cycle tracks, 2.5 m footways";
                    break;
                case "rural road":
                    side = new List<TemplatePart> { P("lane", 3.0, -2.5, Asphalt()), P("shoulder", 1.0, -4, Gravel()), P("verge", 0.5, -6),
                        P("ditch", 1.0, -50, null, 0, "cut"), P("ditch", 0.5, 0, null, 0, "cut") };
                    r.FillSlope = 3.0; r.CutSlope = 2.0;
                    r.Notes = "2 x 3.0 m lanes, 1.0 m gravel shoulders, ditches in cut, 1:3 fill and 1:2 cut slopes";
                    break;
                case "cycle path":
                    side = new List<TemplatePart> { P("cycle", 1.25, -2, Cycle()), P("verge", 0.5, -4) };
                    r.Notes = "2.5 m two-way cycle path, 0.5 m verges";
                    break;
                case "access track":
                    side = new List<TemplatePart> { P("track", 2.0, -3, Gravel()), P("verge", 0.5, -5) };
                    r.Notes = "4.0 m gravel track";
                    break;
                default:
                    throw new ArgumentException("Unknown template '" + name + "'. Presets: " + string.Join(", ", PresetNames) + ", or a JSON template.");
            }
            r.Right = side;
            r.Left = side.Select(p => new TemplatePart { Type = p.Type, Width = p.Width, Slope = p.Slope, Rise = p.Rise, Condition = p.Condition, Layers = p.Layers }).ToList();
            return r;
        }
    }

    /// <summary>One cross section: breakpoints left to right and what lies between them.</summary>
    public sealed class CrossSection
    {
        public double Station;
        public List<double> O = new List<double>();   // offsets, left (-) to right (+)
        public List<double> Z = new List<double>();   // design levels
        /// <summary>Kind of the segment from point i to i+1: a part type, "cut slope", "fill slope", or "step".</summary>
        public List<string> Kind = new List<string>();
        /// <summary>Pavement depth under segment i (0 for unpaved / slopes).</summary>
        public List<double> Depth = new List<double>();
        public List<TemplatePart> Part = new List<TemplatePart>();
        public double CutArea, FillArea;              // m², existing ground vs earthwork line (subgrade under pavement)
        public bool LeftDaylit = true, RightDaylit = true;
        public double MaxCut, MaxFill;                // m, deepest cut / highest fill at the section
        public Dictionary<string, double> LayerArea = new Dictionary<string, double>();   // material -> m² in section
        public Dictionary<string, double> SurfaceWidth = new Dictionary<string, double>(); // part type -> plan width

        public int Count => O.Count;
    }

    public static class CorridorMath
    {
        /// <summary>Superelevation rate (%) for a curve radius: e = V² / (127 R), capped at eMax. 0 when eMax is 0.</summary>
        public static double Superelevation(double radius, double speedKmh, double eMaxPct)
        {
            if (eMaxPct <= 0 || double.IsInfinity(radius) || radius <= 0) return 0;
            double e = speedKmh * speedKmh / (127.0 * radius) * 100.0;
            return Math.Min(eMaxPct, e);
        }

        /// <summary>
        /// Lane cross slope (%) with superelevation e (%, &gt;= 0) on a side: the outside of the curve
        /// rotates from the normal crown up to +e, the inside steepens to -max(crown, e).
        /// </summary>
        public static double LaneSlope(double normalPct, double e, bool outside)
        {
            if (e <= 0) return normalPct;
            double crown = Math.Abs(normalPct);
            if (!outside) return -Math.Max(crown, e);
            return e >= crown ? e : -crown + 2 * e;
        }

        /// <summary>
        /// Builds a cross section. <paramref name="superLeft"/> is the superelevation (%) with its
        /// sign: positive = curve turns left (left is the inside), negative = turns right.
        /// ground(o) returns the existing ground level at an offset (NaN = none).
        /// </summary>
        public static CrossSection Build(RoadTemplate t, double station, double z0, double superLeft, Func<double, double> ground, double sampleStep = 0.1)
        {
            var right = Side(t, t.Right, +1, z0, superLeft, ground);
            var left = Side(t, t.Left, -1, z0, superLeft, ground);
            var s = new CrossSection { Station = station, LeftDaylit = left.daylit, RightDaylit = right.daylit };

            // Assemble left (reversed) + axis + right.
            for (int i = left.o.Count - 1; i >= 1; i--)
            {
                s.O.Add(left.o[i]); s.Z.Add(left.z[i]);
                s.Kind.Add(left.kind[i - 1]); s.Depth.Add(left.depth[i - 1]); s.Part.Add(left.part[i - 1]);
            }
            s.O.Add(0); s.Z.Add(z0);
            for (int i = 1; i < right.o.Count; i++)
            {
                s.Kind.Add(right.kind[i - 1]); s.Depth.Add(right.depth[i - 1]); s.Part.Add(right.part[i - 1]);
                s.O.Add(right.o[i]); s.Z.Add(right.z[i]);
            }

            // Cut / fill against the earthwork line (subgrade under pavement, surface elsewhere).
            double o0 = s.O[0], o1 = s.O[s.Count - 1];
            int steps = Math.Max(1, (int)Math.Ceiling((o1 - o0) / sampleStep));
            double d = (o1 - o0) / steps;
            for (int k = 0; k < steps; k++)
            {
                double o = o0 + (k + 0.5) * d;
                double g = ground(o);
                if (double.IsNaN(g)) continue;
                Surface(s, o, out double zs, out double dep);
                double ze = zs - dep;
                if (g > ze) { s.CutArea += (g - ze) * d; s.MaxCut = Math.Max(s.MaxCut, g - ze); }
                else { s.FillArea += (ze - g) * d; s.MaxFill = Math.Max(s.MaxFill, ze - g); }
            }

            // Layer and surface quantities.
            for (int i = 0; i + 1 < s.Count; i++)
            {
                double w = Math.Abs(s.O[i + 1] - s.O[i]);
                if (w <= 1e-9) continue;
                Add(s.SurfaceWidth, s.Kind[i], w);
                var part = s.Part[i];
                if (part == null) continue;
                foreach (var l in part.Layers) Add(s.LayerArea, l.Material, w * l.Thickness);
            }
            return s;
        }

        /// <summary>Design level and pavement depth at an offset (first segment containing it).</summary>
        public static void Surface(CrossSection s, double o, out double z, out double depth)
        {
            for (int i = 0; i + 1 < s.Count; i++)
            {
                double a = s.O[i], b = s.O[i + 1];
                if (b - a <= 1e-12) continue;
                if (o >= a - 1e-12 && o <= b + 1e-12)
                {
                    double f = (o - a) / (b - a);
                    z = s.Z[i] + f * (s.Z[i + 1] - s.Z[i]);
                    depth = s.Depth[i];
                    return;
                }
            }
            z = o < s.O[0] ? s.Z[0] : s.Z[s.Count - 1];
            depth = 0;
        }

        private static void Add(Dictionary<string, double> d, string k, double v)
        {
            d.TryGetValue(k, out double c); d[k] = c + v;
        }

        private sealed class SideResult
        {
            public List<double> o = new List<double>(), z = new List<double>(), depth = new List<double>();
            public List<string> kind = new List<string>();
            public List<TemplatePart> part = new List<TemplatePart>();
            public bool daylit = true;
        }

        private static SideResult Side(RoadTemplate t, List<TemplatePart> parts, int sign, double z0, double superLeft, Func<double, double> ground)
        {
            var r = new SideResult();
            double o = 0, z = z0;
            r.o.Add(0); r.z.Add(z0);
            double e = Math.Abs(superLeft);
            bool outside = superLeft != 0 && ((superLeft > 0) == (sign > 0)); // left turn: right side is outside

            foreach (var p in parts)
            {
                bool build = true;
                if (p.Condition == "cut" || p.Condition == "fill")
                {
                    double g = ground(sign * o);
                    bool inCut = !double.IsNaN(g) && g > z;
                    build = p.Condition == "cut" ? inCut : !inCut;
                }
                double rise = build ? p.Rise : 0;
                double w = build ? p.Width : 0;
                // Keep the same number of breakpoints whether or not a part is built.
                z += rise;
                r.o.Add(sign * o); r.z.Add(z); r.kind.Add("step"); r.depth.Add(0); r.part.Add(null);
                double slope = p.Superelevates ? LaneSlope(p.Slope, e, outside) : p.Slope;
                o += w; z += w * slope / 100.0;
                r.o.Add(sign * o); r.z.Add(z); r.kind.Add(p.Type); r.depth.Add(p.Depth); r.part.Add(p);
            }

            // Daylight: from the outer edge, a cut slope up or a fill slope down to the ground.
            double g0 = ground(sign * o);
            if (double.IsNaN(g0))
            {
                r.daylit = false;
                r.o.Add(sign * o); r.z.Add(z); r.kind.Add("fill slope"); r.depth.Add(0); r.part.Add(null);
                return r;
            }
            bool cut = g0 > z;
            double grad = cut ? 1.0 / t.CutSlope : -1.0 / t.FillSlope; // dz per m of offset
            Func<double, double> gap = dx => z + grad * dx - ground(sign * (o + dx)); // >0 above ground
            double step = 0.25, prev = 0, found = double.NaN;
            double g0gap = gap(0);
            for (double dx = step; dx <= t.MaxDaylight + 1e-9; dx += step)
            {
                double v = gap(dx);
                if (double.IsNaN(v)) break;
                if (Math.Sign(v) != Math.Sign(g0gap) || v == 0)
                {
                    double a = prev, b = dx;
                    for (int it = 0; it < 40; it++)
                    {
                        double m = 0.5 * (a + b);
                        double vm = gap(m);
                        if (double.IsNaN(vm) || Math.Sign(vm) == Math.Sign(g0gap)) a = m; else b = m;
                    }
                    found = 0.5 * (a + b);
                    break;
                }
                prev = dx;
            }
            if (double.IsNaN(found)) { r.daylit = false; found = Math.Min(t.MaxDaylight, prev); }
            r.o.Add(sign * (o + found)); r.z.Add(z + grad * found);
            r.kind.Add(cut ? "cut slope" : "fill slope"); r.depth.Add(0); r.part.Add(null);
            return r;
        }

        /// <summary>Average-end-area volume between consecutive sections.</summary>
        public static double[] Volumes(double[] stations, double[] areas)
        {
            var v = new double[stations.Length];
            for (int i = 1; i < stations.Length; i++) v[i] = 0.5 * (areas[i - 1] + areas[i]) * (stations[i] - stations[i - 1]);
            return v;
        }
    }

    /// <summary>Mass haul along the axis: cumulative (reusable cut − fill), balance points and haul.</summary>
    public sealed class MassHaul
    {
        public double[] Stations, Mass;         // m, m³ (positive = surplus carried forward)
        public double CutBank, Fill, Reusable;  // m³
        public double Net => Mass.Length > 0 ? Mass[Mass.Length - 1] : 0; // + export, − import at the end
        public List<double> BalancePoints = new List<double>();
        public double Haul;                      // m³·m, area between mass line and zero
        public double MovedVolume;               // m³ moved along the road (sum of loop peaks)
        public double AverageHaul => MovedVolume > 1e-9 ? Haul / MovedVolume : 0;
        public double FreeHaul, OverhaulVolume;  // volume hauled further than the free-haul distance (m³)

        /// <param name="cutVol">Bank cut volume per interval (index i = between station i-1 and i).</param>
        /// <param name="reuse">Fraction of cut usable as fill after compaction/losses (e.g. 0.9).</param>
        /// <param name="freeHaul">Free-haul distance (m); 0 = not evaluated.</param>
        public static MassHaul Compute(double[] stations, double[] cutVol, double[] fillVol, double reuse, double freeHaul = 0)
        {
            int n = stations.Length;
            var m = new MassHaul { Stations = stations, Mass = new double[n], FreeHaul = freeHaul };
            for (int i = 1; i < n; i++)
            {
                m.CutBank += cutVol[i]; m.Fill += fillVol[i];
                m.Mass[i] = m.Mass[i - 1] + cutVol[i] * reuse - fillVol[i];
            }
            m.Reusable = m.CutBank * reuse;

            // Balance points: zero crossings of the mass line (balance line at 0 from the start).
            var loops = new List<(int a, int b)>();
            int start = 0;
            for (int i = 1; i < n; i++)
            {
                double a = m.Mass[i - 1], b = m.Mass[i];
                m.Haul += Area(stations[i - 1], stations[i], a, b);
                if ((a > 0 && b < 0) || (a < 0 && b > 0))
                {
                    double s = stations[i - 1] + (stations[i] - stations[i - 1]) * a / (a - b);
                    m.BalancePoints.Add(s);
                    loops.Add((start, i)); start = i;
                }
            }
            loops.Add((start, n - 1));
            foreach (var (a, b) in loops)
            {
                double peak = 0;
                for (int i = a; i <= b; i++) peak = Math.Max(peak, Math.Abs(m.Mass[i]));
                m.MovedVolume += peak;
                if (freeHaul > 0) m.OverhaulVolume += Overhaul(stations, m.Mass, a, b, freeHaul, peak);
            }
            return m;
        }

        // |area| of a linear piece between the mass line and zero, split at a zero crossing.
        private static double Area(double s0, double s1, double a, double b)
        {
            if ((a >= 0) == (b >= 0)) return 0.5 * Math.Abs(a + b) * (s1 - s0);
            double sc = s0 + (s1 - s0) * a / (a - b);
            return 0.5 * Math.Abs(a) * (sc - s0) + 0.5 * Math.Abs(b) * (s1 - sc);
        }

        // Volume carried further than the free haul inside one loop: the loop height above the
        // highest level at which the loop is still at least freeHaul wide.
        private static double Overhaul(double[] st, double[] mass, int a, int b, double freeHaul, double peak)
        {
            if (peak <= 0) return 0;
            double lo = 0, hi = peak;
            for (int it = 0; it < 50; it++)
            {
                double level = 0.5 * (lo + hi);
                if (Width(st, mass, a, b, level) >= freeHaul) lo = level; else hi = level;
            }
            return lo;
        }

        private static double Width(double[] st, double[] mass, int a, int b, double level)
        {
            double first = double.NaN, last = double.NaN;
            for (int i = Math.Max(a, 1); i <= b; i++)
            {
                double m0 = Math.Abs(mass[i - 1]), m1 = Math.Abs(mass[i]);
                if ((m0 - level) * (m1 - level) <= 0 && m0 != m1)
                {
                    double s = st[i - 1] + (st[i] - st[i - 1]) * (level - m0) / (m1 - m0);
                    if (double.IsNaN(first)) first = s;
                    last = s;
                }
            }
            return double.IsNaN(first) ? 0 : last - first;
        }

        public static string Fmt(double v, int d) => v.ToString("F" + d, CultureInfo.InvariantCulture);
    }
}
