with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# Add to inputs
old_input = '''            pManager.AddIntegerParameter("Width", "W", "Image width in pixels.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "H", "Image height in pixels.", GH_ParamAccess.item, 1080);'''

new_input = '''            pManager.AddIntegerParameter("Width", "W", "Image width in pixels.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "H", "Image height in pixels.", GH_ParamAccess.item, 1080);
            pManager.AddNumberParameter("Scale", "S", "Scale multiplier for the final resolution (e.g. 2 for double size).", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("DPI", "DPI", "Print DPI metadata embedded into the image.", GH_ParamAccess.item, 300);'''

ts = ts.replace(old_input, new_input)

# Add to SolveInstance DA
old_get = '''            int height = 1080;
            DA.GetData("Height", ref height);'''

new_get = '''            int height = 1080;
            DA.GetData("Height", ref height);

            double scale = 1.0;
            DA.GetData("Scale", ref scale);

            int dpi = 300;
            DA.GetData("DPI", ref dpi);'''

ts = ts.replace(old_get, new_get)

# Apply Scale
old_capture = '''                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = width,
                            Height = height,'''

new_capture = '''                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = (int)(width * scale),
                            Height = (int)(height * scale),'''

ts = ts.replace(old_capture, new_capture)

# Apply DPI
old_save = '''                                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                                savedFiles.Add(path);'''

new_save = '''                                bitmap.SetResolution(dpi, dpi);
                                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                                savedFiles.Add(path);'''

ts = ts.replace(old_save, new_save)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
