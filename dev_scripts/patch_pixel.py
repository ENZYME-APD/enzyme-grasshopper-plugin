import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

# Remove the invalid block from RegisterInputParams
start_idx = content.find("            if (!hasSources)")
end_idx = content.find("        private bool hasSources = false;")

content = content[:start_idx] + content[end_idx:]

# Rewrite AddedToDocument to include the autowire block
added_to_doc = """        private bool hasSources = false;
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "C:\\\\path\\\\to\\\\image.jpg", 300, -220, 150, 40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 1, 100, 20, 330, -80);
                // Oh wait, param 1 is Surface, not Slider! I should use param 2 and 3 for U and V.
                // Actually let me fix the auto-wiring indices correctly:
                // 0: Image Path
                // 1: Surface
                // 2: U
                // 3: V
                // 4: Colors
                // 5: Accent Color
                // 6: Jitter Pct
                // 7: Accent Pct
                // 8: Inset Factor
                // 9: Bake
                // 10: Bake Name
                
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "C:\\\\path\\\\to\\\\image.jpg", 300, -180, 150, 40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 1, 100, 20, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 1, 100, 20, 330, -60);
                
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(20, 20, 20),
                    System.Drawing.Color.FromArgb(100, 100, 100),
                    System.Drawing.Color.FromArgb(200, 200, 200),
                    System.Drawing.Color.FromArgb(250, 250, 250)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 4, colors, 150, 20);
                
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 5, System.Drawing.Color.FromArgb(255, 0, 0), 210, 80);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 100.0, 30.0, 330, 120);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 100.0, 10.0, 330, 160);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 1.0, 0.94, 330, 200);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 9, 210, 240);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 0, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 70, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "mesh", 220, 140);
            }
        }
"""
target_start = content.find("        private bool hasSources = false;")
target_end = content.find("        protected override void RegisterOutputParams", target_start)

content = content[:target_start] + added_to_doc + content[target_end:]

with open('Components/PixelatedSurface.cs', 'w') as f:
    f.write(content)
