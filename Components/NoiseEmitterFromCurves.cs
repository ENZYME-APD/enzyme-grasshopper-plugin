using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class NoiseEmitterFromCurves : GH_Component
    {
        public NoiseEmitterFromCurves()
          : base("Noise Emitter From Curves", "NoiseLines",
              "Converts line/curve paths (like roads or railways) into discrete noise emitter points with associated dB values.",
              "Enzyme", "Environmental")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("NoiseEmitterLines.png");

        public override Guid ComponentGuid => new Guid("22223333-4444-5555-6666-777788889999");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve Paths", "Curves", "Lines/Curves representing noise sources (e.g. roads, railways)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Average Noises", "dB", "Average noise levels matching the roads (dB). Re-uses last value if list is shorter.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Divide Length", "Length", "Distance (m) between emitter points along the curve", GH_ParamAccess.item, 5.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "Points", "Generated discrete noise emitter points", GH_ParamAccess.list);
            pManager.AddNumberParameter("Noise Values", "dB", "Decibel values mapped exactly to the points", GH_ParamAccess.list);
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

            List<Point3d> outPts = new List<Point3d>();
            List<double> outNoises = new List<double>();

            for (int i = 0; i < curves.Count; i++)
            {
                Curve c = curves[i];
                if (c == null || !c.IsValid) continue;
                
                double dB = noises[i % noises.Count];
                
                double crvLen = c.GetLength();
                int divisions = (int)Math.Max(1, Math.Round(crvLen / length));
                
                Point3d[] divPts;
                c.DivideByCount(divisions, true, out divPts);
                
                if (divPts != null)
                {
                    foreach (Point3d pt in divPts)
                    {
                        outPts.Add(pt);
                        outNoises.Add(dB);
                    }
                }
            }

            DA.SetDataList(0, outPts);
            DA.SetDataList(1, outNoises);
            
            sw.Stop();
            this.Message = $"NoiseLines\n{sw.Elapsed.TotalMilliseconds:F2} ms\n---\nPoints: {outPts.Count}";
        }

        private void AutoWireDefaultInputs(GH_Document document)
        {
            string[] keys = new string[] { "Highway", "Arterial", "Local", "Pedestrian", "Railway" };
            string[] vals = new string[] { "95.0", "85.0", "70.0", "55.0", "105.0" };
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
