import re

with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# 1. Update InputParams
old_in = '''            pManager.AddBooleanParameter("Transparent", "T", "Transparent background (PNG only).", GH_ParamAccess.item, false);

            pManager[1].Optional = true; // Views can be empty'''

new_in = '''            pManager.AddBooleanParameter("Transparent", "T", "Transparent background (PNG only).", GH_ParamAccess.item, false);
            
            pManager.AddTextParameter("Display Style", "DS", "Optional. Name of the Display Mode (e.g., 'Rendered'). Leaves active if empty.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Layer State", "LS", "Optional. Name of the saved Layer State to restore. Leaves active if empty.", GH_ParamAccess.item, "");

            pManager[1].Optional = true; // Views can be empty
            pManager[12].Optional = true;
            pManager[13].Optional = true;'''
ts = ts.replace(old_in, new_in)

# 2. Extract data in SolveInstance
old_get = '''            bool transparent = false;
            DA.GetData("Transparent", ref transparent);

            var doc = RhinoDoc.ActiveDoc;'''

new_get = '''            bool transparent = false;
            DA.GetData("Transparent", ref transparent);

            string displayStyle = "";
            DA.GetData("Display Style", ref displayStyle);

            string layerState = "";
            DA.GetData("Layer State", ref layerState);

            var doc = RhinoDoc.ActiveDoc;'''
ts = ts.replace(old_get, new_get)

# 3. Add capture logic
old_cap = '''                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = (int)(width * scale),
                            Height = (int)(height * scale),
                            TransparentBackground = transparent,
                            DrawGrid = grid,
                            DrawAxes = worldAxes,
                            DrawGridAxes = cplaneAxes
                        };

                        foreach (var nv in viewsToExport)'''

new_cap = '''                        var capture = new Rhino.Display.ViewCapture
                        {
                            Width = (int)(width * scale),
                            Height = (int)(height * scale),
                            TransparentBackground = transparent,
                            DrawGrid = grid,
                            DrawAxes = worldAxes,
                            DrawGridAxes = cplaneAxes
                        };
                        
                        // Handle Display Style
                        var originalDisplayMode = activeView.ActiveViewport.DisplayMode;
                        if (!string.IsNullOrEmpty(displayStyle))
                        {
                            var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
                            foreach (var mode in modes)
                            {
                                if (mode.EnglishName.Equals(displayStyle, StringComparison.OrdinalIgnoreCase))
                                {
                                    activeView.ActiveViewport.DisplayMode = mode;
                                    break;
                                }
                            }
                        }

                        // Handle Layer State
                        string tempLayerState = "Enzyme_Temp_" + Guid.NewGuid().ToString();
                        bool layerStateChanged = false;
                        if (!string.IsNullOrEmpty(layerState))
                        {
                            var names = doc.NamedLayerStates.Names;
                            bool found = false;
                            foreach(var n in names)
                            {
                                if (n.Equals(layerState, StringComparison.OrdinalIgnoreCase))
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                            {
                                doc.NamedLayerStates.Save(tempLayerState);
                                doc.NamedLayerStates.Restore(layerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                                layerStateChanged = true;
                            }
                            else
                            {
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Layer State '{layerState}' not found.");
                            }
                        }

                        foreach (var nv in viewsToExport)'''
ts = ts.replace(old_cap, new_cap)

# 4. Restore original states after the loop
old_res = '''                        // Ensure it's fully restored
                        activeView.Redraw();
                    }
                }
            }'''

new_res = '''                        // Ensure it's fully restored
                        
                        // Revert display mode if changed
                        if (activeView.ActiveViewport.DisplayMode.Id != originalDisplayMode.Id)
                        {
                            activeView.ActiveViewport.DisplayMode = originalDisplayMode;
                        }

                        // Revert layer state if changed
                        if (layerStateChanged)
                        {
                            doc.NamedLayerStates.Restore(tempLayerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                            doc.NamedLayerStates.Delete(tempLayerState);
                        }

                        activeView.Redraw();
                    }
                }
            }'''
ts = ts.replace(old_res, new_res)


# 5. Add Menu Items for new Value Lists
old_menu = '''        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create View List", Menu_AutoCreateViewList_Clicked);
        }'''

new_menu = '''        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create View List", Menu_AutoCreateViewList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Display Style List", Menu_AutoCreateDisplayStyleList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Layer State List", Menu_AutoCreateLayerStateList_Clicked);
        }'''
ts = ts.replace(old_menu, new_menu)

# 6. Add the two new event handlers at the end of the class
old_end = '''            // Wire it to the "Views" input (Index 1)
            this.Params.Input[1].AddSource(vl);
            vl.ExpireSolution(true);
        }

        public override Guid ComponentGuid'''

new_end = '''            // Wire it to the "Views" input (Index 1)
            this.Params.Input[1].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateDisplayStyleList_Clicked(object sender, EventArgs e)
        {
            var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
            if (modes.Length == 0) return;

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 10);
            vl.ListItems.Clear();

            foreach (var mode in modes)
            {
                vl.ListItems.Add(new GH_ValueListItem(mode.EnglishName, $"\"{mode.EnglishName}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[12].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateLayerStateList_Clicked(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var names = doc.NamedLayerStates.Names;
            if (names.Count == 0)
            {
                RhinoApp.WriteLine("No saved layer states found.");
                return;
            }

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 40);
            vl.ListItems.Clear();

            foreach (string n in names)
            {
                vl.ListItems.Add(new GH_ValueListItem(n, $"\"{n}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[13].AddSource(vl);
            vl.ExpireSolution(true);
        }

        public override Guid ComponentGuid'''
ts = ts.replace(old_end, new_end)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
