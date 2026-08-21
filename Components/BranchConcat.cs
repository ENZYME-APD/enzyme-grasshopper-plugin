using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class BranchConcat : GH_Component
    {
        public BranchConcat()
          : base("Branch-Concat", "Branch-Concat",
              "Concatenates items in each branch into a single string.",
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
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 70, -11, 160, 22);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("tree", "tree", "Tree to process", GH_ParamAccess.tree);
            pManager.AddTextParameter("sep", "sep", "Separator between items", GH_ParamAccess.item, "");
            pManager.AddTextParameter("prefix", "prefix", "Prefix for the joined string", GH_ParamAccess.item, "");
            pManager.AddTextParameter("suffix", "suffix", "Suffix for the joined string", GH_ParamAccess.item, "");

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("a", "a", "Concatenated branches", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> tree)) return;

            string sep = "";
            DA.GetData(1, ref sep);

            string prefix = "";
            DA.GetData(2, ref prefix);

            string suffix = "";
            DA.GetData(3, ref suffix);

            if (sep == null) sep = "";
            if (prefix == null) prefix = "";
            if (suffix == null) suffix = "";

            GH_Structure<GH_String> result = new GH_Structure<GH_String>();

            foreach (GH_Path path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                List<string> strItems = new List<string>();

                foreach (IGH_Goo item in branch)
                {
                    if (item != null)
                        strItems.Add(item.ToString());
                    else
                        strItems.Add("None");
                }

                string joined = prefix + string.Join(sep, strItems) + suffix;
                result.Append(new GH_String(joined), path);
            }

            DA.SetDataTree(0, result);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Branch-Concat.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("1f20d0f5-46c5-4cf5-9922-850dc5768e7c"); }
        }
    }
}
