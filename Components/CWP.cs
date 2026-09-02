using System;
using System.Linq;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class CWP : GH_Component
    {
        public CWP()
          : base("CWProfile", "CWP",
              "Creates a CWProfile polyline",
              Enzyme.Utils.TabInfo.TabName, "Facade")
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 0, 0.0, 2.0, 0.3, 330, -80);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 2.0, 0.5, 330, -40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 0.0, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 2.0, 0.0, 330, 40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 2.0, 0.0, 330, 80);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 220, 0);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("A", "A", "Dimension A", GH_ParamAccess.item, 0.3);
            pManager.AddNumberParameter("B", "B", "Dimension B", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("dA", "dA", "Offset dA", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("dB", "dB", "Offset dB", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("R", "R", "Fillet radius R", GH_ParamAccess.item, 0.0);
            
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Polyline", "P", "Resulting Polyline", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double A = 0.3;
            double B = 0.5;
            double dA = 0.0;
            double dB = 0.0;
            double R = 0.0;

            if (!DA.GetData(0, ref A)) A = 0.3;
            if (!DA.GetData(1, ref B)) B = 0.5;
            if (!DA.GetData(2, ref dA)) dA = 0.0;
            if (!DA.GetData(3, ref dB)) dB = 0.0;
            if (!DA.GetData(4, ref R)) R = 0.0;

            string version = "v1.0";
            string filletMessage = R == 0 ? "No fillet" : $"R={R}";
            Message = $"CWProfile {version}\n{A}x{B}\n{filletMessage}";

            if (A <= 0 || B <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Dimensions A and B must be positive.");
                return;
            }
            if (R < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Fillet radius R cannot be negative.");
                return;
            }

            double halfA = A / 2.0;
            double halfB = B / 2.0;
            Point3d[] corners = new Point3d[]
            {
                new Point3d(-halfA, -halfB, 0),
                new Point3d(halfA, -halfB, 0),
                new Point3d(halfA, halfB, 0),
                new Point3d(-halfA, halfB, 0),
                new Point3d(-halfA, -halfB, 0)
            };

            Polyline polyline = new Polyline(corners);

            if (R > 0)
            {
                if (R > Math.Min(A, B) / 2.0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Fillet radius R is too large for the rectangle dimensions. Using sharp corners.");
                    R = 0;
                }
                else
                {
                    Curve polyCurve = polyline.ToPolylineCurve();
                    if (polyCurve != null && polyCurve.IsValid && polyCurve.IsClosed && polyCurve.IsPlanar())
                    {
                        Curve filletedCurve = Curve.CreateFilletCornersCurve(
                            polyCurve,
                            R,
                            RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001,
                            RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAngleToleranceRadians : 0.01);

                        if (filletedCurve != null && filletedCurve.IsValid && filletedCurve.IsClosed)
                        {
                            Polyline tempPoly;
                            bool converted = filletedCurve.TryGetPolyline(out tempPoly);
                            if (!converted || tempPoly == null)
                            {
                                PolylineCurve polylineCurve = filletedCurve.ToPolyline(
                                    RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001,
                                    RhinoDoc.ActiveDoc != null ? RhinoDoc.ActiveDoc.ModelAngleToleranceRadians : 0.01,
                                    0.01,
                                    0.0
                                );
                                if (polylineCurve != null && polylineCurve.TryGetPolyline(out tempPoly))
                                {
                                    polyline = tempPoly;
                                }
                                else
                                {
                                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to convert filleted curve to polyline. Using sharp corners.");
                                }
                            }
                            else
                            {
                                polyline = tempPoly;
                            }
                        }
                        else
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Filleting failed. Using sharp corners.");
                        }
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid or non-planar curve. Using sharp corners.");
                    }
                }
            }

            Vector3d offset = new Vector3d(dA, dB, 0);
            polyline.Transform(Transform.Translation(offset));

            DA.SetData(0, polyline);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("CWP.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("1A2B3C4D-5E6F-7A8B-9C0D-1E2F3A4B5C6D"); }
        }
    }
}
