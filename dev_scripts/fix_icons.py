with open('Components/CreateNamedViews.cs', 'r') as f:
    content = f.read()
content = content.replace("protected override System.Drawing.Bitmap Icon => null;", 'protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("CreateNamedViews.png");')
with open('Components/CreateNamedViews.cs', 'w') as f:
    f.write(content)

with open('Components/ChangePulse.cs', 'r') as f:
    content = f.read()
content = content.replace("protected override System.Drawing.Bitmap Icon => null; // Fallback to default icon", 'protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("ChangePulse.png");')
with open('Components/ChangePulse.cs', 'w') as f:
    f.write(content)
