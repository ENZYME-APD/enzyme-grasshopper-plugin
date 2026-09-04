import re
with open("Components/FlowHeat.cs", "r") as f:
    text = f.read()

text = text.replace(
    'Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -38);',
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -38);'
)

with open("Components/FlowHeat.cs", "w") as f:
    f.write(text)
