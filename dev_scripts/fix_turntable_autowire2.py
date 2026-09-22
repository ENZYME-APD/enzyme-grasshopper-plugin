import re

with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

autowire_methods = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                AutoWireDefaultInputs();
            }
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) => AutoWireDefaultInputs());
        }

        private void AutoWireDefaultInputs()
        {
            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 1, 0.1, 500.0, 100.0, 200, -100);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 2, 0.0, 500.0, 50.0, 200, -80);
            Enzyme.Utils.AutoWireHelper.WireSliderInt(this, doc, 3, 1, 360, 36, 200, -60);
            Enzyme.Utils.AutoWireHelper.WirePanel(this, doc, 4, "C:\\\\Turntable", 200, -40, 150, 20);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 8, false, 200, 40);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 9, false, 200, 60);
            Enzyme.Utils.AutoWireHelper.WireButton(this, doc, 10, 200, 80);
        }

        protected override Bitmap Icon"""

content = content.replace("        protected override Bitmap Icon", autowire_methods)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
