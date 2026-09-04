import re

with open("Components/TerrainGeneratorPro.cs", "r") as f:
    content = f.read()

content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 1, System.Drawing.Color.Gray, 0.05, 300, -45);',
    'Enzyme.Utils.AutoWireHelper.WireHumanCurvePreview(this, document, 1, System.Drawing.Color.Gray, 0.35, 300, -45);'
)

content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 2, System.Drawing.Color.Black, 0.15, 300, 45);',
    'Enzyme.Utils.AutoWireHelper.WireHumanCurvePreview(this, document, 2, System.Drawing.Color.Black, 0.35, 300, 115);'
)

# Wait, in the image, the second preview is lower. Let's space them out a bit.
# The original was at 300, -45 and 300, 45.
# If I use 300, -45 and 300, 105, they won't overlap as much.
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 300, 135);',
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 300, 220);'
)

with open("Components/TerrainGeneratorPro.cs", "w") as f:
    f.write(content)
