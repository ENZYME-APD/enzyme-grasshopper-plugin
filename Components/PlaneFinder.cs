using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class PlaneFinder : GH_Component
    {
        public PlaneFinder()
          : base("Surface Plane Finder", "plane_finder",
              "Finds a plane from a planar surface or brep face.",
              "Enzyme", "Pattern")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("x", "x", "Input geometry", GH_ParamAccess.list);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddVectorParameter("plane_normal", "plane_normal", "Normal of the plane", GH_ParamAccess.item);
            pManager.AddVectorParameter("reverse_normal", "reverse_normal", "Reversed normal", GH_ParamAccess.item);
            pManager.AddPlaneParameter("output_plane", "output_plane", "Output plane", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GeometryBase> xList = new List<GeometryBase>();
            if (!DA.GetDataList(0, xList)) return;

            string component_name = "Surface Plane Finder";
            string component_version = "v1.0";

            GeometryBase input_item = null;
            if (xList.Count > 0 && xList[0] != null)
            {
                input_item = xList[0];
            }

            Surface surface = null;

            if (input_item != null)
            {
                if (input_item is Surface srf)
                {
                    surface = srf;
                }
                else if (input_item is Brep brep)
                {
                    if (brep.Faces.Count == 1)
                    {
                        surface = brep.Faces[0].UnderlyingSurface();
                    }
                    else
                    {
                        this.Message = $"{component_name} {component_version}\nInput is a polysurface.";
                    }
                }
                else
                {
                    this.Message = $"{component_name} {component_version}\nInvalid input type.";
                }
            }
            else if (xList.Count == 0 || xList[0] == null)
            {
                this.Message = $"{component_name} {component_version}\nNo valid surface provided.";
            }

            if (surface != null)
            {
                double tol = Rhino.RhinoDoc.ActiveDoc != null ? Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance : 0.001;
                
                if (surface.IsPlanar(tol))
                {
                    double u = surface.Domain(0).Mid;
                    double v = surface.Domain(1).Mid;

                    Vector3d normal = surface.NormalAt(u, v);
                    normal.Unitize();

                    Vector3d reverse_normal = -normal;
                    Point3d centroid;

                    if (surface is PlaneSurface planeSrf)
                    {
                        BoundingBox bbox = planeSrf.GetBoundingBox(true);
                        centroid = bbox.Center;
                    }
                    else
                    {
                        var brepFromSurface = Brep.CreateFromSurface(surface);
                        if (brepFromSurface != null)
                        {
                            var amp = AreaMassProperties.Compute(brepFromSurface);
                            if (amp != null)
                            {
                                centroid = amp.Centroid;
                            }
                            else
                            {
                                centroid = surface.PointAt(u, v);
                            }
                        }
                        else
                        {
                            centroid = surface.PointAt(u, v);
                        }
                    }

                    Plane plane = new Plane(centroid, normal);

                    DA.SetData(0, normal);
                    DA.SetData(1, reverse_normal);
                    DA.SetData(2, plane);

                    this.Message = $"{component_name} {component_version}\nSurface is planar.";
                }
                else
                {
                    this.Message = $"{component_name} {component_version}\nSurface is NOT planar.";
                }
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("f6e9bc6f-cd1e-4581-9b19-c0202ad9d968"); }
        }
    }
}
