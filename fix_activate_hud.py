import re

with open("Components/ActivateViewSettings.cs", "r") as f:
    ts = f.read()

old_logic = '''            string statusView = "UNCHANGED";
            string statusStyle = "UNCHANGED";
            string statusLayer = "UNCHANGED";

            if (run)
            {
                // 1. Restore View
                if (!string.IsNullOrEmpty(viewName))
                {
                    int index = doc.NamedViews.FindByName(viewName);
                    if (index >= 0)
                    {
                        doc.NamedViews.Restore(index, activeView.ActiveViewport);
                        statusView = viewName.ToUpper();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Named view '{viewName}' not found.");
                        statusView = "NOT FOUND";
                    }
                }

                // 2. Restore Display Style
                if (!string.IsNullOrEmpty(displayStyle))
                {
                    var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
                    bool found = false;
                    foreach (var mode in modes)
                    {
                        if (mode.EnglishName.Equals(displayStyle, StringComparison.OrdinalIgnoreCase))
                        {
                            activeView.ActiveViewport.DisplayMode = mode;
                            statusStyle = mode.EnglishName.ToUpper();
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Display mode '{displayStyle}' not found.");
                        statusStyle = "NOT FOUND";
                    }
                }

                // 3. Restore Layer State
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
                        doc.NamedLayerStates.Restore(layerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                        statusLayer = layerState.ToUpper();
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Layer state '{layerState}' not found.");
                        statusLayer = "NOT FOUND";
                    }
                }

                activeView.Redraw();
            }
            else
            {
                statusView = "WAITING...";
                statusStyle = "WAITING...";
                statusLayer = "WAITING...";
            }'''

new_logic = '''            string statusView = string.IsNullOrEmpty(viewName) ? "UNCHANGED" : viewName.ToUpper();
            string statusStyle = string.IsNullOrEmpty(displayStyle) ? "UNCHANGED" : displayStyle.ToUpper();
            string statusLayer = string.IsNullOrEmpty(layerState) ? "UNCHANGED" : layerState.ToUpper();

            if (run)
            {
                // 1. Restore View
                if (!string.IsNullOrEmpty(viewName))
                {
                    int index = doc.NamedViews.FindByName(viewName);
                    if (index >= 0)
                    {
                        doc.NamedViews.Restore(index, activeView.ActiveViewport);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Named view '{viewName}' not found.");
                        statusView = "NOT FOUND";
                    }
                }

                // 2. Restore Display Style
                if (!string.IsNullOrEmpty(displayStyle))
                {
                    var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
                    bool found = false;
                    foreach (var mode in modes)
                    {
                        if (mode.EnglishName.Equals(displayStyle, StringComparison.OrdinalIgnoreCase))
                        {
                            activeView.ActiveViewport.DisplayMode = mode;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Display mode '{displayStyle}' not found.");
                        statusStyle = "NOT FOUND";
                    }
                }

                // 3. Restore Layer State
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
                        doc.NamedLayerStates.Restore(layerState, Rhino.DocObjects.Tables.RestoreLayerProperties.All);
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Layer state '{layerState}' not found.");
                        statusLayer = "NOT FOUND";
                    }
                }

                activeView.Redraw();
            }'''

ts = ts.replace(old_logic, new_logic)

with open("Components/ActivateViewSettings.cs", "w") as f:
    f.write(ts)
