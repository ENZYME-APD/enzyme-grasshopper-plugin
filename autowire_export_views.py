import re

with open("Components/ExportViews.cs", "r") as f:
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
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 210, -180);
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 4, "C:\\\\", 210, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 150);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, true, 210, 210);

                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 150, 0, 300, 300);
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
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 210, -180);
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 4, "C:\\\\", 210, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 150);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, true, 210, 210);

                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 150, 0, 300, 300);

                // Autowire Document Size to Pixels component
                var paperComp = new Enzyme.Components.PaperSizeToPixels();
                paperComp.CreateAttributes();
                
                // Position it relatively
                float px = this.Attributes.Pivot.X - 350;
                float py = this.Attributes.Pivot.Y + 40;
                paperComp.Attributes.Pivot = new System.Drawing.PointF(px, py);
                
                document.AddObject(paperComp, false);
                
                // Wire outputs of PaperSize to ExportViews
                this.Params.Input[8].AddSource(paperComp.Params.Output[0]); // Width
                this.Params.Input[9].AddSource(paperComp.Params.Output[1]); // Height
                this.Params.Input[10].AddSource(paperComp.Params.Output[2]); // DPI
            }
        }'''

ts = ts.replace(old_added, new_added)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
