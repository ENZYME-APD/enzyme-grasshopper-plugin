using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class NoiseEmitterFromCurves : GH_Component
    {
        public NoiseEmitterFromCurves()
          : base("Noise Emitter From Curves", "NoiseLines",
              "Converts line/curve paths (roads, railways) into discrete point emitters whose source levels are calibrated so the summed field reproduces the road's level at the reference distance, independent of the divide length.",
              "Enzyme", "Site Analysis")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("NoiseEmitterLines.png");

        public override Guid ComponentGuid => new Guid("22223333-4444-5555-6666-777788889999");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve Paths", "Curves", "Lines/Curves representing noise sources (e.g. roads, railways)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Average Noises", "dB", "Equivalent continuous level LAeq [dB(A)] measured at the reference distance from each road. Re-uses last value if list is shorter.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Divide Length", "Length", "Target distance (m) between emitter points along the curve. Results are independent of this value; smaller values only give smoother fields near the road.", GH_ParamAccess.item, 5.0);
            pManager.AddNumberParameter("Reference Distance", "RefDist", "Perpendicular distance (m) from the road at which the 'Average Noises' levels apply (10 m is the usual convention).", GH_ParamAccess.item, 10.0);
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "Points", "Generated discrete noise emitter points", GH_ParamAccess.list);
            pManager.AddNumberParameter("Noise Values", "dB", "Calibrated point source level Lp at 1 m [dB(A)] for each point. Feed directly into the Fast Noise Analyzer 'Power(dB)' input.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Methodology of the line-source calibration", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            List<Curve> curves = new List<Curve>();
            if (!DA.GetDataList(0, curves)) return;
            if (curves.Count == 0) return;

            List<double> noises = new List<double>();
            if (!DA.GetDataList(1, noises)) return;
            if (noises.Count == 0) return;

            double length = 5.0;
            if (!DA.GetData(2, ref length) || length <= 0.001) length = 5.0;

            double refDist = 10.0;
            DA.GetData(3, ref refDist);
            if (refDist < 1.0) refDist = 1.0;

            List<Point3d> outPts = new List<Point3d>();
            List<double> outNoises = new List<double>();

            for (int i = 0; i < curves.Count; i++)
            {
                Curve c = curves[i];
                if (c == null || !c.IsValid) continue;

                double lRef = noises[Math.Min(i, noises.Count - 1)];

                double crvLen = c.GetLength();
                if (crvLen < 1e-6) continue;
                int divisions = (int)Math.Max(1, Math.Round(crvLen / length));
                double spacing = crvLen / divisions; // actual spacing used

                // Incoherent line of point sources, spacing D, each with level Lp1 at 1 m.
                // Intensity at perpendicular distance d: sum(I1/r^2) ~ I1*pi/(D*d).
                // Requiring this to equal L_ref at d_ref gives:
                //   Lp1 = L_ref + 10*log10(D * d_ref / pi)
                double lp1 = lRef + 10.0 * Math.Log10(spacing * refDist / Math.PI);
                // End points of open curves only represent half a segment (D/2): -3.01 dB
                double lp1End = lp1 - 10.0 * Math.Log10(2.0);

                Point3d[] divPts;
                c.DivideByCount(divisions, true, out divPts);
                if (divPts == null) continue;

                bool closed = c.IsClosed;
                for (int k = 0; k < divPts.Length; k++)
                {
                    bool isEnd = !closed && (k == 0 || k == divPts.Length - 1);
                    outPts.Add(divPts[k]);
                    outNoises.Add(isEnd ? lp1End : lp1);
                }
            }

            DA.SetDataList(0, outPts);
            DA.SetDataList(1, outNoises);

            string info =
                "NOISE EMITTER FROM CURVES - LINE SOURCE CALIBRATION\n" +
                "==================================================\n\n" +
                $"Reference distance: {refDist:F1} m | Target spacing: {length:F2} m | Points: {outPts.Count}\n\n" +
                "METHOD:\n" +
                "- Each road is modelled as an incoherent line source discretised into point sources.\n" +
                "- The input level is interpreted as LAeq [dB(A)] at the reference distance from the road.\n" +
                "- Each point receives Lp(1 m) = L_ref + 10*log10(spacing * d_ref / pi), so the summed field reproduces L_ref at d_ref\n" +
                "  and decays at -3 dB per doubling of distance (line source), regardless of the divide length.\n" +
                "- Open curve end points carry half a segment (-3 dB).\n\n" +
                "LIMITATIONS:\n" +
                "- Calibration assumes a long straight road; near the ends of short roads levels drift slightly low.\n" +
                "- Points closer than ~spacing to a receiver can produce local hot spots; use a divide length smaller than the closest receiver distance.\n\n" +
                "TYPICAL LAeq AT 10 m (presets): Highway 78, Arterial 70, Local 60, Pedestrian 50, Railway 80 dB(A).";
            DA.SetData(2, info);

            sw.Stop();
            Hud.Done(this, sw.Elapsed.TotalMilliseconds, $"Points: {outPts.Count}");
        }

        private void AutoWireDefaultInputs(GH_Document document)
        {
            // Typical LAeq [dB(A)] at 10 m from the road edge
            string[] keys = new string[] { "Highway", "Arterial", "Local", "Pedestrian", "Railway" };
            string[] vals = new string[] { "78.0", "70.0", "60.0", "50.0", "80.0" };
            Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 1, keys, vals, 200, 0);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 1.0, 50.0, 5.0, 200, 50);
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
