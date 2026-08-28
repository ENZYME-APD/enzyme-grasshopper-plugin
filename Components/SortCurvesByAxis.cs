using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class SortCurvesByAxis : GH_Component
    {
        public SortCurvesByAxis()
          : base("Sort Curves By Axis", "SortAxis",
              "Sorts a tree of curves based on their bounding box center along a specific axis.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "Tree of curves to sort", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Axis", "A", "Sort Axis (0=X, 1=Y, 2=Z)", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Reverse", "R", "Reverse sorting direction", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        private bool hasSources = false;
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 1, new string[]{"X Axis", "Y Axis", "Z Axis"}, new string[]{"0", "1", "2"}, 300, -20);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 2, false, 210, 20);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Sorted Curves", "C", "Sorted tree of curves", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Curve> curvesTree = new GH_Structure<GH_Curve>();
            if (!DA.GetDataTree(0, out curvesTree)) return;

            int axis = 0;
            DA.GetData(1, ref axis);

            bool reverse = false;
            DA.GetData(2, ref reverse);

            GH_Structure<GH_Curve> outTree = new GH_Structure<GH_Curve>();

            for (int i = 0; i < curvesTree.Paths.Count; i++)
            {
                var path = curvesTree.Paths[i];
                var branch = curvesTree.Branches[i];
                
                var curveDataList = new List<Tuple<GH_Curve, double>>();

                foreach (var ghCurve in branch)
                {
                    if (ghCurve == null || ghCurve.Value == null) continue;
                    
                    BoundingBox bbox = ghCurve.Value.GetBoundingBox(true);
                    Point3d center = bbox.Center;
                    
                    double sortVal = 0.0;
                    if (axis == 0) sortVal = center.X;
                    else if (axis == 1) sortVal = center.Y;
                    else sortVal = center.Z;
                    
                    curveDataList.Add(new Tuple<GH_Curve, double>(ghCurve, sortVal));
                }
                
                var sorted = curveDataList.OrderBy(x => x.Item2).ToList();
                
                if (reverse)
                {
                    sorted.Reverse();
                }
                
                foreach (var item in sorted)
                {
                    outTree.Append(item.Item1, path);
                }
            }

            DA.SetDataTree(0, outTree);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("D5E2F6B2-72E9-5578-BC41-BCDEF2345678"); }
        }
    }
}
