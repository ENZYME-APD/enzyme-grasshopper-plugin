import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# 1. Add Input
input_reg = r'pManager\.AddNumberParameter\("Fillet".*?\);'
new_input = 'pManager.AddNumberParameter("Fillet", "F", "Corner fillet radius (auto-clamped for small segments)", GH_ParamAccess.item, 5.0);\n            pManager.AddBooleanParameter("Colorize", "Col", "Colorize terrain/volumes for Cut (Red) and Fill (Blue)", GH_ParamAccess.item, true);'
content = re.sub(input_reg, new_input, content)

# 2. Add Wire
wire_reg = r'Enzyme\.Utils\.AutoWireHelper\.WireSlider\(this, document, 10, 0\.0, 20\.0, 5\.0, 330, 260\);'
new_wire = 'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 10, 0.0, 20.0, 5.0, 330, 260);\n                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 11, true, 330, 300);'
content = re.sub(wire_reg, new_wire, content)

# 3. Add to SolveInstance variables
vars_reg = r'double laneW = 3\.5.*?filletRadius = 5\.0;'
new_vars = 'double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0, filletRadius = 5.0;\n            bool colorize = true;'
content = re.sub(vars_reg, new_vars, content)

da_reg = r'DA\.GetData\(10, ref filletRadius\);'
new_da = 'DA.GetData(10, ref filletRadius);\n            DA.GetData(11, ref colorize);'
content = re.sub(da_reg, new_da, content)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
