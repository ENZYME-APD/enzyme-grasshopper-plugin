import re

with open("Components/CanvasStateManager.cs", "r") as f:
    text = f.read()

if "using System.Windows.Forms;" not in text:
    text = "using System.Windows.Forms;\n" + text

menu_code = """
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-fill Load ValueList", (s, e) => AutoFillValueList());
        }

        private void AutoFillValueList()
        {
            if (_states.Count == 0) return;

            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            var loadParam = this.Params.Input[3];
            Grasshopper.Kernel.Special.GH_ValueList vl = null;

            foreach (var source in loadParam.Sources)
            {
                if (source is Grasshopper.Kernel.Special.GH_ValueList v)
                {
                    vl = v;
                    break;
                }
            }

            bool isNew = false;
            if (vl == null)
            {
                vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 60);
                vl.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.DropDown;
                isNew = true;
            }

            vl.ListItems.Clear();
            foreach (var key in _states.Keys)
            {
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(key, $"\\\"{key}\\\""));
            }

            if (isNew)
            {
                doc.AddObject(vl, false);
                loadParam.AddSource(vl);
            }

            vl.ExpireSolution(true);
        }

        public override void AddedToDocument(GH_Document document)"""

text = text.replace("        public override void AddedToDocument(GH_Document document)", menu_code)

with open("Components/CanvasStateManager.cs", "w") as f:
    f.write(text)

