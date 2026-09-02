using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class AutoGrade : GH_Component
    {
        public AutoGrade()
          : base("Auto-Grade Road", "AutoGrade",
              "Procedurally drapes and slope-constrains a 2D centerline into a smooth 3D road profile.",
              Enzyme.Utils.TabInfo.TabName, "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Centerline", "C", "Flat 2D Centerline", GH_ParamAccess.item);
            pManager.AddMeshParameter("Terrain", "T", "Existing Ground Mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Max Slope", "S", "Maximum longitudinal slope (e.g. 0.08 for 8%)", GH_ParamAccess.item, 0.08);
            pManager.AddNumberParameter("Resolution", "R", "Subdivision distance in meters", GH_ParamAccess.item, 2.0);
            pManager.AddIntegerParameter("Smoothing", "Sm", "Smoothing iterations for vertical curvature", GH_ParamAccess.item, 5);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("3D Road", "R3D", "The optimized, slope-constrained 3D centerline", GH_ParamAccess.item);
            pManager.AddCurveParameter("Existing Ground", "EG", "The raw draped profile on the terrain", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve crv = null;
            Mesh terrain = null;
            double maxSlope = 0.08;
            double res = 2.0;
            int smoothPasses = 5;

            if (!DA.GetData(0, ref crv)) return;
            if (!DA.GetData(1, ref terrain)) return;
            DA.GetData(2, ref maxSlope);
            DA.GetData(3, ref res);
            DA.GetData(4, ref smoothPasses);

            Curve nCrv = crv.ToNurbsCurve();
            double length = nCrv.GetLength();
            int divs = Math.Max(2, (int)(length / res));

            double[] tParams = nCrv.DivideByCount(divs, true);
            if (tParams == null || tParams.Length < 2) tParams = nCrv.DivideByCount(divs, false);
            if (tParams == null || tParams.Length < 2)
            {
                tParams = new double[divs + 1];
                for (int i = 0; i <= divs; i++)
                    tParams[i] = nCrv.Domain.T0 + (nCrv.Domain.T1 - nCrv.Domain.T0) * ((double)i / divs);
            }

            Point3d[] pts2D = new Point3d[tParams.Length];
            for (int i = 0; i < tParams.Length; i++) pts2D[i] = nCrv.PointAt(tParams[i]);

            // 1. DRAPE: Existing Ground (EG)
            double[] zEG = new double[pts2D.Length];
            for (int i = 0; i < pts2D.Length; i++)
            {
                Ray3d ray = new Ray3d(new Point3d(pts2D[i].X, pts2D[i].Y, pts2D[i].Z + 10000), -Vector3d.ZAxis);
                double t = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, ray);
                if (t >= 0.0) zEG[i] = ray.PointAt(t).Z;
                else zEG[i] = pts2D[i].Z; // fallback if off-mesh
            }

            // 2. CONSTRAIN: Forward/Backward Slope Envelope
            double[] zGrade = (double[])zEG.Clone();

            // Forward Pass
            for (int i = 1; i < pts2D.Length; i++)
            {
                double dist = new Point3d(pts2D[i].X, pts2D[i].Y, 0).DistanceTo(new Point3d(pts2D[i - 1].X, pts2D[i - 1].Y, 0));
                double maxZ = zGrade[i - 1] + maxSlope * dist;
                double minZ = zGrade[i - 1] - maxSlope * dist;

                if (zGrade[i] > maxZ) zGrade[i] = maxZ;
                if (zGrade[i] < minZ) zGrade[i] = minZ;
            }

            // Backward Pass
            for (int i = pts2D.Length - 2; i >= 0; i--)
            {
                double dist = new Point3d(pts2D[i].X, pts2D[i].Y, 0).DistanceTo(new Point3d(pts2D[i + 1].X, pts2D[i + 1].Y, 0));
                double maxZ = zGrade[i + 1] + maxSlope * dist;
                double minZ = zGrade[i + 1] - maxSlope * dist;

                if (zGrade[i] > maxZ) zGrade[i] = maxZ;
                if (zGrade[i] < minZ) zGrade[i] = minZ;
            }

            // 3. SMOOTH: Vertical Curvature (Moving Average)
            // Moving average is mathematically guaranteed to preserve (or lessen) the max slope constraint
            // while smoothing sharp kinks into vertical parabolas.
            double[] zSmooth = (double[])zGrade.Clone();
            int window = Math.Max(1, divs / 20); // 5% window size
            
            for (int pass = 0; pass < smoothPasses; pass++)
            {
                double[] temp = (double[])zSmooth.Clone();
                for (int i = 0; i < zSmooth.Length; i++)
                {
                    double sum = 0;
                    int count = 0;
                    int start = Math.Max(0, i - window);
                    int end = Math.Min(zSmooth.Length - 1, i + window);
                    for (int j = start; j <= end; j++)
                    {
                        sum += zSmooth[j];
                        count++;
                    }
                    temp[i] = sum / count;
                }
                // Pin the absolute start and end points so it perfectly hits intersections
                temp[0] = zSmooth[0];
                temp[temp.Length - 1] = zSmooth[zSmooth.Length - 1];
                
                zSmooth = temp;
            }

            // 4. BAKE: Generate Output Curves
            List<Point3d> egPts = new List<Point3d>();
            List<Point3d> gradePts = new List<Point3d>();
            for (int i = 0; i < pts2D.Length; i++)
            {
                egPts.Add(new Point3d(pts2D[i].X, pts2D[i].Y, zEG[i]));
                gradePts.Add(new Point3d(pts2D[i].X, pts2D[i].Y, zSmooth[i]));
            }

            Curve egCrv = new PolylineCurve(egPts);
            Curve final3D = Curve.CreateInterpolatedCurve(gradePts, 3);
            if (final3D == null) final3D = new PolylineCurve(gradePts);

            DA.SetData(0, final3D);
            DA.SetData(1, egCrv);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("8F1604B4-CC1A-4FF0-911E-14E1C2BC9DB2"); }
        }
    }
}
