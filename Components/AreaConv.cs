using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class AreaConv : GH_Component
    {
        public AreaConv()
          : base("Area Converter", "AreaConv",
              "Converts between Square Meters and Square Feet while maintaining Data Trees.",
              Enzyme.Utils.TabInfo.TabName, "Utilities")
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
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 1, false, 210, 0);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Area", "A", "Area values to convert", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Conv_Type", "C", "True for SQM > SQFT, False for SQFT > SQM", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Area", "a", "Converted area values", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<IGH_Goo> areaTree))
            {
                return;
            }

            bool convType = true;
            DA.GetData(1, ref convType);

            if (areaTree == null || areaTree.DataCount == 0)
            {
                Message = "Empty Tree";
                return;
            }

            Message = convType ? "SQM > SQFT" : "SQFT > SQM";

            GH_Structure<IGH_Goo> resultTree = new GH_Structure<IGH_Goo>();
            double factor = 10.7639104;

            foreach (GH_Path path in areaTree.Paths)
            {
                var branch = areaTree.get_Branch(path);
                foreach (IGH_Goo goo in branch.Cast<IGH_Goo>())
                {
                    if (goo == null)
                    {
                        resultTree.Append(null, path);
                        continue;
                    }

                    if (goo.CastTo(out double val))
                    {
                        double res = convType ? val * factor : val / factor;
                        resultTree.Append(new GH_Number(res), path);
                    }
                    else
                    {
                        resultTree.Append(goo.Duplicate(), path);
                    }
                }
            }

            DA.SetDataTree(0, resultTree);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("AreaConv.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("636E28D4-7EE9-4BE0-A224-C7C1E2ADD8A2"); }
        }
    }
}
