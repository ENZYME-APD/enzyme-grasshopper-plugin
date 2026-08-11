using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class CondFilletComponent : GH_Component
    {
        public CondFilletComponent()
          : base("Conditional Fillet", "CondFillet",
              "Standard fillet components apply a single radius to an entire curve, which fails if any segment is too short. This \"smart\" solver evaluates and fillets a curve corner by corner.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("curves", "curves", "Input geometry (Polylines, Curves, Arcs)", GH_ParamAccess.tree);
            pManager.AddNumberParameter("radius", "radius", "The ideal fillet size you want.", GH_ParamAccess.item);
            pManager.AddNumberParameter("threshold_pct", "threshold_pct", "Max % of a segment the fillet can consume.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("skip", "skip", "True to skip tight corners; False to clamp.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("round_dec", "round_dec", "[Optional] Decimals to round down to.", GH_ParamAccess.item);
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("out_curves", "out_curves", "The final filleted geometry. (Preserves empty branches)", GH_ParamAccess.tree);
            pManager.AddNumberParameter("radii", "radii", "The exact radius applied to each specific corner.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            
            GH_Structure<GH_Curve> curvesTree;
            if (!DA.GetDataTree(0, out curvesTree)) return;

            double radius = 0;
            if (!DA.GetData(1, ref radius)) return;

            double threshold_pct = 0;
            if (!DA.GetData(2, ref threshold_pct)) return;

            bool skip = false;
            if (!DA.GetData(3, ref skip)) return;

            int round_dec = -1;
            bool hasRoundDec = DA.GetData(4, ref round_dec);

            string status_msg = skip ? "SKIP: ON" : "SKIP: OFF (Clamp)";
            string round_msg = hasRoundDec ? $"ROUND: {round_dec} dec (Floor)" : "ROUND: OFF";
            string base_msg = $"{status_msg}\n{round_msg}";
            
            Message = $"{base_msg}\nRunning...";

            GH_Structure<GH_Curve> out_curves = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Number> radii = new GH_Structure<GH_Number>();

            for (int i = 0; i < curvesTree.PathCount; i++)
            {
                GH_Path path = curvesTree.Paths[i];
                var branch = curvesTree.get_Branch(path);

                if (branch.Count == 0)
                {
                    out_curves.EnsurePath(path);
                    radii.EnsurePath(path);
                    continue;
                }

                int j = 0;
                foreach (GH_Curve ghCrv in branch.Cast<GH_Curve>())
                {
                    GH_Path radii_path = path.AppendElement(j);

                    if (ghCrv == null || ghCrv.Value == null)
                    {
                        out_curves.Append(null, path);
                        radii.EnsurePath(radii_path);
                    }
                    else
                    {
                        Curve crv = ghCrv.Value;
                        List<double> radii_list;
                        Curve new_crv = FilletUniversal(crv, radius, threshold_pct, skip, hasRoundDec, round_dec, out radii_list);

                        out_curves.Append(new GH_Curve(new_crv), path);
                        foreach (double r_val in radii_list)
                        {
                            radii.Append(new GH_Number(r_val), radii_path);
                        }
                    }
                    j++;
                }
            }
            
            watch.Stop();
            double elapsed_ms = watch.Elapsed.TotalMilliseconds;
            Message = $"{base_msg}\nTime: {elapsed_ms:F1} ms";

            DA.SetDataTree(0, out_curves);
            DA.SetDataTree(1, radii);
        }

        private Curve FilletUniversal(Curve crv, double r_input, double pct, bool skip_mode, bool hasRoundDec, int r_decimals, out List<double> calc_radii)
        {
            calc_radii = new List<double>();
            pct = Math.Min(pct, 0.49);

            var segments = crv.DuplicateSegments();
            if (segments == null || segments.Length < 2)
            {
                int c = segments == null ? 2 : segments.Length + 1;
                for (int i = 0; i < c; i++) calc_radii.Add(0.0);
                return crv;
            }

            int num_segs = segments.Length;
            bool is_closed = crv.IsClosed;
            int corner_count = is_closed ? num_segs : num_segs + 1;

            Curve[] arcs = new Curve[corner_count];
            double[][] domains = new double[num_segs][];
            for (int i = 0; i < num_segs; i++)
            {
                domains[i] = new double[] { segments[i].Domain.Min, segments[i].Domain.Max };
            }

            double calc_tol = 0.001;

            for (int i = 0; i < corner_count; i++)
            {
                if (!is_closed && (i == 0 || i == corner_count - 1))
                {
                    calc_radii.Add(0.0);
                    continue;
                }

                int idx_prev = (i - 1 + num_segs) % num_segs;
                int idx_next = i % num_segs;

                Curve c1 = segments[idx_prev];
                Curve c2 = segments[idx_next];

                Vector3d v1 = c1.TangentAt(c1.Domain.Max);
                Vector3d v2 = c2.TangentAt(c2.Domain.Min);
                v1.Unitize();
                v2.Unitize();

                double angle = Vector3d.VectorAngle(v1, v2);

                if (angle < 0.05)
                {
                    calc_radii.Add(0.0);
                    continue;
                }

                double len_limit = Math.Min(c1.GetLength(), c2.GetLength()) * pct;
                double half_angle = angle / 2.0;

                double limit;
                try
                {
                    double r_max = len_limit / Math.Tan(half_angle);
                    limit = Math.Min(len_limit, r_max);
                }
                catch
                {
                    limit = len_limit;
                }

                double r = r_input;
                if (r > limit)
                {
                    r = skip_mode ? 0.0 : limit;
                }

                if (hasRoundDec)
                {
                    double factor = Math.Pow(10.0, r_decimals);
                    r = Math.Floor(r * factor) / factor;
                }

                calc_radii.Add(r);

                if (r > calc_tol)
                {
                    double dist_t = r * Math.Tan(half_angle);
                    double g1_dist = Math.Min(dist_t, c1.GetLength() * 0.9);
                    double g2_dist = Math.Min(dist_t, c2.GetLength() * 0.9);

                    double t1;
                    bool s1 = c1.LengthParameter(c1.GetLength() - g1_dist, out t1);
                    Point3d p1 = s1 ? c1.PointAt(t1) : c1.PointAtNormalizedLength(0.9);

                    double t2;
                    bool s2 = c2.LengthParameter(g2_dist, out t2);
                    Point3d p2 = s2 ? c2.PointAt(t2) : c2.PointAtNormalizedLength(0.1);

                    Curve[] res = Curve.CreateFilletCurves(c1, p1, c2, p2, r, false, false, true, calc_tol, 0.1);

                    if (res != null && res.Length > 0)
                    {
                        Curve arc = res[0];
                        Point3d A = arc.PointAtStart;
                        Point3d B = arc.PointAtEnd;

                        double tA1, tB1, t_cut_c2_dummy;
                        bool sA1 = c1.ClosestPoint(A, out tA1);
                        bool sB1 = c1.ClosestPoint(B, out tB1);

                        double dA1 = sA1 ? A.DistanceTo(c1.PointAt(tA1)) : 999;
                        double dB1 = sB1 ? B.DistanceTo(c1.PointAt(tB1)) : 999;

                        double t_cut_c1, t_cut_c2;

                        if (dA1 < dB1)
                        {
                            t_cut_c1 = tA1;
                            c2.ClosestPoint(B, out t_cut_c2);
                            arcs[i] = arc;
                        }
                        else
                        {
                            t_cut_c1 = tB1;
                            c2.ClosestPoint(A, out t_cut_c2);
                            arc.Reverse();
                            arcs[i] = arc;
                        }

                        domains[idx_prev][1] = t_cut_c1;
                        domains[idx_next][0] = t_cut_c2;
                    }
                }
            }

            List<Curve> parts = new List<Curve>();
            for (int i = 0; i < num_segs; i++)
            {
                double d_start = domains[i][0];
                double d_end = domains[i][1];

                if (d_end - d_start > 1e-5)
                {
                    Curve trimmed = segments[i].Trim(new Interval(d_start, d_end));
                    if (trimmed != null && trimmed.GetLength() > calc_tol)
                    {
                        parts.Add(trimmed);
                    }
                }

                if (is_closed)
                {
                    int next_corner = (i + 1) % num_segs;
                    if (arcs[next_corner] != null) parts.Add(arcs[next_corner]);
                }
                else
                {
                    if (i < num_segs - 1)
                    {
                        if (arcs[i + 1] != null) parts.Add(arcs[i + 1]);
                    }
                }
            }

            if (parts.Count > 0)
            {
                Curve[] joined = Curve.JoinCurves(parts, 0.01);
                if (joined != null && joined.Length > 0)
                {
                    if (joined.Length == 1)
                    {
                        return joined[0];
                    }
                    else
                    {
                        PolyCurve pc = new PolyCurve();
                        foreach (Curve p in parts) pc.Append(p);
                        if (pc.IsValid) return pc;
                        return joined[0];
                    }
                }
            }

            return crv;
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("CondFillet.png");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public override Guid ComponentGuid
        {
            get { return new Guid("D7A5E68C-7E43-4C21-A59B-12C85D8337F1"); }
        }
    }
}
