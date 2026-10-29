using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Enzyme.Utils;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    /// <summary>
    /// Flags survey points that jump out of the surface formed by their neighbours
    /// (bad shots, Z = 0 busts, canopy/roof hits) before meshing a terrain.
    /// </summary>
    public class SurveyOutlierCleaner : EnzymeComponent
    {
        public SurveyOutlierCleaner()
          : base("Survey Outlier Cleaner", "SurveyClean",
              "Flags survey points that sit above or below the local ground formed by their neighbours (bad shots, Z = 0 points, canopy or roof hits). Uses a robust local plane fit, so slopes are not mistaken for errors. Nothing is deleted: mesh the Clean points and review the Outliers.",
              "Enzyme", "Site Analysis")
        { }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "Pts", "Survey points (e.g. from a DWG survey).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Neighbours", "K", "Neighbours used to estimate the local ground around each point. Raise (e.g. 20) for sparse surveys.", GH_ParamAccess.item, 12);
            pManager.AddNumberParameter("Threshold", "T", "How many local scatters (robust standard deviations) a point may sit off the local ground before it is flagged.", GH_ParamAccess.item, 3.0);
            pManager.AddNumberParameter("Min Deviation", "MinDev", "Never flag deviations smaller than this (model units, e.g. metres).", GH_ParamAccess.item, 0.3);
            pManager.AddIntegerParameter("Passes", "Passes", "Re-check passes. Pass 2 ignores points flagged in pass 1, so clustered errors cannot vouch for each other.", GH_ParamAccess.item, 2);
            for (int i = 0; i < 5; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Clean", "Clean", "Points that agree with their neighbours — use these to build the mesh.", GH_ParamAccess.list);
            pManager.AddPointParameter("Outliers", "Outliers", "Flagged points, for review.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Deviation", "Dev", "Signed distance of every input point from its local ground (+ above, - below).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Score", "Score", "Deviation divided by the local scatter for every input point (flagged when > Threshold).", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Flags", "Flags", "True for every input point that was flagged (same order as Points; use with Cull Pattern).", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "Preview colour per input point: grey = clean, red = above ground, blue = below ground.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "How the component works and how to tune it.", GH_ParamAccess.item);
        }

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            DA.SetData(6, InfoText);

            var pts = new List<Point3d>();
            DA.GetDataList(0, pts);
            pts = pts.Where(p => p.IsValid).ToList();
            if (pts.Count < 4) { Hud.Waiting(this, "points"); return; }

            int k = 12; double threshold = 3.0, minDev = 0.3; int passes = 2;
            DA.GetData(1, ref k); DA.GetData(2, ref threshold); DA.GetData(3, ref minDev); DA.GetData(4, ref passes);
            if (k < 4) { k = 4; AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Neighbours raised to the minimum of 4."); }
            if (threshold <= 0) threshold = 3.0;
            if (minDev < 0) minDev = 0;
            passes = Math.Max(1, Math.Min(passes, 5));

            var result = Detect(pts, k, threshold, minDev, passes);

            var clean = new List<Point3d>(); var outliers = new List<Point3d>(); var colors = new List<Color>(pts.Count);
            int up = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                if (result.Flags[i])
                {
                    outliers.Add(pts[i]);
                    if (result.Deviation[i] > 0) { up++; colors.Add(Color.FromArgb(220, 50, 47)); }
                    else colors.Add(Color.FromArgb(38, 110, 200));
                }
                else { clean.Add(pts[i]); colors.Add(Color.FromArgb(160, 160, 160)); }
            }

            DA.SetDataList(0, clean);
            DA.SetDataList(1, outliers);
            DA.SetDataList(2, result.Deviation);
            DA.SetDataList(3, result.Score);
            DA.SetDataList(4, result.Flags);
            DA.SetDataList(5, colors);

            double maxDev = outliers.Count > 0 ? result.Deviation.Where((d, i) => result.Flags[i]).Max(d => Math.Abs(d)) : 0;
            Hud.Done(this, sw,
                Hud.Metric("Points", pts.Count),
                Hud.Metric("Outliers", outliers.Count),
                $"Up: {up} | Down: {outliers.Count - up}",
                outliers.Count > 0 ? Hud.Metric("Max off", maxDev, "m", 2) : null);
        }

        // ---------------- Detection ----------------

        public sealed class Result
        {
            public bool[] Flags;
            public double[] Deviation;
            public double[] Score;
        }

        public static Result Detect(IList<Point3d> pts, int k, double threshold, double minDev, int passes)
        {
            int n = pts.Count;
            var flat = pts.Select(p => new Point3d(p.X, p.Y, 0)).ToArray();
            var flags = new bool[n];
            var dev = new double[n];
            var score = new double[n];
            double scaleFloor = Math.Max(minDev, 1e-9) / threshold;

            for (int pass = 0; pass < passes; pass++)
            {
                int[] pool = Enumerable.Range(0, n).Where(i => !flags[i]).ToArray();
                if (pool.Length < 4) break;
                Point3d[] hay = pool.Select(i => flat[i]).ToArray();
                int[][] nbs = RTree.Point3dKNeighbors(hay, flat, Math.Min(k + 1, pool.Length)).ToArray();
                var next = new bool[n];

                Parallel.For(0, n, i =>
                {
                    var nn = new List<int>(k);
                    foreach (int h in nbs[i]) { int j = pool[h]; if (j != i && nn.Count < k) nn.Add(j); }
                    if (nn.Count < 3) { dev[i] = 0; score[i] = 0; return; }

                    int m = nn.Count;
                    var dx = new double[m]; var dy = new double[m]; var z = new double[m]; var use = new bool[m];
                    for (int q = 0; q < m; q++)
                    {
                        dx[q] = pts[nn[q]].X - pts[i].X; dy[q] = pts[nn[q]].Y - pts[i].Y; z[q] = pts[nn[q]].Z; use[q] = true;
                    }

                    // First fit, then drop neighbours that disagree with it (e.g. a spike next door) and refit.
                    FitPlane(dx, dy, z, use, out double a, out double b, out double c);
                    double mad = Mad(dx, dy, z, use, a, b, c);
                    double cut = Math.Max(3 * mad, minDev);
                    int kept = 0;
                    for (int q = 0; q < m; q++) { use[q] = Math.Abs(z[q] - (a + b * dx[q] + c * dy[q])) <= cut; if (use[q]) kept++; }
                    if (kept >= 3 && kept < m)
                    {
                        FitPlane(dx, dy, z, use, out a, out b, out c);
                        mad = Mad(dx, dy, z, use, a, b, c);
                    }
                    else for (int q = 0; q < m; q++) use[q] = true;

                    double r = pts[i].Z - a;                       // plane at the point itself (dx = dy = 0)
                    double s = Math.Abs(r) / Math.Max(mad, scaleFloor);
                    dev[i] = r; score[i] = s;
                    next[i] = s > threshold && Math.Abs(r) > minDev;
                });
                flags = next;
            }
            return new Result { Flags = flags, Deviation = dev, Score = score };
        }

        // Least-squares plane z = a + b·dx + c·dy; collinear neighbours (a road string) fall back to
        // a line fit along their main direction; a single cluster falls back to the median.
        static void FitPlane(double[] dx, double[] dy, double[] z, bool[] use, out double a, out double b, out double c)
        {
            double n = 0, sx = 0, sy = 0, sz = 0, sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0;
            for (int q = 0; q < z.Length; q++)
            {
                if (!use[q]) continue;
                n++; sx += dx[q]; sy += dy[q]; sz += z[q];
                sxx += dx[q] * dx[q]; syy += dy[q] * dy[q]; sxy += dx[q] * dy[q];
                sxz += dx[q] * z[q]; syz += dy[q] * z[q];
            }
            double[,] M = { { n, sx, sy }, { sx, sxx, sxy }, { sy, sxy, syy } };
            double[] v = { sz, sxz, syz };
            double det = Det(M);
            double spread = Math.Max(1e-12, (sxx + syy) / Math.Max(n, 1));
            if (Math.Abs(det) > 1e-9 * n * spread * spread)
            {
                a = Det(Replace(M, 0, v)) / det; b = Det(Replace(M, 1, v)) / det; c = Det(Replace(M, 2, v)) / det;
                return;
            }
            double mx = sx / n, my = sy / n, cxx = sxx / n - mx * mx, cyy = syy / n - my * my, cxy = sxy / n - mx * my;
            double ang = 0.5 * Math.Atan2(2 * cxy, cxx - cyy), ux = Math.Cos(ang), uy = Math.Sin(ang);
            double st = 0, stt = 0, stz = 0;
            for (int q = 0; q < z.Length; q++)
            {
                if (!use[q]) continue;
                double t = dx[q] * ux + dy[q] * uy; st += t; stt += t * t; stz += t * z[q];
            }
            double d = n * stt - st * st;
            if (Math.Abs(d) < 1e-12)
            {
                a = Median(z.Where((_, q) => use[q]).ToList()); b = 0; c = 0; return;
            }
            double g = (n * stz - st * sz) / d;
            a = (sz - g * st) / n; b = g * ux; c = g * uy;
        }

        static double Mad(double[] dx, double[] dy, double[] z, bool[] use, double a, double b, double c)
        {
            var res = new List<double>();
            for (int q = 0; q < z.Length; q++) if (use[q]) res.Add(z[q] - (a + b * dx[q] + c * dy[q]));
            double med = Median(new List<double>(res));
            return 1.4826 * Median(res.Select(r => Math.Abs(r - med)).ToList());
        }

        static double Det(double[,] m) =>
            m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
          - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
          + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

        static double[,] Replace(double[,] m, int col, double[] v)
        {
            var r = (double[,])m.Clone();
            for (int i = 0; i < 3; i++) r[i, col] = v[i];
            return r;
        }

        static double Median(List<double> v)
        {
            if (v.Count == 0) return 0;
            v.Sort();
            int h = v.Count / 2;
            return v.Count % 2 == 1 ? v[h] : 0.5 * (v[h - 1] + v[h]);
        }

        const string InfoText =
            "SURVEY OUTLIER CLEANER\n\n" +
            "HOW IT WORKS:\n" +
            "For every point it takes the K nearest neighbours in plan, fits a local ground plane through them, " +
            "drops neighbours that disagree with that plane and refits, then measures how far the point sits above " +
            "or below it. That distance is divided by the neighbours' own scatter (a robust standard deviation), so " +
            "rough ground tolerates more than smooth paving. A point is flagged when it is more than Threshold " +
            "scatters AND more than Min Deviation away. A second pass repeats the test ignoring flagged points, so " +
            "a small cluster of bad shots cannot vouch for each other.\n\n" +
            "TUNING:\n" +
            "- Too many good points flagged on rough ground: raise Threshold (4-5) or Min Deviation.\n" +
            "- Spikes in sparse areas missed: raise Neighbours (20+).\n" +
            "- Real features (walls, kerbs, roofs, trees, manhole lids) are correct survey points but not ground: " +
            "filter them by layer or point code before this component.\n" +
            "- Points the surveyor left at Z = 0 appear as large downward outliers; check nearby elevation text.\n\n" +
            "Nothing is deleted: mesh the Clean output and review Outliers (Colors: red = above, blue = below).";

        // ---------------- Auto-wire ----------------

        protected override void AutoWire(GH_Document document)
        {
            AutoWireHelper.WireIntegerSlider(this, document, 1, 4, 50, 12, 260, 0);
            AutoWireHelper.WireSlider(this, document, 2, 1.0, 10.0, 3.0, 260, 25);
            AutoWireHelper.WireSlider(this, document, 3, 0.0, 5.0, 0.3, 260, 50);
            AutoWireHelper.WireIntegerSlider(this, document, 4, 1, 5, 2, 260, 75);
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc != null) { AutoWire(doc); ExpireSolution(true); }
            });
        }

        protected override Bitmap Icon => IconLoader.Load("SurveyOutlierCleaner.png");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("17C7E3E1-186E-4A51-9A6E-D328024B9637");
    }
}
