import re

with open('Components/ConfigJson.cs', 'r') as f:
    content = f.read()

start_str = "if (!hasSources)"
end_str = "        }"

start_idx = content.find(start_str)
end_idx = content.find("        protected override void RegisterInputParams", start_idx)

lines = []
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 0, "Office\\nResidential\\nServ.Apt\\nRetail\\nAmenities\\nHotel\\nPodium\\nParking\\nDefault", 250, -80, 100, 150);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 1, "30000\\n56500\\n5000\\n150000\\n300000\\n10000\\n15000\\n60000\\n0", 140, -80, 100, 150);')
lines.append('                var colors = new System.Drawing.Color[] {')
lines.append('                    System.Drawing.Color.FromArgb(80, 180, 220),')
lines.append('                    System.Drawing.Color.FromArgb(160, 120, 180),')
lines.append('                    System.Drawing.Color.FromArgb(160, 220, 80),')
lines.append('                    System.Drawing.Color.FromArgb(250, 180, 100),')
lines.append('                    System.Drawing.Color.FromArgb(230, 130, 170),')
lines.append('                    System.Drawing.Color.FromArgb(110, 130, 200),')
lines.append('                    System.Drawing.Color.FromArgb(230, 100, 70),')
lines.append('                    System.Drawing.Color.FromArgb(190, 180, 160),')
lines.append('                    System.Drawing.Color.FromArgb(255, 255, 255)')
lines.append('                };')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 2, colors, 150, 120);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -34, 180, 22);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 11, 180, 22);')

new_block = "if (!hasSources)\n            {\n" + "\n".join(lines) + "\n            }\n        }\n\n"

content = content[:start_idx] + new_block + content[end_idx:]

with open('Components/ConfigJson.cs', 'w') as f:
    f.write(content)
