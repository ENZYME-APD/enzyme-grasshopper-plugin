import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# Insert the boolean declaration
old_dec = """            int dirs = 2, lanes = 2;
            double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0, filletRadius = 5.0;"""

new_dec = """            int dirs = 2, lanes = 2;
            double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0, filletRadius = 5.0;
            bool colorize = true;"""

content = content.replace(old_dec, new_dec)

# Add it to DA.GetData
old_da = """            DA.GetData(8, ref angle);
            DA.GetData(9, ref subDist);
            DA.GetData(10, ref filletRadius);"""

new_da = """            DA.GetData(8, ref angle);
            DA.GetData(9, ref subDist);
            DA.GetData(10, ref filletRadius);
            DA.GetData(11, ref colorize);"""

content = content.replace(old_da, new_da)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
