import re

with open("Components/MeshHeightAnalysis.cs", "r") as f:
    mha = f.read()

mha = mha.replace("public override bool Read(GH_IReader reader)", "public override bool Read(GH_IO.Serialization.GH_IReader reader)")

with open("Components/MeshHeightAnalysis.cs", "w") as f:
    f.write(mha)

with open("Components/TerrainSections.cs", "r") as f:
    ts = f.read()

# Replace the null icon
ts = ts.replace('''        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return Enzyme.Utils.IconLoader.Load("TERRAIN SECTIONS.png"); }
        }''',
'''        protected override System.Drawing.Bitmap Icon
        {
            get { return Enzyme.Utils.IconLoader.Load("TERRAIN SECTIONS.png"); }
        }''')

with open("Components/TerrainSections.cs", "w") as f:
    f.write(ts)
