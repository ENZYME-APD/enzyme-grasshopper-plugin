using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class TShuffle : GH_Component
    {
        public TShuffle()
          : base("Tree Shuffler", "T-Shuffle",
              "Shuffles items from a pool into an existing tree structure",
              "Enzyme", "Data")
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 84, 42, 330, 0);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tree", "tree", "Existing tree structure", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Choices", "choices", "List of choices to shuffle from", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Seed", "seed", "Random seed", GH_ParamAccess.item, 42);
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Result", "A", "Shuffled result", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree))
            {
                Message = "Waiting for data...";
                return;
            }
            
            List<IGH_Goo> choices = new List<IGH_Goo>();
            if (!DA.GetDataList(1, choices) || choices.Count == 0)
            {
                Message = "Waiting for data...";
                return;
            }
            
            int seed = 42;
            DA.GetData(2, ref seed);

            Random rnd = new Random(seed);
            List<IGH_Goo> pool = new List<IGH_Goo>(choices);
            
            // Fisher-Yates shuffle
            int n = pool.Count;
            while (n > 1) 
            {
                n--;
                int k = rnd.Next(n + 1);
                IGH_Goo value = pool[k];
                pool[k] = pool[n];
                pool[n] = value;
            }

            GH_Structure<IGH_Goo> result = new GH_Structure<IGH_Goo>();
            int i = 0;
            int total_items = 0;

            foreach (GH_Path path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                
                // Explicit cast as instructed for get_Branch result
                foreach (IGH_Goo item in branch.Cast<IGH_Goo>())
                {
                    IGH_Goo pick = pool[i % pool.Count];
                    result.Append(pick, path);
                    i++;
                    total_items++;
                }
            }

            Message = $"Branches: {tree.PathCount}\nItems: {total_items}";
            
            DA.SetDataTree(0, result);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("T-Shuffle.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("b8c0a21a-e8d1-4475-8422-9df738b58490"); }
        }
    }
}
