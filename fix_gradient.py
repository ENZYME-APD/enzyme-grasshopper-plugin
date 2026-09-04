import re

with open("Utils/AutoWireHelper.cs", "r") as f:
    text = f.read()

old_line = "swatch.Attributes.Pivot = new System.Drawing.PointF(merge.Attributes.Pivot.X - 120, merge.Attributes.Pivot.Y - (colors.Length * 24 / 2) + i * 24);"
new_line = "swatch.Attributes.Pivot = new System.Drawing.PointF(merge.Attributes.Pivot.X - 165, merge.Attributes.Pivot.Y - 63.3f + i * 24);"

text = text.replace(old_line, new_line)

with open("Utils/AutoWireHelper.cs", "w") as f:
    f.write(text)

with open("Components/GradientGenerator.cs", "r") as f:
    text2 = f.read()

# Update the WireMergeWithSwatches call
text2 = re.sub(
    r'Enzyme\.Utils\.AutoWireHelper\.WireMergeWithSwatches\(this, document, 0, defaultColors, [-\d]+, [-\d]+\);',
    'Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 0, defaultColors, 121, -10);',
    text2
)

# Update the WireIntegerSlider call
text2 = re.sub(
    r'Enzyme\.Utils\.AutoWireHelper\.WireIntegerSlider\(this, document, 1, 2, 100, 10, [-\d]+, [-\d]+\);',
    'Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 1, 2, 100, 10, 247, 58);',
    text2
)

with open("Components/GradientGenerator.cs", "w") as f:
    f.write(text2)

