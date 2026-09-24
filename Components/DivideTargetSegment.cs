using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class DivideTargetSegment : GH_Component
    {
        public DivideTargetSegment()
            : base("Divide Target Length by Segment", "DivTrgtSeg",
                "Divides a curve by a target length, but respects kinks/discontinuities by dividing each structural segment independently.",
                "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Curves to divide", GH_ParamAccess.list);
            pManager.AddNumberParameter("Target Length", "L", "Target length for subdivisions", GH_ParamAccess.item, 10.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Division points (without duplicates at kinks)", GH_ParamAccess.tree);
            pManager.AddCurveParameter("SubCurves", "S", "Divided sub-curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Lengths", "L", "Actual length of each sub-curve", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            List<Curve> curves = new List<Curve>();
            double targetLength = 10.0;

            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref targetLength);

            if (targetLength <= 0.001)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Target Length must be strictly positive.");
                return;
            }

            GH_Structure<GH_Point> outPts = new GH_Structure<GH_Point>();
            GH_Structure<GH_Curve> outSubs = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Number> outLens = new GH_Structure<GH_Number>();

            for (int i = 0; i < curves.Count; i++)
            {
                Curve c = curves[i];
                if (c == null || !c.IsValid) continue;

                GH_Path path = new GH_Path(i);

                // Break the curve into its sub-segments at C1/G1 discontinuities (kinks)
                Curve[] segments = c.DuplicateSegments();
                if (segments == null || segments.Length == 0)
                {
                    segments = new Curve[] { c };
                }

                List<Point3d> pts = new List<Point3d>();
                List<Curve> subs = new List<Curve>();
                List<double> lens = new List<double>();

                // Initialize with the very first point of the curve
                pts.Add(segments[0].PointAtStart);

                foreach (Curve seg in segments)
                {
                    double len = seg.GetLength();
                    
                    // Determine number of divisions based on target length
                    int n = (int)Math.Round(len / targetLength);
                    if (n < 1) n = 1;

                    double[] tVals = seg.DivideByCount(n, true);
                    if (tVals != null && tVals.Length > 1)
                    {
                        for (int k = 0; k < tVals.Length - 1; k++)
                        {
                            Curve sub = seg.Trim(tVals[k], tVals[k + 1]);
                            if (sub != null)
                            {
                                subs.Add(sub);
                                lens.Add(sub.GetLength());
                            }
                        }
                        
                        // Add the division points, skipping the first one to avoid duplicates at kinks
                        for (int k = 1; k < tVals.Length; k++)
                        {
                            pts.Add(seg.PointAt(tVals[k]));
                        }
                    }
                    else
                    {
                        // Fallback if division fails (e.g. extremely short segment)
                        subs.Add(seg);
                        lens.Add(len);
                        pts.Add(seg.PointAtEnd);
                    }
                }

                // Add to output structures
                foreach (Point3d pt in pts) outPts.Append(new GH_Point(pt), path);
                foreach (Curve sub in subs) outSubs.Append(new GH_Curve(sub), path);
                foreach (double l in lens) outLens.Append(new GH_Number(l), path);
            }

            DA.SetDataTree(0, outPts);
            DA.SetDataTree(1, outSubs);
            DA.SetDataTree(2, outLens);
        
            stopwatch.Stop();
            Message = $"{this.NickName}\n{stopwatch.ElapsedMilliseconds} ms\n---\nDone";
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("4A983B1E-F3C2-4B2E-8E5A-71B62C9D3F8C");
    }
}
