using System;
using System.Collections.Generic;

namespace Enzyme.Utils
{
    /// <summary>
    /// Plain-array mesh with one scalar per vertex. Faces use the Rhino convention: 4 indices per face,
    /// a triangle repeats its third index (F[4i+3] == F[4i+2]). No RhinoCommon dependency, so the maths
    /// can be unit-tested outside Rhino.
    /// </summary>
    public sealed class ScalarMesh
    {
        public double[] X, Y, Z;
        public int[] F;
        public int VertexCount => X.Length;
        public int FaceCount => F.Length / 4;

        public ScalarMesh(double[] x, double[] y, double[] z, int[] faces)
        {
            X = x; Y = y; Z = z; F = faces;
        }
    }

    /// <summary>A filled polygon belonging to one contour band (flat xyz).</summary>
    public sealed class BandPolygon
    {
        public int Band;
        public double[] Xyz;
    }

    /// <summary>One chained isoline (flat xyz).</summary>
    public sealed class Isoline
    {
        public double Value;
        public int Level;
        public bool Closed;
        public List<double> Xyz = new List<double>();
        public int PointCount => Xyz.Count / 3;
    }

    /// <summary>A label position on an isoline with its unit tangent.</summary>
    public struct LabelSpot
    {
        public double X, Y, Z, TX, TY, TZ, Value;
    }

    /// <summary>
    /// Scalar-field helpers shared by the Enzyme analysis visualisers: face/vertex conversion,
    /// masked smoothing, exact contour bands and isolines (marching triangles with shared-edge
    /// chaining), collision-free label placement and peak finding.
    /// Method after Meerkat (T.-T. Tran, MIT), rewritten in C# with exact triangle clipping.
    /// </summary>
    public static class FieldMath
    {
        // ---------------- face / vertex values ----------------

        /// <summary>Average of the unmasked faces around each vertex; NaN where no unmasked face touches it.</summary>
        public static double[] FaceToVertex(int[] F, int nv, IList<double> faceVals, bool[] faceMask)
        {
            var acc = new double[nv];
            var cnt = new int[nv];
            int nf = F.Length / 4;
            for (int f = 0; f < nf; f++)
            {
                if (faceMask != null && faceMask[f]) continue;
                double v = faceVals[f];
                if (double.IsNaN(v)) continue;
                int b = 4 * f;
                int n = F[b + 3] == F[b + 2] ? 3 : 4;
                for (int k = 0; k < n; k++) { acc[F[b + k]] += v; cnt[F[b + k]]++; }
            }
            var o = new double[nv];
            for (int i = 0; i < nv; i++) o[i] = cnt[i] > 0 ? acc[i] / cnt[i] : double.NaN;
            return o;
        }

        /// <summary>Mean of the valid vertex values of each face (NaN if none).</summary>
        public static double[] VertexToFace(int[] F, IList<double> vv)
        {
            int nf = F.Length / 4;
            var o = new double[nf];
            for (int f = 0; f < nf; f++)
            {
                int b = 4 * f;
                int n = F[b + 3] == F[b + 2] ? 3 : 4;
                double s = 0; int c = 0;
                for (int k = 0; k < n; k++)
                {
                    double v = vv[F[b + k]];
                    if (double.IsNaN(v)) continue;
                    s += v; c++;
                }
                o[f] = c > 0 ? s / c : double.NaN;
            }
            return o;
        }

        /// <summary>
        /// Laplacian smoothing along the edges of unmasked faces. Masked (building) faces don't
        /// contribute, so values never bleed across obstacles. NaN vertices stay NaN.
        /// </summary>
        public static double[] Smooth(int[] F, int nv, double[] vv, int passes, bool[] faceMask)
        {
            if (passes <= 0) return (double[])vv.Clone();
            var edges = new HashSet<long>();
            int nf = F.Length / 4;
            for (int f = 0; f < nf; f++)
            {
                if (faceMask != null && faceMask[f]) continue;
                int b = 4 * f;
                int n = F[b + 3] == F[b + 2] ? 3 : 4;
                for (int k = 0; k < n; k++)
                {
                    int p = F[b + k], q = F[b + (k + 1) % n];
                    if (p == q) continue;
                    edges.Add(p < q ? (long)p * nv + q : (long)q * nv + p);
                }
            }
            var ea = new int[edges.Count];
            var eb = new int[edges.Count];
            int e = 0;
            foreach (long key in edges) { ea[e] = (int)(key / nv); eb[e] = (int)(key % nv); e++; }

            var cur = (double[])vv.Clone();
            var acc = new double[nv];
            var cnt = new int[nv];
            for (int it = 0; it < passes; it++)
            {
                for (int i = 0; i < nv; i++) { acc[i] = double.IsNaN(cur[i]) ? 0 : cur[i]; cnt[i] = 1; }
                for (int k = 0; k < ea.Length; k++)
                {
                    double a = cur[ea[k]], b = cur[eb[k]];
                    if (double.IsNaN(a) || double.IsNaN(b)) continue;
                    acc[ea[k]] += b; cnt[ea[k]]++;
                    acc[eb[k]] += a; cnt[eb[k]]++;
                }
                for (int i = 0; i < nv; i++) cur[i] = double.IsNaN(cur[i]) ? double.NaN : acc[i] / cnt[i];
            }
            return cur;
        }

