using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Grasshopper.Kernel;
using GH_IO.Serialization;

namespace Enzyme.Components
{
    public class CanvasStateManager : GH_Component
    {
        // StateName -> (ComponentGuid.ToString() -> isLocked)
        private Dictionary<string, Dictionary<string, bool>> _states = new Dictionary<string, Dictionary<string, bool>>();
        private bool _prevSave = false;
        private bool _prevLoad = false;

        public CanvasStateManager()
          : base("Canvas State Manager", "StateMgr",
              "Saves and restores the Enabled/Disabled (Locked) state of all components on the canvas.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Save State", "Save", "Button to save current canvas state", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Save Name", "S_Name", "Name to save the state under", GH_ParamAccess.item, "State1");
            pManager.AddBooleanParameter("Load State", "Load", "Button to load the specified canvas state", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Load Name", "L_Name", "Name of the state to load", GH_ParamAccess.item, "State1");
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Available States", "States", "List of saved states", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Status output", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool save = false, load = false;
            string saveName = "", loadName = "";

            DA.GetData(0, ref save);
            DA.GetData(1, ref saveName);
            DA.GetData(2, ref load);
            DA.GetData(3, ref loadName);

            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            string msg = "Idle";

            if (save && !_prevSave && !string.IsNullOrWhiteSpace(saveName))
            {
                var currentState = new Dictionary<string, bool>();
                foreach (var obj in doc.Objects)
                {
                    if (obj is IGH_ActiveObject activeObj && obj.InstanceGuid != this.InstanceGuid)
                    {
                        currentState[obj.InstanceGuid.ToString()] = activeObj.Locked;
                    }
                }
                _states[saveName] = currentState;
                msg = "Saved state '" + saveName + "' with " + currentState.Count + " components.";
                this.Message = "SAVED: " + saveName;
            }
            _prevSave = save;

            if (load && !_prevLoad && !string.IsNullOrWhiteSpace(loadName))
            {
                if (_states.TryGetValue(loadName, out var savedState))
                {
                    bool changedAny = false;
                    foreach (var obj in doc.Objects)
                    {
                        if (obj is IGH_ActiveObject activeObj && obj.InstanceGuid != this.InstanceGuid)
                        {
                            string guidStr = obj.InstanceGuid.ToString();
                            if (savedState.TryGetValue(guidStr, out bool wasLocked))
                            {
                                if (activeObj.Locked != wasLocked)
                                {
                                    activeObj.Locked = wasLocked;
                                    changedAny = true;
                                }
                            }
                        }
                    }
                    
                    msg = "Loaded state '" + loadName + "'.";
                    this.Message = "LOADED: " + loadName;
                    
                    if (changedAny)
                    {
                        doc.ScheduleSolution(5, (d) => { });
                    }
                }
                else
                {
                    msg = "State '" + loadName + "' not found!";
                    this.Message = "NOT FOUND";
                }
            }
            _prevLoad = load;

            DA.SetDataList(0, _states.Keys.ToList());
            DA.SetData(1, msg);
        }

        public override bool Write(GH_IWriter writer)
        {
            try
            {
                string json = JsonConvert.SerializeObject(_states);
                writer.SetString("SavedCanvasStates", json);
            }
            catch { }
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            try
            {
                if (reader.ItemExists("SavedCanvasStates"))
                {
                    string json = reader.GetString("SavedCanvasStates");
                    _states = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, bool>>>(json) ?? new Dictionary<string, Dictionary<string, bool>>();
                }
            }
            catch { }
            return base.Read(reader);
        }
        
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 200, -60);
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 1, "State1", 200, -20, 100, 30);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 2, 200, 20);
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 3, "State1", 200, 60, 100, 30);
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("DASH.png"); 
        
        public override Guid ComponentGuid => new Guid("7F2A3B9D-1C4E-4A8D-9B1C-E3D2F4A5B6C7");
    }
}
