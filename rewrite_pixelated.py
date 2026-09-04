import re

with open("Components/PixelatedSurface.cs", "r") as f:
    content = f.read()

new_inputs = """        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Image Path", "Img", "Absolute path to the image file", GH_ParamAccess.item);
            pManager.AddCurveParameter("Grid Cells", "Cells", "Pre-generated grid cells", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Mapping Plane", "Plane", "Optional plane for UV mapping. Auto-detected if empty.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "C", "List of colors mapped to brightness (dark to light)", GH_ParamAccess.list);
            pManager.AddColourParameter("Accent Color", "AC", "Accent color", GH_ParamAccess.item, System.Drawing.Color.Empty);
            pManager.AddNumberParameter("Jitter Pct", "J", "Jitter percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Accent Pct", "AP", "Accent percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Inset Factor", "I", "Inset factor (0.0-1.0)", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Bake", "B", "Bake trigger", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Bake Name", "BN", "Bake group/layer name", GH_ParamAccess.item, "");
            pManager.AddIntegerParameter("Rotate 90", "R90", "Rotate image by multiples of 90 degrees (1=90, 2=180, 3=270)", GH_ParamAccess.item, 0);

            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
        }"""
content = re.sub(r'protected override void RegisterInputParams.*?\}', new_inputs, content, flags=re.DOTALL)

with open("Components/PixelatedSurface.cs", "w") as f:
    f.write(content)
