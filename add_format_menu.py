with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# Add Menu
old_menu = '''            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Display Style List", Menu_AutoCreateDisplayStyleList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Layer State List", Menu_AutoCreateLayerStateList_Clicked);
        }'''

new_menu = '''            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Display Style List", Menu_AutoCreateDisplayStyleList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Layer State List", Menu_AutoCreateLayerStateList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Format List", Menu_AutoCreateFormatList_Clicked);
        }'''
ts = ts.replace(old_menu, new_menu)

# Add Handler
old_end = '''        public override Guid ComponentGuid'''

new_end = '''        private void Menu_AutoCreateFormatList_Clicked(object sender, EventArgs e)
        {
            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 70);
            vl.ListItems.Clear();

            vl.ListItems.Add(new GH_ValueListItem("PNG", "\\"png\\""));
            vl.ListItems.Add(new GH_ValueListItem("JPG", "\\"jpg\\""));
            vl.ListItems.Add(new GH_ValueListItem("BMP", "\\"bmp\\""));
            vl.ListItems.Add(new GH_ValueListItem("TIFF", "\\"tif\\""));

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[5].AddSource(vl);
            vl.ExpireSolution(true);
        }

        public override Guid ComponentGuid'''
ts = ts.replace(old_end, new_end)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
