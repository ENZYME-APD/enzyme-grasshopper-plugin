with open('Components/CanvasAuditor.cs', 'r') as f:
    content = f.read()

orig = "protected override System.Drawing.Bitmap Icon => null; // Fallback"
new_icon = 'protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("CanvasAuditor.png");'
content = content.replace(orig, new_icon)

with open('Components/CanvasAuditor.cs', 'w') as f:
    f.write(content)
