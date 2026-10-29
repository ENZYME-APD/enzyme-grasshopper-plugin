// Survey Outlier Cleaner — Grasshopper C# Script component (Rhino 8)
//
// SETUP (right-click inputs/outputs on the C# Script component, or zoom in and use +/-):
//   Inputs   Pts        : Point3d,  List Access     — survey points
//            K          : int,      Item Access     — neighbours per point (default 12)
//            Threshold  : double,   Item Access     — robust sigmas to flag (default 3.0)
//            MinDev     : double,   Item Access     — ignore deviations smaller than this, model units (default 0.3)
//            Passes     : int,      Item Access     — re-check passes (default 2; lets clustered errors be found)
//   Outputs  Clean, Outliers, Deviation, Score, Flags
//
// METHOD: for every point, take its K nearest neighbours in plan (XY), fit a local plane
// z = a + b·dx + c·dy to them (excluding the point itself), trim neighbours that disagree
// with that plane and refit, then measure how far the point sits above/below the plane.
// That distance is scaled by the neighbours' own scatter (MAD, a robust standard deviation),
// so rough ground tolerates more than smooth paving. A point is an outlier when it is
// > Threshold scatters AND > MinDev away. Pass 2 repeats using only the points that passed,
// so a small cluster of bad points can't vouch for each other. Nothing is deleted:
// review Outliers before meshing Clean.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rhino.Geometry;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(List<Point3d> Pts, int K, double Threshold, double MinDev, int Passes,
        ref object Clean, ref object Outliers, ref object Deviation, ref object Score, ref object Flags)
    {
        if (Pts == null || Pts.Count < 4) { SetHud(Component.NickName + "\n---\nAwaiting points"); return; }
        if (K < 4) K = 12;
        if (Threshold <= 0) Threshold = 3.0;
        if (MinDev <= 0) MinDev = 0.3;
        if (Passes < 1) Passes = 2;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int n = Pts.Count;
        var flat = Pts.Select(p => new Point3d(p.X, p.Y, 0)).ToArray();
        var flagged = new bool[n];
        var dev = new double[n];
        var score = new double[n];

        for (int pass = 0; pass < Passes; pass++)
        {
            // Neighbour pool = points not flagged in the previous pass
            var pool = Enumerable.Range(0, n).Where(i => !flagged[i]).ToArray();
            if (pool.Length < 4) break;
            var hay = pool.Select(i => flat[i]).ToArray();
            int ask = Math.Min(K + 1, pool.Length);
            var nbs = RTree.Point3dKNeighbors(hay, flat, ask).ToArray();
            var next = new bool[n];

            Parallel.For(0, n, i =>
            {
                var nn = new List<int>(K);
                foreach (int h in nbs[i]) { int j = pool[h]; if (j != i && nn.Count < K) nn.Add(j); }
                if (nn.Count < 3) { dev[i] = 0; score[i] = 0; return; }

                double[] dx = nn.Select(j => Pts[j].X - Pts[i].X).ToArray();
                double[] dy = nn.Select(j => Pts[j].Y - Pts[i].Y).ToArray();
                double[] z = nn.Select(j => Pts[j].Z).ToArray();
                var use = Enumerable.Repeat(true, nn.Count).ToArray();

                double a, mad;
                Fit(dx, dy, z, use, out a, out mad);
                // Trim neighbours that disagree with the first fit (e.g. a spike next door), refit once
                double cut = Math.Max(3 * mad, MinDev);
                double b0, c0; FitCoeffs(dx, dy, z, use, out a, out b0, out c0);
                int kept = 0;
                for (int k = 0; k < z.Length; k++)
                {
                    use[k] = Math.Abs(z[k] - (a + b0 * dx[k] + c0 * dy[k])) <= cut;
                    if (use[k]) kept++;
                }
                if (kept >= 3 && kept < z.Length) Fit(dx, dy, z, use, out a, out mad);

                double r = Pts[i].Z - a;                 // plane evaluated at the point itself (dx=dy=0)
                double s = Math.Abs(r) / Math.Max(mad, MinDev / Threshold);
                dev[i] = r; score[i] = s;
                next[i] = s > Threshold && Math.Abs(r) > MinDev;
            });
            flagged = next;
        }

        var clean = new List<Point3d>(); var outl = new List<Point3d>();
        for (int i = 0; i < n; i++) (flagged[i] ? outl : clean).Add(Pts[i]);
        Clean = clean; Outliers = outl; Deviation = dev; Score = score; Flags = flagged;

        int up = Enumerable.Range(0, n).Count(i => flagged[i] && dev[i] > 0);
        SetHud($"{Component.NickName}\n{sw.Elapsed.TotalMilliseconds:F0} ms\n---\nPoints: {n:N0}\nOutliers: {outl.Count:N0} ({up} up / {outl.Count - up} down)");
    }

    void SetHud(string text)
    {
        if (Component is Grasshopper.Kernel.GH_Component gc) gc.Message = text;
    }

    // Least-squares plane through neighbours (centred on the query point); falls back to a
    // line fit along the main direction when neighbours are collinear (e.g. a road string),
    // and to the median when they are a single cluster.
    static void FitCoeffs(double[] dx, double[] dy, double[] z, bool[] use, out double a, out double b, out double c)
    {
        double n = 0, sx = 0, sy = 0, sz = 0, sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0;
        for (int k = 0; k < z.Length; k++)
        {
            if (!use[k]) continue;
            n++; sx += dx[k]; sy += dy[k]; sz += z[k];
            sxx += dx[k] * dx[k]; syy += dy[k] * dy[k]; sxy += dx[k] * dy[k];
            sxz += dx[k] * z[k]; syz += dy[k] * z[k];
        }
        double[,] M = { { n, sx, sy }, { sx, sxx, sxy }, { sy, sxy, syy } };
        double[] v = { sz, sxz, syz };
        double det = Det(M);
        double scale = Math.Max(1e-12, (sxx + syy) / Math.Max(n, 1));
        if (Math.Abs(det) > 1e-9 * n * scale * scale)
        {
            a = Det(Rep(M, 0, v)) / det; b = Det(Rep(M, 1, v)) / det; c = Det(Rep(M, 2, v)) / det;
            return;
        }
        // Collinear: fit z = a + t·g along the principal direction
        double mx = sx / n, my = sy / n, cxx = sxx / n - mx * mx, cyy = syy / n - my * my, cxy = sxy / n - mx * my;
        double ang = 0.5 * Math.Atan2(2 * cxy, cxx - cyy), ux = Math.Cos(ang), uy = Math.Sin(ang);
        double st = 0, stt = 0, stz = 0;
        for (int k = 0; k < z.Length; k++) { if (!use[k]) continue; double t = dx[k] * ux + dy[k] * uy; st += t; stt += t * t; stz += t * z[k]; }
        double d = n * stt - st * st;
        if (Math.Abs(d) < 1e-12) { a = Median(z, use); b = 0; c = 0; return; }
        double g = (n * stz - st * sz) / d;
        a = (sz - g * st) / n; b = g * ux; c = g * uy;
    }

    static void Fit(double[] dx, double[] dy, double[] z, bool[] use, out double a, out double mad)
    {
        double b, c; FitCoeffs(dx, dy, z, use, out a, out b, out c);
        var res = new List<double>();
        for (int k = 0; k < z.Length; k++) if (use[k]) res.Add(z[k] - (a + b * dx[k] + c * dy[k]));
        double med = MedianList(res);
        mad = 1.4826 * MedianList(res.Select(r => Math.Abs(r - med)).ToList());
    }

    static double Det(double[,] m) =>
        m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
    static double[,] Rep(double[,] m, int col, double[] v)
    { var r = (double[,])m.Clone(); for (int i = 0; i < 3; i++) r[i, col] = v[i]; return r; }
    static double Median(double[] z, bool[] use) => MedianList(z.Where((_, k) => use[k]).ToList());
    static double MedianList(List<double> v)
    { if (v.Count == 0) return 0; v.Sort(); int h = v.Count / 2; return v.Count % 2 == 1 ? v[h] : 0.5 * (v[h - 1] + v[h]); }
}
