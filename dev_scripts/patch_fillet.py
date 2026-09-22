import re

with open('Components/FilletRules.cs', 'r') as f:
    content = f.read()

start_str = "if (!hasSources)"
end_str = "        }"

start_idx = content.find(start_str)
end_idx = content.find("        protected override void RegisterInputParams", start_idx)

lines = []
lines.append('                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "0", 250, -80, 80, 25);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 1, "Program\\nProgram\\nBuilding", 250, -40, 150, 70);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 2, "Residential-02\\nOffice\\nBuilding_11", 250, 50, 150, 70);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMergeWithSliders(this, document, 3, new double[] { 6.0, 2.0, 4.0 }, 150, 140);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 4, "True\\nFalse\\nTrue", 250, 230, 150, 70);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -34, 180, 22);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 11, 180, 22);')


new_block = "if (!hasSources)\n            {\n" + "\n".join(lines) + "\n            }\n        }\n\n"

content = content[:start_idx] + new_block + content[end_idx:]

with open('Components/FilletRules.cs', 'w') as f:
    f.write(content)
