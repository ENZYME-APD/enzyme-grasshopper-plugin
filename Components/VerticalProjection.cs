using System;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class VerticalProjectionComponent : GH_Component
    {
        public VerticalProjectionComponent()
          : base("Vertical Projection", "VertProj",
              "Projects a point vertically (along world Z-axis) onto a given plane.",
              "Enzyme", "Pattern")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Point", "pt", "Input point", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Plane", "plane", "Input plane", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Is Contained", "isContained", "Boolean if the point is on the plane", GH_ParamAccess.item);
            pManager.AddPointParameter("Projected Point", "ProjectedPoint", "The actual projected Point3d", GH_ParamAccess.item);
            pManager.AddTextParameter("Message", "Message", "Multi-line message output", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d pt = Point3d.Unset;
            Plane plane = Plane.Unset;

            if (!DA.GetData(0, ref pt)) return;
            if (!DA.GetData(1, ref plane)) return;

            // Compute the signed distance from the point to the plane
            double dist = plane.DistanceTo(pt);

            // Create a vertical line from the point (World Z-axis)
            Vector3d verticalDir = new Vector3d(0, 0, 1);
            Line verticalLine = new Line(pt - verticalDir * 10000.0, pt + verticalDir * 10000.0);

            // Find intersection of the vertical line with the plane
            bool success = Rhino.Geometry.Intersect.Intersection.LinePlane(verticalLine, plane, out double parameter);

            bool isContained = false;
            Point3d projectedPt = pt;
            string message = "";

            if (success)
            {
                projectedPt = verticalLine.PointAt(parameter);

                if (Math.Abs(dist) < 1e-6)
                {
                    message = "Point is on the plane\nNo projection needed";
                    isContained = true;
                }
                else if (dist > 0)
                {
                    message = "Point was above the plane\nProjected downward";
                }
                else
                {
                    message = "Point was below the plane\nProjected upward";
                }
            }
            else
            {
                message = "No valid projection\nCheck input values";
            }

            // HUD Message
            this.Message = message;

            DA.SetData(0, isContained);
            DA.SetData(1, projectedPt);
            DA.SetData(2, message);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("VerticalProjection.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("B95F5651-7667-4638-A3BA-F0121F247920"); }
        }
    }
}
