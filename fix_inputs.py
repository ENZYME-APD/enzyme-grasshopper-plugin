import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# Add Colorize input
old_input = 'pManager.AddNumberParameter("Subdivide", "SD", "Resolution along the road for terrain modification", GH_ParamAccess.item, 2.0);'
new_input = 'pManager.AddNumberParameter("Subdivide", "SD", "Resolution along the road for terrain modification", GH_ParamAccess.item, 2.0);\n            pManager.AddBooleanParameter("Colorize", "Col", "Colorize terrain/volumes for Cut (Red) and Fill (Blue)", GH_ParamAccess.item, true);'
content = content.replace(old_input, new_input)

# Add Wire
old_wire = 'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.5, 10.0, 2.0, 330, 220);'
new_wire = 'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.5, 10.0, 2.0, 330, 220);\n                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 10, true, 330, 260);'
content = content.replace(old_wire, new_wire)

# Add variables
old_vars = 'double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0;'
new_vars = 'double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0;\n            bool colorize = true;'
content = content.replace(old_vars, new_vars)

# Fix duplicated variable declarations that I might have mistakenly added earlier with sed
content = content.replace("""            int dirs = 2, lanes = 2;
            double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0, filletRadius = 5.0;
            bool colorize = true;""", "            int dirs = 2, lanes = 2;\n            double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0;")


# Add DA.GetData
old_da = 'DA.GetData(9, ref subDist);'
new_da = 'DA.GetData(9, ref subDist);\n            DA.GetData(10, ref colorize);'
content = content.replace(old_da, new_da)

content = content.replace("DA.GetData(10, ref filletRadius);\n            DA.GetData(11, ref colorize);", "")

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
