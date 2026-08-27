import re

with open('Components/TerrainGeneratorPro.cs', 'r') as f:
    content = f.read()

# Define the new wiring lines
lines = []
y = -440

lines.append(f'                Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 0, "curve", 180, {y});') # Boundary
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 200, 100.0, 330, {y});') # MaxHeight
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 0.0, 330, {y});') # MinHeight
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 84, 42, 330, {y});') # Seed
y += 40
# PatternSizeXY panel (Multiline)
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 4, "150\\n50\\n20", 250, {y}, 100, 60);') 
y += 70
# PatternHeightZ panel (Multiline)
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 5, "1.0\\n0.3\\n0.1", 250, {y}, 100, 60);')
y += 70
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 2.0, 1.0, 330, {y});') # ContourStep
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 10.0, 5.0, 330, {y});') # MainStep
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 200, 100, 330, {y});') # Resolution
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10, false, 210, {y});') # UseSlopeColor
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 11, System.Drawing.Color.DarkGray, 210, {y});') # SlopeColor
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 12, 0.0, 60, 30.0, 330, {y});') # SlopeAngle
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 13, new string[]{{"Realistic Soft Hills", "Ridged/Cellular Pattern"}}, new string[]{{"0", "1"}}, 300, {y});') # TerrainStyle
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, false, 210, {y});') # Solid
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 15, System.Drawing.Color.DimGray, 210, {y});') # BaseCol
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 16, 0.0, 1.0, 0.0, 330, {y});') # TreeMsk
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 17, 0.0, 1.0, 0.0, 330, {y});') # TreeDns
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 18, 0.0, 24690, 12345, 330, {y});') # TreeSeed
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 19, 0.0, 1.0, 0.15, 330, {y});') # TreeZMin
y += 40
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 20, 0.0, 1.0, 0.85, 330, {y});') # TreeZMax

# Outputs
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 300, -135);')
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 1, System.Drawing.Color.Gray, 0.05, 300, -45);')
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 2, System.Drawing.Color.Black, 0.15, 300, 45);')
lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 300, 135);')

new_block = "\n".join(lines)

# Regex to replace the if (!hasSources) block
pattern = re.compile(r'if \(\!hasSources\)\s*\{[^\}]+\}', re.MULTILINE)
replacement = "if (!hasSources)\n            {\n" + new_block + "\n            }"

content = pattern.sub(replacement, content)

with open('Components/TerrainGeneratorPro.cs', 'w') as f:
    f.write(content)
