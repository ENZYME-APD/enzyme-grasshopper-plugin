with open("Components/MeshHeightAnalysis.cs", "r") as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    if "Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 10" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 11" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 12" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 13" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 14" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 15" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 16" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 17" in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 18" in line: continue
    
    new_lines.append(line)

with open("Components/MeshHeightAnalysis.cs", "w") as f:
    f.writelines(new_lines)
