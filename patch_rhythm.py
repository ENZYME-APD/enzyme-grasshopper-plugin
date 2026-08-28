import re

with open('Components/ModRhythmBalc.cs', 'r') as f:
    content = f.read()

start_str = "if (!hasSources)"
end_str = "        }"

start_idx = content.find(start_str)
end_idx = content.find("        protected override void RegisterInputParams", start_idx)

lines = []
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 3.5, 330, -220);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 3.0, 2.9, 330, -180);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 3.0, 1.5, 330, -140);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 2.0, 1.0, 330, -100);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 6, "0110101\\n1010110", 250, -60, 100, 40);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 7, "10", 250, 0, 100, 25);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 8, false, 210, 40);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 9, false, 210, 80);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10, true, 210, 120);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 160);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 12, 0.0, 5.0, 3.0, 330, 200);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 240);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(180, 180, 180), 220, -240);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(150, 150, 150), 220, -165);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(230, 230, 230), 220, -90);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 3, System.Drawing.Color.FromArgb(150, 200, 255), 220, -15);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 4, System.Drawing.Color.FromArgb(250, 250, 250), 220, 60);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 5, System.Drawing.Color.FromArgb(250, 250, 250), 220, 135);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 6, System.Drawing.Color.FromArgb(250, 250, 250), 220, 210);')

new_block = "if (!hasSources)\n            {\n" + "\n".join(lines) + "\n            }\n        }\n\n"

content = content[:start_idx] + new_block + content[end_idx:]

with open('Components/ModRhythmBalc.cs', 'w') as f:
    f.write(content)
