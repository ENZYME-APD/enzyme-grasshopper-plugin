with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

orig_params = """            pManager.AddIntegerParameter("Width", "Width", "Export width in pixels", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "Height", "Export height in pixels", GH_ParamAccess.item, 1080);
            pManager.AddBooleanParameter("Transparent", "Trans", "Export with a transparent background", GH_ParamAccess.item, false);"""

new_params = """            pManager.AddIntegerParameter("Width", "Width", "Export width in pixels", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "Height", "Export height in pixels", GH_ParamAccess.item, 1080);
            pManager.AddIntegerParameter("DPI", "DPI", "Print resolution in Dots Per Inch", GH_ParamAccess.item, 300);
            pManager.AddBooleanParameter("Transparent", "Trans", "Export with a transparent background", GH_ParamAccess.item, false);"""

content = content.replace(orig_params, new_params)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
