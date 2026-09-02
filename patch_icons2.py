import re

def fix_icon_loader(filepath):
    with open(filepath, 'r') as f:
        content = f.read()

    content = content.replace("Enzyme.Utils.IconLoader.Load", "Enzyme.IconLoader.Load")
    
    with open(filepath, 'w') as f:
        f.write(content)

fix_icon_loader('Components/SortCurvesByAxis.cs')
fix_icon_loader('Components/PixelatedSurface.cs')
