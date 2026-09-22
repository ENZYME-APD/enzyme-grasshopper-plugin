import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

# Replace RegisterInputParams
start_reg = content.find("        protected override void RegisterInputParams")
end_reg = content.find("        private bool hasSources = false;")

new_reg = """        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Image Path", "Img", "Absolute path to the image file", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("Surface", "Srf", "Base surface to pixelate", GH_ParamAccess.item);
            pManager.AddIntegerParameter("U_Subdivisions", "U", "Number of tiles in U direction", GH_ParamAccess.item, 20);
            pManager.AddIntegerParameter("V_Subdivisions", "V", "Number of tiles in V direction", GH_ParamAccess.item, 20);
            pManager.AddColourParameter("Colors", "C", "List of colors mapped to brightness (dark to light)", GH_ParamAccess.list);
            pManager.AddColourParameter("Accent Color", "AC", "Accent color", GH_ParamAccess.item, Color.Empty);
            pManager.AddNumberParameter("Jitter Pct", "J", "Jitter percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Accent Pct", "AP", "Accent percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Inset Factor", "I", "Inset factor (0.0-1.0)", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Bake", "B", "Bake trigger", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Bake Name", "BN", "Bake group/layer name", GH_ParamAccess.item, "");

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[10].Optional = true;
        }

"""

content = content[:start_reg] + new_reg + content[end_reg:]

# Replace AddedToDocument
start_add = content.find("        public override void AddedToDocument(GH_Document document)")
end_add = content.find("        protected override void RegisterOutputParams")

new_add = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "C:\\\\path\\\\to\\\\image.jpg", 300, -180, 150, 40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 1, 100, 20, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 1, 100, 20, 330, -60);
                
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(20, 20, 20),
                    System.Drawing.Color.FromArgb(100, 100, 100),
                    System.Drawing.Color.FromArgb(200, 200, 200),
                    System.Drawing.Color.FromArgb(250, 250, 250)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 4, colors, 150, 20);
                
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 5, System.Drawing.Color.FromArgb(255, 0, 0), 210, 80);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 100.0, 30.0, 330, 120);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 100.0, 10.0, 330, 160);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 1.0, 0.94, 330, 200);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 9, 210, 240);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 0, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 70, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "mesh", 220, 140);
            }
        }
"""

content = content[:start_add] + new_add + content[end_add:]

# Replace SolveInstance Image Logic
start_solve = content.find("            if (Params.Input[0].SourceCount > 0)")
end_solve = content.find("            Surface srf = null;")

new_solve = """            string imgPath = "";
            DA.GetData(0, ref imgPath);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new Bitmap(imgPath);
                        _cachedImagePath = imgPath;
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load image: " + ex.Message);
                        return;
                    }
                }
            }

            if (_cachedBitmap == null)
            {
                Message = "No Image";
                return;
            }

"""

content = content[:start_solve] + new_solve + content[end_solve:]

with open('Components/PixelatedSurface.cs', 'w') as f:
    f.write(content)
