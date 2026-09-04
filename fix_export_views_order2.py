import re

with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# 1. Replace RegisterInputParams completely
pattern_inputs = r"protected override void RegisterInputParams.*?protected override void RegisterOutputParams"
new_inputs = '''protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "R", "Set to true to export the views.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Views", "V", "Names of the views to export. If empty, exports ALL named views.", GH_ParamAccess.list);
            pManager.AddTextParameter("Display Style", "DS", "Optional. Name of the Display Mode (e.g., 'Rendered'). Leaves active if empty.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Layer State", "LS", "Optional. Name of the saved Layer State to restore. Leaves active if empty.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Directory", "Dir", "Folder path to save the images.", GH_ParamAccess.item);
            pManager.AddTextParameter("Prefix", "P", "Prefix for the output filenames (optional).", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Suffix", "Suf", "Suffix for the output filenames (optional).", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Format", "Fmt", "Image format: png, jpg, bmp, tiff.", GH_ParamAccess.item, "png");
            
            pManager.AddIntegerParameter("Width", "W", "Image width in pixels.", GH_ParamAccess.item, 1920);
            pManager.AddIntegerParameter("Height", "H", "Image height in pixels.", GH_ParamAccess.item, 1080);
            pManager.AddNumberParameter("Scale", "S", "Scale multiplier for the final resolution (e.g. 2 for double size).", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("DPI", "DPI", "Print DPI metadata embedded into the image.", GH_ParamAccess.item, 300);
            
            pManager.AddBooleanParameter("Grid", "G", "Show Grid.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("World Axes", "WA", "Show World Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("CPlane Axes", "CA", "Show CPlane Axes.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Transparent", "T", "Transparent background (PNG only).", GH_ParamAccess.item, false);

            pManager[1].Optional = true; // Views can be empty
            pManager[2].Optional = true; // Display Style
            pManager[3].Optional = true; // Layer State
            pManager[4].Optional = true; // Directory can be empty
        }

        protected override void RegisterOutputParams'''

ts = re.sub(pattern_inputs, new_inputs, ts, flags=re.DOTALL)

# 2. Right Click Menu
pattern_menu = r"public override void AppendAdditionalMenuItems.*?private void Menu_AutoCreateViewList_Clicked"
new_menu = '''public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create ALL Value Lists", Menu_AutoCreateAll_Clicked);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create View List", Menu_AutoCreateViewList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Display Style List", Menu_AutoCreateDisplayStyleList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Layer State List", Menu_AutoCreateLayerStateList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Format List", Menu_AutoCreateFormatList_Clicked);
        }

        private void Menu_AutoCreateAll_Clicked(object sender, EventArgs e)
        {
            Menu_AutoCreateViewList_Clicked(sender, e);
            Menu_AutoCreateDisplayStyleList_Clicked(sender, e);
            Menu_AutoCreateLayerStateList_Clicked(sender, e);
            Menu_AutoCreateFormatList_Clicked(sender, e);
        }

        private void Menu_AutoCreateViewList_Clicked'''

ts = re.sub(pattern_menu, new_menu, ts, flags=re.DOTALL)

# 3. Update Indices in right-click menus
ts = ts.replace('this.Params.Input[14].AddSource(vl);', 'this.Params.Input[2].AddSource(vl);')
ts = ts.replace('this.Params.Input[15].AddSource(vl);', 'this.Params.Input[3].AddSource(vl);')
ts = ts.replace('this.Params.Input[5].AddSource(vl);', 'this.Params.Input[7].AddSource(vl);')

# 4. Update AutoWireHelper in AddedToDocument
pattern_auto = r"if \(\!hasSources\)\s*\{.*?Enzyme\.Utils\.AutoWireHelper\.WireOutputPanel"
new_auto = '''if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 210, -180);
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 4, "C:\\\\", 210, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 150);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 15, true, 210, 210);

                Enzyme.Utils.AutoWireHelper.WireOutputPanel'''
ts = re.sub(pattern_auto, new_auto, ts, flags=re.DOTALL)

# Adjust Pivot points slightly for auto-created lists so they don't overlap exactly
ts = ts.replace('vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 10);', 'vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 80);')
ts = ts.replace('vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 40);', 'vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 50);')
ts = ts.replace('vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 70);', 'vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 60);')

# And I must fix the HUD logic if there's no data or the old missing issue?
# The user's screenshot had NO HUD text (it says "No data was collected...").
# That means `SolveInstance` exited early!
# Wait, why did it exit early?
# Ah! If a parameter conversion fails (e.g. String -> Integer), Grasshopper halts the component BEFORE it even calls `SolveInstance`!
# Because Grasshopper internally validates types on inputs.
# That's why the HUD didn't show up—the component errored out entirely before `SolveInstance` ran!

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
