import re

with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

# Add AddedToDocument and Context Menu methods
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

            // 1: Radius
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 1, 0.1, 500.0, 100.0, 200, -100);
            // 2: Elevation
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 2, 0.0, 500.0, 50.0, 200, -80);
            // 3: Frames
            Enzyme.Utils.AutoWireHelper.WireSliderInt(this, doc, 3, 1, 360, 36, 200, -60);
            
            // 4: Directory
            Enzyme.Utils.AutoWireHelper.WirePanel(this, doc, 4, "C:\\\\Turntable", 200, -40, 150, 20);
            
            // 8: Transparent
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 8, false, 200, 40);
            // 9: Scale Items
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 9, false, 200, 60);
            
            // 10: Run
            Enzyme.Utils.AutoWireHelper.WireButton(this, doc, 10, 200, 80);
        }

        protected override System.Drawing.Bitmap Icon"""

content = content.replace("        protected override System.Drawing.Bitmap Icon", autowire_methods)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
