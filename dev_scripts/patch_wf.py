import re

with open('Components/WaterFlow.cs', 'r') as f:
    content = f.read()

start_str = "if (!hasSources)"
end_str = "        }"

start_idx = content.find(start_str)
end_idx = content.find("        protected override void RegisterInputParams", start_idx)

lines = []
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 10.0, 5.0, 330, -40);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 5.0, 330, 0);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 10.0, 10, 330, 40);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 0, System.Drawing.Color.DeepSkyBlue, 0.06, 300, -30);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "point", 300, 50);')

new_block = "if (!hasSources)\n            {\n" + "\n".join(lines) + "\n            }\n        }\n\n"

content = content[:start_idx] + new_block + content[end_idx:]

with open('Components/WaterFlow.cs', 'w') as f:
    f.write(content)
