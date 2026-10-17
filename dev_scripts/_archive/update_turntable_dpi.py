import re

with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

# Update RegisterInputParams
inputs_orig = """            pManager.AddIntegerParameter("Width", "Width", "Export width in pixels", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "Height", "Export height in pixels", GH_ParamAccess.item, 1080);
            pManager.AddBooleanParameter("Transparent", "Alpha", "Transparent background", GH_ParamAccess.item, false);"""

inputs_new = """            pManager.AddIntegerParameter("Width", "Width", "Export width in pixels", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "Height", "Export height in pixels", GH_ParamAccess.item, 1080);
            pManager.AddIntegerParameter("DPI", "DPI", "Print resolution in Dots Per Inch", GH_ParamAccess.item, 300);
            pManager.AddBooleanParameter("Transparent", "Alpha", "Transparent background", GH_ParamAccess.item, false);"""
content = content.replace(inputs_orig, inputs_new)

# Update SolveInstance variable extraction
solve_orig = """            int width = 1920; DA.GetData(6, ref width);
            int height = 1080; DA.GetData(7, ref height);
            bool transparent = false; DA.GetData(8, ref transparent);
            bool scaleItems = false; DA.GetData(9, ref scaleItems);
            bool run = false; DA.GetData(10, ref run);"""

solve_new = """            int width = 1920; DA.GetData(6, ref width);
            int height = 1080; DA.GetData(7, ref height);
            int dpi = 300; DA.GetData(8, ref dpi);
            bool transparent = false; DA.GetData(9, ref transparent);
            bool scaleItems = false; DA.GetData(10, ref scaleItems);
            bool run = false; DA.GetData(11, ref run);"""
content = content.replace(solve_orig, solve_new)

# Apply DPI to Bitmap
dpi_orig = """                            using (Bitmap bmp = capture.CaptureToBitmap(activeView))
                            {
                                if (bmp != null)
                                {
                                    string fileName = $"{prefix}{i:D4}.png";"""

dpi_new = """                            using (Bitmap bmp = capture.CaptureToBitmap(activeView))
                            {
                                if (bmp != null)
                                {
                                    bmp.SetResolution(dpi, dpi);
                                    string fileName = $"{prefix}{i:D4}.png";"""
content = content.replace(dpi_orig, dpi_new)

# Update AutoWireDefaultInputs indices
autowire_orig = """            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 8, false, 200, 40);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 9, false, 200, 60);
            Enzyme.Utils.AutoWireHelper.WireButton(this, doc, 10, 200, 80);"""

autowire_new = """            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 9, false, 200, 40);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 10, false, 200, 60);
            Enzyme.Utils.AutoWireHelper.WireButton(this, doc, 11, 200, 80);"""
content = content.replace(autowire_orig, autowire_new)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
