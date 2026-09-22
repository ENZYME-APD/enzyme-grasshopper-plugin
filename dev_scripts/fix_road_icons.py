with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()
if "System.Drawing.Bitmap Icon" not in content:
    content = content.replace("public override Guid ComponentGuid", 'protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("RoadGenerator.png");\n\n        public override Guid ComponentGuid')
with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)

with open('Components/AutoGrade.cs', 'r') as f:
    content = f.read()
if "System.Drawing.Bitmap Icon" not in content:
    content = content.replace("public override Guid ComponentGuid", 'protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("AutoGrade.png");\n\n        public override Guid ComponentGuid')
with open('Components/AutoGrade.cs', 'w') as f:
    f.write(content)
