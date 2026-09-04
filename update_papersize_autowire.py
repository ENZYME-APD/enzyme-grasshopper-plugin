import re

with open("Components/PaperSizeToPixels.cs", "r") as f:
    ts = f.read()

old_added = '''        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 1, true, 210, 0);
            }
        }'''

new_added = '''        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 1, true, 210, 0);

                var vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 180, this.Attributes.Pivot.Y - 20);
                vl.ListItems.Clear();
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A0", "\\"A0\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A1", "\\"A1\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A2", "\\"A2\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A3", "\\"A3\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A4", "\\"A4\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A5", "\\"A5\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 FHD", "\\"16:9 FHD\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 QHD", "\\"16:9 QHD\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 4K", "\\"16:9 4K\\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("1:1 Instagram", "\\"1:1 INSTAGRAM\\""));
                vl.SelectItem(4); // Select A4

                document.AddObject(vl, false);
                this.Params.Input[0].AddSource(vl);
            }
        }'''

ts = ts.replace(old_added, new_added)

with open("Components/PaperSizeToPixels.cs", "w") as f:
    f.write(ts)
