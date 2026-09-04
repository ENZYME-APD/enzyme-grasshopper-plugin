with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

autowire_code = '''
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 210, -180);
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 2, "C:\\\\", 210, -120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10, false, 210, 50);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 80);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 110);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, true, 210, 140);

                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 150, 0, 300, 300);
            }
        }

        public override void AppendAdditionalMenuItems'''

ts = ts.replace("        // Add right-click menu option to auto-fill value list\n        public override void AppendAdditionalMenuItems", autowire_code)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
