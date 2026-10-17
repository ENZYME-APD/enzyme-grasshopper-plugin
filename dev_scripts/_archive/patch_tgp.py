import re

with open('Components/TerrainGeneratorPro.cs', 'r') as f:
    content = f.read()

old_outputs = """                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -83);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "curve", 220, -8);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "curve", 220, 37);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 220, 82);"""

new_outputs = """                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 300, -135);
                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 1, System.Drawing.Color.Gray, 0.05, 300, -45);
                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 2, System.Drawing.Color.Black, 0.15, 300, 45);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 300, 135);"""

content = content.replace(old_outputs, new_outputs)

with open('Components/TerrainGeneratorPro.cs', 'w') as f:
    f.write(content)
