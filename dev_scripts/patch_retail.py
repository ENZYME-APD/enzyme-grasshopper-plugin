import re

with open('Components/ModRetail.cs', 'r') as f:
    content = f.read()

start_str = "if (!hasSources)"
end_str = "        }"

start_idx = content.find(start_str)
end_idx = content.find("        protected override void RegisterInputParams", start_idx)

lines = []
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 6.0, 330, -220);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 5.0, 0.0, 330, -180);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 10.0, 6.0, 330, -140);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 2.0, 0.4, 330, -100);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, false, 210, -60);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 7, "1111111111\\n1000100010\\n0000000000", 250, -20, 120, 60);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 8, "0111", 250, 40, 100, 25);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 5.0, 2.5, 330, 80);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 10, "100", 250, 120, 100, 25);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 160);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 200);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 13, 0.0, 5.0, 1.5, 330, 240);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(150, 200, 255), 220, -188);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(250, 250, 250), 220, -113);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(250, 250, 250), 220, -38);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 3, System.Drawing.Color.FromArgb(50, 50, 50), 220, 37);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 4, System.Drawing.Color.FromArgb(200, 200, 200), 220, 112);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "curve", 220, 187);')


new_block = "if (!hasSources)\n            {\n" + "\n".join(lines) + "\n            }\n        }\n\n"

content = content[:start_idx] + new_block + content[end_idx:]

with open('Components/ModRetail.cs', 'w') as f:
    f.write(content)
