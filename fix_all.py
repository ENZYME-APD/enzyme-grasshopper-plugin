import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# --- 1. Fix AddedToDocument ---
start_idx = content.find("public override void AddedToDocument(GH_Document document)")
end_idx = content.find("protected override void RegisterOutputParams", start_idx)

if start_idx != -1 and end_idx != -1:
    new_added = """public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 1, 2, 2, 330, -60);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 1, 6, 2, 330, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 4, 1.0, 10.0, 3.5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 5, 0.0, 5.0, 1.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 6, 1.0, 20.0, 5.0, 330, 100);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 7, 5.0, 100.0, 20.0, 330, 140);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 8, 10, 80, 45, 330, 180);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 9, 0.5, 10.0, 2.0, 330, 220);
                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 10, true, 330, 260);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", -250, -60);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "mesh", -250, -20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "curve", -250, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", -250, 60);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", -250, 100);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "mesh", -250, 140);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 6, "mesh", -250, 180);
            }
        }

        """
    content = content[:start_idx] + new_added + content[end_idx:]

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
