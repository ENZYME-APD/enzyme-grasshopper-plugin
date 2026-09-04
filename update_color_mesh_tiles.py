import re

with open("Components/ColorMeshTiles.cs", "r") as f:
    content = f.read()

# 1. Autowiring
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "mesh", 220, 90);',
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", 220, 90);'
)

# 2. Input parameter renaming
content = content.replace(
    'pManager.AddCurveParameter("polylines", "P", "Tree of polylines", GH_ParamAccess.tree);',
    'pManager.AddCurveParameter("Grid Cells", "Cells", "Tree of grid cells", GH_ParamAccess.tree);'
)

# 3. Icon
old_icon = """        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("ColorMeshTiles.png");
            }
        }"""
new_icon = """        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Pixel Gradient.png");
            }
        }"""
content = content.replace(old_icon, new_icon)

with open("Components/ColorMeshTiles.cs", "w") as f:
    f.write(content)
