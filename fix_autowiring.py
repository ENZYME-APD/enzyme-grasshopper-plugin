import re

# 1. Update ColorMeshTiles.cs
with open("Components/ColorMeshTiles.cs", "r") as f:
    cmt_content = f.read()

cmt_old_wire = """                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 100.0, 30.0, 330, 30);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 100.0, 40.0, 330, 70);"""
cmt_new_wire = """                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 4, 0, 100, 30, 330, 30);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 5, 0, 100, 40, 330, 70);"""

cmt_content = cmt_content.replace(cmt_old_wire, cmt_new_wire)

with open("Components/ColorMeshTiles.cs", "w") as f:
    f.write(cmt_content)

# 2. Update PixelatedSurface.cs
with open("Components/PixelatedSurface.cs", "r") as f:
    ps_content = f.read()

ps_old_added = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 0, "", 300, -180);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 1, 100, 20, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 1, 100, 20, 330, -60);
                
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(20, 20, 20),
                    System.Drawing.Color.FromArgb(100, 100, 100),
                    System.Drawing.Color.FromArgb(200, 200, 200),
                    System.Drawing.Color.FromArgb(250, 250, 250)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 4, colors, 150, 20);
                
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 5, System.Drawing.Color.FromArgb(255, 0, 0), 210, 80);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 6, 0, 100, 30, 330, 120);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 7, 0, 100, 10, 330, 160);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 1.0, 0.94, 330, 200);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 9, 210, 240);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 0, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 70, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", 220, 140);
            }
        }"""

ps_new_added = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 0, "", 300, -180);
                
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(20, 20, 20),
                    System.Drawing.Color.FromArgb(100, 100, 100),
                    System.Drawing.Color.FromArgb(200, 200, 200),
                    System.Drawing.Color.FromArgb(250, 250, 250)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 3, colors, 150, 20);
                
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 4, System.Drawing.Color.FromArgb(255, 0, 0), 210, 80);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 5, 0, 100, 30, 330, 120);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 6, 0, 100, 10, 330, 160);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 1.0, 0.94, 330, 200);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 8, 210, 240);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 10, -180, 180, 0, 330, 280);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 0, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 70, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", 220, 140);
            }
        }"""

ps_content = ps_content.replace(ps_old_added, ps_new_added)

with open("Components/PixelatedSurface.cs", "w") as f:
    f.write(ps_content)
