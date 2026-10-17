import re

with open('Components/DataVisualizer.cs', 'r') as f:
    content = f.read()

# 1. Update RegisterInputParams
old_reg = 'pManager.AddNumberParameter("Bar Thickness", "W", "Thickness for Bar Chart (Type 0 only)", GH_ParamAccess.item, 0.5);'
new_reg = 'pManager.AddNumberParameter("Bar Thickness", "W", "Thickness for Bar Chart (Type 0 only)", GH_ParamAccess.list);'
content = content.replace(old_reg, new_reg)

# 2. Update SolveInstance GetData
old_get = """            double thickness = 0.5;
            DA.GetData(5, ref thickness);"""
new_get = """            List<double> thicknesses = new List<double>();
            if (!DA.GetDataList(5, thicknesses) || thicknesses.Count == 0)
            {
                thicknesses.Add(0.5);
            }"""
content = content.replace(old_get, new_get)

# 3. Update Loop
old_loop = "Mesh m = CreateGeometry(type, p, mappedSize, thickness);"
new_loop = """double currentThickness = thicknesses[i % thicknesses.Count];
                Mesh m = CreateGeometry(type, p, mappedSize, currentThickness);"""
content = content.replace(old_loop, new_loop)

with open('Components/DataVisualizer.cs', 'w') as f:
    f.write(content)