        // ---------------- contour bands ----------------

        /// <summary>
        /// Exact filled contour bands. bounds = ascending band edges (n bands = bounds.Length - 1);
        /// values outside are clamped into the first / last band. Faces wholly inside one band are
        /// kept whole; band-crossing faces are split into triangles and clipped exactly, so band
        /// edges coincide with the isolines.
        /// </summary>
        public static List<BandPolygon> Bands(ScalarMesh m, double[] vv, double[] bounds, bool[] faceMask)
        {
            var o = new List<BandPolygon>();
            int nb = bounds.Length - 1;
            if (nb < 1) return o;
            double lo = bounds[0], hi = bounds[nb];
            int nf = m.FaceCount;
            var poly = new List<double[]>(8);
            for (int f = 0; f < nf; f++)
            {
                if (faceMask != null && faceMask[f]) continue;
                int b = 4 * f;
                int n = m.F[b + 3] == m.F[b + 2] ? 3 : 4;
                bool bad = false;
                int bmin = int.MaxValue, bmax = -1;
                for (int k = 0; k < n; k++)
                {
                    double v = vv[m.F[b + k]];
                    if (double.IsNaN(v)) { bad = true; break; }
                    int bi = BandOf(Clamp(v, lo, hi), bounds);
                    if (bi < bmin) bmin = bi;
                    if (bi > bmax) bmax = bi;
                }
                if (bad) continue;
                if (bmin == bmax)
                {
                    var xyz = new double[3 * n];
                    for (int k = 0; k < n; k++)
                    {
                        int q = m.F[b + k];
                        xyz[3 * k] = m.X[q]; xyz[3 * k + 1] = m.Y[q]; xyz[3 * k + 2] = m.Z[q];
                    }
                    o.Add(new BandPolygon { Band = bmin, Xyz = xyz });
                    continue;
                }
                // split into triangles (scalar is linear on each) and clip per band
                for (int t = 0; t < n - 2; t++)
                {
                    int ia = m.F[b], ib = m.F[b + t + 1], ic = m.F[b + t + 2];
                    for (int band = bmin; band <= bmax; band++)
                    {
                        poly.Clear();
                        poly.Add(P(m, ia, Clamp(vv[ia], lo, hi)));
                        poly.Add(P(m, ib, Clamp(vv[ib], lo, hi)));
                        poly.Add(P(m, ic, Clamp(vv[ic], lo, hi)));
                        if (band > 0) poly = ClipAbove(poly, bounds[band]);
                        if (poly.Count >= 3 && band < nb - 1) poly = ClipBelow(poly, bounds[band + 1]);
                        if (poly.Count < 3) continue;
                        var xyz = new double[3 * poly.Count];
                        for (int k = 0; k < poly.Count; k++)
                        {
                            xyz[3 * k] = poly[k][0]; xyz[3 * k + 1] = poly[k][1]; xyz[3 * k + 2] = poly[k][2];
                        }
                        if (Area2(xyz) > 1e-18) o.Add(new BandPolygon { Band = band, Xyz = xyz });
                    }
                }
            }
            return o;
        }

        /// <summary>Band index of a value (last band includes its upper bound).</summary>
        public static int BandOf(double v, double[] bounds)
        {
            int nb = bounds.Length - 1;
            if (v <= bounds[0]) return 0;
            if (v >= bounds[nb]) return nb - 1;
            int lo = 0, hi = nb;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (v >= bounds[mid]) lo = mid; else hi = mid;
            }
            return lo;
        }

        private static double[] P(ScalarMesh m, int i, double v) => new[] { m.X[i], m.Y[i], m.Z[i], v };

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

