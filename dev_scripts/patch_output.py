import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "mesh", 220, 140);',
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", 220, 140);'
)

with open('Components/PixelatedSurface.cs', 'w') as f:
    f.write(content)
