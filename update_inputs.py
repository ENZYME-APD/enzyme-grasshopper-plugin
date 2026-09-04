import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

new_inputs = """        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Base Geometry", "Base", "Planar Surface, Brep, or closed Curve to fill.", GH_ParamAccess.item);
            pManager.AddPointParameter("Setout Point", "Setout", "Optional origin point for the grid alignment. If not supplied, the centroid is used.", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Grid Type", "Grid Type", "Grid type: rectangular, offset_rectangular, hexagonal, triangular", GH_ParamAccess.item, "rectangular");
            pManager.AddNumberParameter("Cell Width", "X Dim", "Cell width", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Cell Height", "Y Dim", "Cell height", GH_ParamAccess.item, 1.0);
        }"""

content = re.sub(r'protected override void RegisterInputParams.*?\}', new_inputs, content, flags=re.DOTALL)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)