        // Sutherland-Hodgman in value space: keep the part with value >= c
        private static List<double[]> ClipAbove(List<double[]> pts, double c) => Clip(pts, c, true);
        private static List<double[]> ClipBelow(List<double[]> pts, double c) => Clip(pts, c, false);

        private static List<double[]> Clip(List<double[]> pts, double c, bool keepAbove)
        {
            var o = new List<double[]>(pts.Count + 2);
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                double[] a = pts[i], b = pts[(i + 1) % n];
                bool ina = keepAbove ? a[3] >= c : a[3] <= c;
                bool inb = keepAbove ? b[3] >= c : b[3] <= c;
                if (ina) o.Add(a);
                if (ina != inb)
                {
                    double t = (c - a[3]) / (b[3] - a[3]);
                    o.Add(new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t, c });
                }
            }
            return o;
        }

        // twice the area of a polygon (any orientation, 3D), relative to the first vertex for precision
        private static double Area2(double[] xyz)
        {
            int n = xyz.Length / 3;
            double sx = 0, sy = 0, sz = 0;
            for (int i = 1; i < n - 1; i++)
            {
                double ax = xyz[3 * i] - xyz[0], ay = xyz[3 * i + 1] - xyz[1], az = xyz[3 * i + 2] - xyz[2];
                double bx = xyz[3 * i + 3] - xyz[0], by = xyz[3 * i + 4] - xyz[1], bz = xyz[3 * i + 5] - xyz[2];
                sx += ay * bz - az * by; sy += az * bx - ax * bz; sz += ax * by - ay * bx;
            }
            return Math.Sqrt(sx * sx + sy * sy + sz * sz);
        }

        // ---------------- isolines ----------------

        /// <summary>
        /// Isolines at the given levels by marching triangles. Crossing points are keyed by mesh edge,
        /// so segments of neighbouring faces chain exactly (no tolerance-based JoinCurves).
        /// </summary>
        public static List<Isoline> Isolines(ScalarMesh m, double[] vv, double[] levels, bool[] faceMask)
        {
            var result = new List<Isoline>();
            int nv = m.VertexCount, nf = m.FaceCount;
            double vmin = double.MaxValue, vmax = double.MinValue;
            foreach (double v in vv) { if (double.IsNaN(v)) continue; if (v < vmin) vmin = v; if (v > vmax) vmax = v; }
            if (vmax <= vmin) return result;
            double nudge = 1e-9 * (vmax - vmin);

            for (int li = 0; li < levels.Length; li++)
            {
                double c = levels[li];
                if (c <= vmin || c >= vmax) continue;
                var adj = new Dictionary<long, List<long>>();
                var pt = new Dictionary<long, double[]>();

                for (int f = 0; f < nf; f++)
                {
                    if (faceMask != null && faceMask[f]) continue;
                    int b = 4 * f;
                    int n = m.F[b + 3] == m.F[b + 2] ? 3 : 4;
                    for (int t = 0; t < n - 2; t++)
                    {
                        int ia = m.F[b], ib = m.F[b + t + 1], ic = m.F[b + t + 2];
                        double va = vv[ia], vb = vv[ib], vc = vv[ic];
                        if (double.IsNaN(va) || double.IsNaN(vb) || double.IsNaN(vc)) continue;
                        if (va == c) va += nudge;
                        if (vb == c) vb += nudge;
                        if (vc == c) vc += nudge;
                        long k0 = -1, k1 = -1;
                        Cross(m, ia, ib, va, vb, c, nv, pt, ref k0, ref k1);
                        Cross(m, ib, ic, vb, vc, c, nv, pt, ref k0, ref k1);
                        Cross(m, ic, ia, vc, va, c, nv, pt, ref k0, ref k1);
                        if (k0 < 0 || k1 < 0 || k0 == k1) continue;
                        Link(adj, k0, k1);
                        Link(adj, k1, k0);
                    }
                }
                Chain(adj, pt, c, li, result);
            }
            return result;
        }

        private static void Cross(ScalarMesh m, int a, int b, double va, double vb, double c, int nv,
                                  Dictionary<long, double[]> pt, ref long k0, ref long k1)
        {
            if ((va < c) == (vb < c)) return;
            long key = a < b ? (long)a * nv + b : (long)b * nv + a;
            if (!pt.ContainsKey(key))
            {
                double t = (c - va) / (vb - va);
                pt[key] = new[] { m.X[a] + (m.X[b] - m.X[a]) * t, m.Y[a] + (m.Y[b] - m.Y[a]) * t, m.Z[a] + (m.Z[b] - m.Z[a]) * t };
            }
            if (k0 < 0) k0 = key; else k1 = key;
        }

        private static void Link(Dictionary<long, List<long>> adj, long a, long b)
        {
            List<long> l;
            if (!adj.TryGetValue(a, out l)) { l = new List<long>(2); adj[a] = l; }
            if (!l.Contains(b)) l.Add(b);
        }

        private static void Chain(Dictionary<long, List<long>> adj, Dictionary<long, double[]> pt, double c, int li, List<Isoline> result)
        {
            var used = new HashSet<(long, long)>();   // undirected links already walked
            // open chains first (start at ends of degree 1), then loops
            var starts = new List<long>();
            foreach (var kv in adj) if (kv.Value.Count == 1) starts.Add(kv.Key);
            foreach (var kv in adj) if (kv.Value.Count != 1) starts.Add(kv.Key);

            foreach (long s in starts)
            {
                if (AllUsed(adj, s, used)) continue;
                var line = new Isoline { Value = c, Level = li };
                long cur = s, prev = long.MinValue;
                Add(line, pt[cur]);
                while (true)
                {
                    long next = long.MinValue;
                    foreach (long nb in adj[cur])
                    {
                        if (nb == prev) continue;
                        if (used.Contains(PairKey(cur, nb))) continue;
                        next = nb; break;
                    }
                    if (next == long.MinValue) break;
                    used.Add(PairKey(cur, next));
                    prev = cur; cur = next;
                    Add(line, pt[cur]);
                    if (cur == s) { line.Closed = true; break; }
                }
                if (line.PointCount >= 2) result.Add(line);
            }
        }

        private static bool AllUsed(Dictionary<long, List<long>> adj, long k, HashSet<(long, long)> used)
        {
            foreach (long nb in adj[k]) if (!used.Contains(PairKey(k, nb))) return false;
            return true;
        }

        private static (long, long) PairKey(long a, long b) => a < b ? (a, b) : (b, a);

        private static void Add(Isoline l, double[] p) { l.Xyz.Add(p[0]); l.Xyz.Add(p[1]); l.Xyz.Add(p[2]); }

        // ---------------- labels ----------------

        /// <summary>
        /// Evenly spaced value labels along isolines. Lines shorter than minLength get none; any two
        /// labels (also on different lines) stay at least minGap apart (spatial hash, O(n)).
        /// </summary>
        public static List<LabelSpot> PlaceLabels(List<Isoline> lines, double spacing, double minGap, double minLength)
        {
            var o = new List<LabelSpot>();
            if (spacing <= 0) return o;
            double g = Math.Max(minGap, 1e-9), g2 = g * g;
            var grid = new Dictionary<long, List<double[]>>();

            foreach (var line in lines)
            {
                int n = line.PointCount;
                if (n < 2) continue;
                var cum = new double[n];
                for (int i = 1; i < n; i++)
                {
                    double dx = line.Xyz[3 * i] - line.Xyz[3 * i - 3], dy = line.Xyz[3 * i + 1] - line.Xyz[3 * i - 2], dz = line.Xyz[3 * i + 2] - line.Xyz[3 * i - 1];
                    cum[i] = cum[i - 1] + Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }
                double len = cum[n - 1];
                if (len < minLength) continue;
                double first = len < spacing ? len * 0.5 : spacing * 0.5;
                int seg = 1;
                for (double s = first; s < len; s += spacing)
                {
                    while (seg < n - 1 && cum[seg] < s) seg++;
                    double l0 = cum[seg - 1], l1 = cum[seg];
                    double t = l1 > l0 ? (s - l0) / (l1 - l0) : 0;
                    int a = seg - 1, b = seg;
                    double ax = line.Xyz[3 * a], ay = line.Xyz[3 * a + 1], az = line.Xyz[3 * a + 2];
                    double bx = line.Xyz[3 * b], by = line.Xyz[3 * b + 1], bz = line.Xyz[3 * b + 2];
                    double px = ax + (bx - ax) * t, py = ay + (by - ay) * t, pz = az + (bz - az) * t;
                    // tangent over a window of +-2 vertices for a steadier reading direction
                    int ta = Math.Max(0, a - 2), tb = Math.Min(n - 1, b + 2);
                    double tx = line.Xyz[3 * tb] - line.Xyz[3 * ta], ty = line.Xyz[3 * tb + 1] - line.Xyz[3 * ta + 1], tz = line.Xyz[3 * tb + 2] - line.Xyz[3 * ta + 2];
                    double tl = Math.Sqrt(tx * tx + ty * ty + tz * tz);
                    if (tl < 1e-12) continue;
                    if (!Free(grid, px, py, pz, g, g2)) continue;
                    o.Add(new LabelSpot { X = px, Y = py, Z = pz, TX = tx / tl, TY = ty / tl, TZ = tz / tl, Value = line.Value });
                }
            }
            return o;
        }

        /// <summary>True (and reserves the spot) if no earlier label is within gap.</summary>
        public static bool Free(Dictionary<long, List<double[]>> grid, double px, double py, double pz, double gap, double gap2)
        {
            long ki = (long)Math.Floor(px / gap), kj = (long)Math.Floor(py / gap), kk = (long)Math.Floor(pz / gap);
            for (long a = ki - 1; a <= ki + 1; a++)
                for (long b = kj - 1; b <= kj + 1; b++)
                    for (long c = kk - 1; c <= kk + 1; c++)
                    {
                        List<double[]> l;
                        if (!grid.TryGetValue(Cell(a, b, c), out l)) continue;
                        foreach (var q in l)
                        {
                            double dx = px - q[0], dy = py - q[1], dz = pz - q[2];
                            if (dx * dx + dy * dy + dz * dz < gap2) return false;
                        }
                    }
            long key = Cell(ki, kj, kk);
            List<double[]> lst;
            if (!grid.TryGetValue(key, out lst)) { lst = new List<double[]>(); grid[key] = lst; }
            lst.Add(new[] { px, py, pz });
            return true;
        }

        private static long Cell(long a, long b, long c)
        {
            unchecked { return (a * 73856093L) ^ (b * 19349663L) ^ (c * 83492791L); }
        }

        // ---------------- peaks ----------------

        /// <summary>
        /// Local extrema over the mesh graph (vertex >= all neighbours for maxima), strongest first,
        /// at least minSeparation apart. Returns vertex indices.
        /// </summary>
        public static List<int> Peaks(ScalarMesh m, double[] vv, bool[] faceMask, bool maxima, double minSeparation, int maxCount)
        {
            int nv = m.VertexCount;
            var nb = new List<int>[nv];
            int nf = m.FaceCount;
            for (int f = 0; f < nf; f++)
            {
                if (faceMask != null && faceMask[f]) continue;
                int b = 4 * f;
                int n = m.F[b + 3] == m.F[b + 2] ? 3 : 4;
                for (int k = 0; k < n; k++)
                {
                    int p = m.F[b + k];
                    if (nb[p] == null) nb[p] = new List<int>(8);
                    for (int j = 0; j < n; j++) if (j != k && !nb[p].Contains(m.F[b + j])) nb[p].Add(m.F[b + j]);
                }
            }
            double sgn = maxima ? 1 : -1;
            var cand = new List<int>();
            for (int i = 0; i < nv; i++)
            {
                if (nb[i] == null || double.IsNaN(vv[i])) continue;
                bool ok = true, strict = false;
                foreach (int j in nb[i])
                {
                    if (double.IsNaN(vv[j])) continue;
                    double d = sgn * (vv[i] - vv[j]);
                    if (d < 0) { ok = false; break; }
                    if (d > 0) strict = true;
                }
                if (ok && strict) cand.Add(i);
            }
            cand.Sort((a, b) => (sgn * vv[b]).CompareTo(sgn * vv[a]));
            var grid = new Dictionary<long, List<double[]>>();
            double g = Math.Max(minSeparation, 1e-9);
            var o = new List<int>();
            foreach (int i in cand)
            {
                if (o.Count >= maxCount) break;
                if (Free(grid, m.X[i], m.Y[i], m.Z[i], g, g * g)) o.Add(i);
            }
            return o;
        }

        /// <summary>"Nice" round step for n intervals over a range (1, 2, 2.5, 5 x 10^k).</summary>
        public static double NiceStep(double range, int n)
        {
            if (range <= 0 || n <= 0) return 1;
            double raw = range / n;
            double p = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double r = raw / p;
            double s = r <= 1 ? 1 : r <= 2 ? 2 : r <= 2.5 ? 2.5 : r <= 5 ? 5 : 10;
            return s * p;
        }
    }
}
