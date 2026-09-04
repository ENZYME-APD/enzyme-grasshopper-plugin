import re

with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# 1. Remove from RegisterInputParams
old_param = '            pManager.AddNumberParameter("Scale", "S", "Scale multiplier for the final resolution (e.g. 2 for double size).", GH_ParamAccess.item, 1.0);\n'
ts = ts.replace(old_param, '')

# 2. Remove GetData
old_getdata = '''            int height = 1080;
            DA.GetData("Height", ref height);

            double scale = 1.0;
            DA.GetData("Scale", ref scale);

            int dpi = 300;'''
new_getdata = '''            int height = 1080;
            DA.GetData("Height", ref height);

            int dpi = 300;'''
ts = ts.replace(old_getdata, new_getdata)

# 3. Remove from ViewCapture
old_capture = '''                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = (int)(width * scale),
                            Height = (int)(height * scale),
                            TransparentBackground = transparent,
                            DrawGrid = grid,
                            DrawAxes = worldAxes,
                            DrawGridAxes = cplaneAxes
                        };'''
new_capture = '''                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = width,
                            Height = height,
                            TransparentBackground = transparent,
                            DrawGrid = grid,
                            DrawAxes = worldAxes,
                            DrawGridAxes = cplaneAxes
                        };'''
ts = ts.replace(old_capture, new_capture)

# 4. Update Autowire offsets
old_auto = '''                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 4, "C:\\\\", 210, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 150);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 15, true, 210, 210);'''
new_auto = '''                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 4, "C:\\\\", 210, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 150);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, true, 210, 210);'''
ts = ts.replace(old_auto, new_auto)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
