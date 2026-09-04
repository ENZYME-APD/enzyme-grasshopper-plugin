import re

with open("Components/PaperSizeToPixels.cs", "r") as f:
    ts = f.read()

ts = ts.replace('pManager.AddIntegerParameter("Height", "H", "Height in pixels.", GH_ParamAccess.item);', 'pManager.AddIntegerParameter("Height", "H", "Height in pixels.", GH_ParamAccess.item);\n            pManager.AddIntegerParameter("DPI", "DPI", "Pass-through DPI to wire into Export Views.", GH_ParamAccess.item);')

ts = ts.replace('DA.SetData(1, exactHeight);', 'DA.SetData(1, exactHeight);\n            DA.SetData(2, dpi);')

with open("Components/PaperSizeToPixels.cs", "w") as f:
    f.write(ts)
