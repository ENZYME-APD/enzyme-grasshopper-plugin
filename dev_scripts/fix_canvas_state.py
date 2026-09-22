import re

with open('Components/CanvasStateManager.cs', 'r') as f:
    content = f.read()

# Update RegisterInputParams
inputs_orig = """        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Save State", "Save", "Button to save current canvas state", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Save Name", "S_Name", "Name to save the state under", GH_ParamAccess.item, "State1");
            pManager.AddBooleanParameter("Load State", "Load", "Button to load the specified canvas state", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Load Name", "L_Name", "Name of the state to load", GH_ParamAccess.item, "State1");
            pManager.AddBooleanParameter("Turn Off New Nodes", "OffNew", "If true, new nodes added after the state was saved will have preview disabled (hidden).", GH_ParamAccess.item, true);

            // Make text inputs optional so value lists without a selection don't break the component
            pManager[1].Optional = true;
            pManager[3].Optional = true;
        }"""

inputs_new = """        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Save State", "Save", "Button to save current canvas state", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Save Name", "S_Name", "Name to save the state under", GH_ParamAccess.item, "State1");
            pManager.AddBooleanParameter("Load State", "Load", "Button to load the specified canvas state", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Load Name", "L_Name", "Name of the state to load", GH_ParamAccess.item, "State1");
            pManager.AddBooleanParameter("Turn Off New Nodes", "OffNew", "If true, new nodes added after the state was saved will have preview disabled (hidden).", GH_ParamAccess.item, true);
            pManager.AddTextParameter("Import JSON", "Import", "Optional JSON string from another State Manager to merge states.", GH_ParamAccess.item);

            pManager[1].Optional = true;
            pManager[3].Optional = true;
            pManager[5].Optional = true;
        }"""
content = content.replace(inputs_orig, inputs_new)

# Update RegisterOutputParams
outputs_orig = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Available States", "States", "List of saved states", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Status output", GH_ParamAccess.item);
        }"""
outputs_new = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Available States", "States", "List of saved states", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Status output", GH_ParamAccess.item);
            pManager.AddTextParameter("Export JSON", "JSON", "Serialized JSON of all saved states. Plug this into 'Import JSON' of another State Manager to share states.", GH_ParamAccess.item);
        }"""
content = content.replace(outputs_orig, outputs_new)


# Update SolveInstance (part 1: read import)
solve_orig = """            bool offNew = true;
            DA.GetData(4, ref offNew);

            GH_Document doc = OnPingDocument();
            string msg = "";

            if (save && !_prevSave && !string.IsNullOrWhiteSpace(saveName))"""

solve_new = """            bool offNew = true;
            DA.GetData(4, ref offNew);

            string importJson = null;
            if (DA.GetData(5, ref importJson) && !string.IsNullOrWhiteSpace(importJson))
            {
                try
                {
                    var importedStates = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, NodeState>>>(importJson);
                    if (importedStates != null)
                    {
                        foreach (var kvp in importedStates)
                        {
                            _states[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch { }
            }

            GH_Document doc = OnPingDocument();
            string msg = "";

            if (save && !_prevSave && !string.IsNullOrWhiteSpace(saveName))"""
content = content.replace(solve_orig, solve_new)


# Update SolveInstance (part 2: set output)
solve_out_orig = """            DA.SetDataList(0, _states.Keys.ToList());
            DA.SetData(1, "CANVAS STATE MANAGER\\n\\nHOW IT WORKS:\\nSaves and restores the Enabled/Disabled (Locked) and Preview (Hidden) states of all components on the canvas.\\n\\nINTERPRETATION & IMPORTANCE:\\nAllows you to swap between different visualization or computation modes instantly.");

            sw.Stop();
            this.Message = $"STATE MANAGER\\nTime: {sw.ElapsedMilliseconds} ms\\n---\\n{_lastAction}\\nSaved: {_states.Count}";"""

solve_out_new = """            DA.SetDataList(0, _states.Keys.ToList());
            DA.SetData(1, "CANVAS STATE MANAGER\\n\\nHOW IT WORKS:\\nSaves and restores the Enabled/Disabled (Locked) and Preview (Hidden) states of all components on the canvas.\\n\\nINTERPRETATION & IMPORTANCE:\\nAllows you to swap between different visualization or computation modes instantly.");
            
            try {
                string exportJson = Newtonsoft.Json.JsonConvert.SerializeObject(_states);
                DA.SetData(2, exportJson);
            } catch { }

            sw.Stop();
            this.Message = $"STATE MANAGER\\nTime: {sw.ElapsedMilliseconds} ms\\n---\\n{_lastAction}\\nSaved: {_states.Count}";"""
content = content.replace(solve_out_orig, solve_out_new)

# Update Context Menu
menu_orig = """        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-fill Load ValueList", (s, e) => AutoFillValueList());
        }"""

menu_new = """        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-fill Load ValueList", (s, e) => AutoFillValueList());
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Copy States to Clipboard (JSON)", (s, e) => {
                try {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(_states);
                    System.Windows.Forms.Clipboard.SetText(json);
                } catch { }
            });
            Menu_AppendItem(menu, "Paste States from Clipboard", (s, e) => {
                try {
                    string json = System.Windows.Forms.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(json)) {
                        var importedStates = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, NodeState>>>(json);
                        if (importedStates != null) {
                            foreach (var kvp in importedStates) { _states[kvp.Key] = kvp.Value; }
                            this.ExpireSolution(true);
                        }
                    }
                } catch { }
            });
        }"""
content = content.replace(menu_orig, menu_new)


with open('Components/CanvasStateManager.cs', 'w') as f:
    f.write(content)
