with open('Components/MapToGradient.cs', 'r') as f:
    content = f.read()

content = content.replace("protected override System.Drawing.Bitmap Icon => null; // Uses default fallback", 'protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("MapToGradient.png");')

with open('Components/MapToGradient.cs', 'w') as f:
    f.write(content)
