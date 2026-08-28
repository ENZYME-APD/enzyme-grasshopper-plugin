import re

with open('Components/ModFins.cs', 'r') as f:
    content = f.read()

start_str = "if (!hasSources)"
end_str = "        }"

start_idx = content.find(start_str)
end_idx = content.find("        protected override void RegisterInputParams", start_idx)

lines = []
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 5.0, 0.75, 330, -100);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 2.0, 0.30, 330, -60);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 2.0, 1.00, 330, -20);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 5, "01101101\\n11010110", 250, 40, 100, 40);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 6, "01010110", 250, 100, 100, 25);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 7, true, 210, 140);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(150, 200, 255), 220, -120);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(200, 200, 200), 220, -45);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(50, 50, 50), 220, 30);')

new_block = "if (!hasSources)\n            {\n" + "\n".join(lines) + "\n            }\n        }\n\n"

content = content[:start_idx] + new_block + content[end_idx:]

with open('Components/ModFins.cs', 'w') as f:
    f.write(content)
