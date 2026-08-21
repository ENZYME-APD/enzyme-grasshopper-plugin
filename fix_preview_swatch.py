import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

# We want to change where the swatch is placed relative to the preview component.
# Current: swatch.Attributes.Pivot = new PointF(preview.Attributes.Pivot.X - 90, preview.Attributes.Pivot.Y);
# Change to: swatch.Attributes.Pivot = new PointF(preview.Attributes.Pivot.X - 60, preview.Attributes.Pivot.Y + 25);

content = content.replace("swatch.Attributes.Pivot = new PointF(preview.Attributes.Pivot.X - 90, preview.Attributes.Pivot.Y);", 
                          "swatch.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 60, preview.Attributes.Pivot.Y + 20);")

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)

