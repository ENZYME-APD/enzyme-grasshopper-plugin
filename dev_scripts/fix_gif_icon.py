with open('Components/GifCompiler.cs', 'r') as f:
    content = f.read()
content = content.replace("protected override System.Drawing.Bitmap Icon => null;", 'protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("GifCompiler.png");')
with open('Components/GifCompiler.cs', 'w') as f:
    f.write(content)
