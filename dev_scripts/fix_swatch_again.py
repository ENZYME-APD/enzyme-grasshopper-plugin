import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

content = content.replace("swatch.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 60, preview.Attributes.Pivot.Y + 20);", 
                          "swatch.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 80, preview.Attributes.Pivot.Y + 25);")

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)
