using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class BranchSizeSplit : GH_Component
    {
        public BranchSizeSplit()
          : base("BranchSizeSplit", "BranchSizeSplit",
              "Split branches based on their size",
              "Enzyme", "Utilities")
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 3.0, 1.5, 160, -15);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 3.0, 1.5, 160, 15);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Tree to process", GH_ParamAccess.tree);
            pManager.AddNumberParameter("MinItems", "minItems", "Minimum items threshold", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxItems", "maxItems", "Maximum items threshold", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree A", "A", "Branches with fewer than minItems items", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Tree B", "B", "Branches with items between minItems and maxItems", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Tree C", "C", "Branches with more than maxItems items", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree)) return;
            
            double minItems = 0;
            if (!DA.GetData(1, ref minItems)) return;
            
            double maxItems = 0;
            if (!DA.GetData(2, ref maxItems)) return;

            int minThreshold = (int)minItems;
            int maxThreshold = (int)maxItems;

            if (minThreshold < 0 || maxThreshold < minThreshold)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "minItems must be ≥ 0 and maxItems must be ≥ minItems");
                return;
            }

            GH_Structure<IGH_Goo> treeA = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> treeB = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> treeC = new GH_Structure<IGH_Goo>();

            int countA = 0;
            int countB = 0;
            int countC = 0;

            foreach (GH_Path path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                int itemCount = branch.Count;
                
                var items = branch.Cast<IGH_Goo>();

                if (itemCount < minThreshold)
                {
                    treeA.AppendRange(items, path);
                    countA++;
                }
                else if (itemCount <= maxThreshold)
                {
                    treeB.AppendRange(items, path);
                    countB++;
                }
                else
                {
                    treeC.AppendRange(items, path);
                    countC++;
                }
            }

            DA.SetDataTree(0, treeA);
            DA.SetDataTree(1, treeB);
            DA.SetDataTree(2, treeC);

            Message = "BranchSizeSplit\n" +
                      countA + " branches in A (<" + minThreshold + " items)\n" +
                      countB + " branches in B (" + minThreshold + "-" + maxThreshold + " items)\n" +
                      countC + " branches in C (>" + maxThreshold + " items)";
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("BranchSizeSplit.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("14d60fc5-afaf-4c38-89c0-8d5cd62002f9"); }
        }
    }
}
