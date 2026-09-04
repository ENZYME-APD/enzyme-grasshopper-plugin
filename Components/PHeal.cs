using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class PolylineHealerComponent : GH_Component
    {
        public PolylineHealerComponent()
          : base("Polyline Healer", "P-Heal",
              "Heals polylines by extending segments and creating boolean regions.",
              "Enzyme", "Curve")
        {
        }

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 3.0, 1.5, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 220, 0);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Polyline", "p", "Polyline to heal", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Tolerance", "tol", "Extension factor", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Result", "a", "Healed curve", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> pTree)) return;
            
            double tol = 0;
            if (!DA.GetData(1, ref tol)) return;

            GH_Structure<GH_Curve> resultTree = new GH_Structure<GH_Curve>();

            foreach (GH_Path path in pTree.Paths)
            {
                System.Collections.IList branch = pTree.get_Branch(path);
                foreach (GH_Curve ghCurve in branch)
                {
                    if (ghCurve == null || ghCurve.Value == null)
                    {
                        resultTree.Append(null, path);
                        continue;
                    }

                    Curve c = ghCurve.Value;
                    Curve resCrv = HealByExtensionLogic(c, tol);
                    resultTree.Append(new GH_Curve(resCrv), path);
                }
            }

            DA.SetDataTree(0, resultTree);

            stopwatch.Stop();
            double calcTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            string version = "v1.2";
            Message = $"{version}\n{calcTimeMs:F2} ms";
        }

        private Curve HealByExtensionLogic(Curve curve, double extensionFactor)
        {
            if (curve == null) return curve;
            
            if (!curve.TryGetPolyline(out Polyline poly) || poly.Count < 2)
            {
                return curve;
            }

            if (Plane.FitPlaneToPoints(poly, out Plane plane) != PlaneFitResult.Success)
            {
                plane = Plane.WorldXY;
            }

            Line[] segments = poly.GetSegments();
            List<Curve> extendedCurves = new List<Curve>();

            foreach (Line line in segments)
            {
                if (line.Length > 0.001)
                {
                    Vector3d vec = line.UnitTangent;
                    Point3d p0 = line.From - vec * extensionFactor;
                    Point3d p1 = line.To + vec * extensionFactor;
                    extendedCurves.Add(new LineCurve(p0, p1));
                }
            }

            var res = Curve.CreateBooleanRegions(extendedCurves, plane, true, 0.001);

            if (res != null && res.RegionCount > 0)
            {
                List<Curve> allPieces = new List<Curve>();
                for (int i = 0; i < res.RegionCount; i++)
                {
                    allPieces.AddRange(res.RegionCurves(i));
                }

                Curve[] finalUnion = Curve.CreateBooleanUnion(allPieces, 0.001);
                if (finalUnion != null && finalUnion.Length > 0)
                {
                    Curve outer = finalUnion.OrderBy(c => 
                    {
                        var amp = AreaMassProperties.Compute(c);
                        return amp != null ? amp.Area : 0;
                    }).LastOrDefault();

                    if (outer != null)
                    {
                        if (outer.TryGetPolyline(out Polyline resultPoly))
                        {
                            return new PolylineCurve(resultPoly);
                        }
                        return outer;
                    }
                }
            }

            return curve;
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("P-Heal.png");
            }
        }

        
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public override Guid ComponentGuid
        {
            get { return new Guid("13e61c5a-73d7-4c7b-944a-d8c7c91d84f4"); }
        }
    }
}
