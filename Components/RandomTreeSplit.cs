using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class RandomTreeSplit : GH_Component
    {
        public RandomTreeSplit()
          : base("RandomTreeSplit", "RandomTreeSplit",
              "RandomTreeSplit",
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 3.0, 1.5, 160, -30);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 3.0, 1.5, 160, 0);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 3.0, 1.5, 160, 30);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "T", "Tree", GH_ParamAccess.tree);
            pManager.AddNumberParameter("pA", "pA", "pA", GH_ParamAccess.item);
            pManager.AddNumberParameter("pB", "pB", "pB", GH_ParamAccess.item);
            pManager.AddNumberParameter("seed", "seed", "seed", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("A", "A", "A", GH_ParamAccess.tree);
            pManager.AddGenericParameter("B", "B", "B", GH_ParamAccess.tree);
            pManager.AddGenericParameter("C", "C", "C", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> T)) return;
            
            double pA = 0;
            if (!DA.GetData(1, ref pA)) return;
            
            double pB = 0;
            if (!DA.GetData(2, ref pB)) return;
            
            double seed = 0;
            if (!DA.GetData(3, ref seed)) return;

            int seedValue = (int)seed;

            if (pA < 0 || pB < 0 || pA + pB > 1.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "pA and pB must be ≥ 0 and their sum must be ≤ 1.0");
                return;
            }

            Random rng = new Random(seedValue);

            int totalBranches = T.PathCount;
            List<int> branchIndices = new List<int>();
            for (int i = 0; i < totalBranches; i++)
            {
                branchIndices.Add(i);
            }

            for (int i = 0; i < branchIndices.Count - 1; i++)
            {
                int j = rng.Next(i, branchIndices.Count);
                int temp = branchIndices[i];
                branchIndices[i] = branchIndices[j];
                branchIndices[j] = temp;
            }

            int countA = (int)Math.Floor(pA * totalBranches);
            int countB = (int)Math.Floor(pB * totalBranches);

            GH_Structure<IGH_Goo> treeA = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> treeB = new GH_Structure<IGH_Goo>();
            GH_Structure<IGH_Goo> treeC = new GH_Structure<IGH_Goo>();

            for (int i = 0; i < totalBranches; i++)
            {
                int branchIndex = branchIndices[i];
                GH_Path originalPath = T.Paths[branchIndex];
                
                var branch = T.get_Branch(originalPath);
                var items = branch.Cast<IGH_Goo>();
                
                if (i < countA)
                {
                    treeA.AppendRange(items, originalPath);
                }
                else if (i < countA + countB)
                {
                    treeB.AppendRange(items, originalPath);
                }
                else
                {
                    treeC.AppendRange(items, originalPath);
                }
            }

            DA.SetDataTree(0, treeA);
            DA.SetDataTree(1, treeB);
            DA.SetDataTree(2, treeC);

            Message = "RandomTreeSplit\n" + totalBranches + " branches split\nA:" + countA + " B:" + countB + " C:" + (totalBranches - countA - countB);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("RandomTreeSplit.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("b48ab692-a9b0-4db0-b08f-2879ef74e6f6"); }
        }
    }
}
