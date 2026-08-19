using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class ParseTableComponent : GH_Component
    {
        public ParseTableComponent()
          : base("Table to DataTree", "ParseTable",
              "Converts multiline tabular text into a Grasshopper DataTree.",
              "Enzyme", "Masterplan")
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
                int ix = 200, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 1, true, ix, -120);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("TableData", "TD", "Multiline text panel (Paste from Excel).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("ByColumn", "BC", "True = Branches are Columns. False = Rows.", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("DataTree", "DT", "The structured Grasshopper DataTree.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string text = null;
            if (!DA.GetData(0, ref text)) return;

            bool byColumn = true;
            DA.GetData(1, ref byColumn);

            var tree = new GH_Structure<IGH_Goo>();
            string msg = "Awaiting Table Data";

            if (string.IsNullOrWhiteSpace(text))
            {
                this.Message = $"{this.NickName}\n{msg}";
                DA.SetDataTree(0, tree);
                return;
            }

            string delimiter = text.Contains("\t") ? "\t" : ",";
            
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var grid = new List<List<IGH_Goo>>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var row = new List<IGH_Goo>();
                foreach (var item in line.Split(new[] { delimiter }, StringSplitOptions.None))
                {
                    row.Add(SmartCast(item));
                }
                grid.Add(row);
            }

            if (grid.Count == 0)
            {
                msg = "No valid data found.";
                this.Message = $"{this.NickName}\n{msg}";
                DA.SetDataTree(0, tree);
                return;
            }

            if (byColumn)
            {
                int colCount = grid.Max(r => r.Count);
                for (int c = 0; c < colCount; c++)
                {
                    var path = new GH_Path(c);
                    for (int r = 0; r < grid.Count; r++)
                    {
                        if (c < grid[r].Count)
                        {
                            tree.Append(grid[r][c], path);
                        }
                    }
                }
                msg = $"Output: {colCount} Columns (Branches)";
            }
            else
            {
                for (int r = 0; r < grid.Count; r++)
                {
                    var path = new GH_Path(r);
                    for (int c = 0; c < grid[r].Count; c++)
                    {
                        tree.Append(grid[r][c], path);
                    }
                }
                msg = $"Output: {grid.Count} Rows (Branches)";
            }

            this.Message = $"{this.NickName}\n{msg}";
            DA.SetDataTree(0, tree);
        }

        private IGH_Goo SmartCast(string val)
        {
            val = val.Trim();
            if (double.TryParse(val, out double f))
            {
                if (f % 1 == 0)
                {
                    return new GH_Integer((int)f);
                }
                return new GH_Number(f);
            }
            return new GH_String(val);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("ParseTable.png");
            }
        }

        public override Guid ComponentGuid => new Guid("084e55e5-f5be-443b-826f-40e107f9c882");
    }
}
