using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Grasshopper.Kernel;
using GH_IO.Serialization;

namespace Enzyme.Components
{
    public class NodeState
    {
        public bool Locked { get; set; }
        public bool Hidden { get; set; }
    }

    public class CanvasStateManager : GH_Component
    {
        // StateName -> (ComponentGuid.ToString() -> NodeState)
        private Dictionary<string, Dictionary<string, NodeState>> _states = new Dictionary<string, Dictionary<string, NodeState>>();
        private bool _prevSave = false;
        private bool _prevLoad = false;

        public CanvasStateManager()
          : base("Canvas State Manager", "StateMgr",
              "Saves and restores the Enabled/Disabled (Locked) and Preview (Hidden) state of all components on the canvas.",
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

        private System.Diagnostics.Stopwatch _stopwatch;
        private string _lastAction = "Idle";

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
            _stopwatch?.Stop();
            long ms = _stopwatch != null ? _stopwatch.ElapsedMilliseconds : 0;
            this.Message = $"STATE MANAGER\nTime: {ms} ms\n---\nStatus: {_lastAction}\nSaved States: {_states.Count}";
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
                var currentState = new Dictionary<string, NodeState>();
                foreach (var obj in doc.Objects)
                {
                    if (obj.InstanceGuid != this.InstanceGuid)
                    {
                        var st = new NodeState();
                        bool hasState = false;
                        
                        if (obj is IGH_ActiveObject act)
                        {
                            st.Locked = act.Locked;
                            hasState = true;
                        }
                        if (obj is IGH_PreviewObject prv)
                        {
                            st.Hidden = prv.Hidden;
                            hasState = true;
                        }
                        
                        if (hasState)
                        {
                            currentState[obj.InstanceGuid.ToString()] = st;
                        }
                    }
                }
                _states[saveName] = currentState;
                msg = "Saved state '" + saveName + "' with " + currentState.Count + " components.";
                _lastAction = "SAVED: " + saveName;
            }
            _prevSave = save;

            if (load && !_prevLoad && !string.IsNullOrWhiteSpace(loadName))
            {
                if (_states.TryGetValue(loadName, out var savedState))
                {
                    msg = "Loaded state '" + loadName + "'.";
                    _lastAction = "LOADED: " + loadName;
                    
                    doc.ScheduleSolution(5, (d) => {
                        bool redraw = false;
                        foreach (var obj in d.Objects)
                        {
                            if (obj.InstanceGuid != this.InstanceGuid)
                            {
                                string guidStr = obj.InstanceGuid.ToString();
                                if (savedState.TryGetValue(guidStr, out NodeState wasState))
                                {
                                    bool changed = false;
                                    if (obj is IGH_ActiveObject act && act.Locked != wasState.Locked)
                                    {
                                        act.Locked = wasState.Locked;
                                        changed = true;
                                    }
                                    if (obj is IGH_PreviewObject prv && prv.Hidden != wasState.Hidden)
                                    {
                                        prv.Hidden = wasState.Hidden;
                                        changed = true;
                                        redraw = true; // Preview changes require redraw
                                    }
                                    
                                    if (changed && obj is IGH_ActiveObject act2)
                                    {
                                        act2.ExpireSolution(false);
                                    }
                                }
                            }
                        }
                        if (redraw) {
                            Grasshopper.Instances.ActiveCanvas?.Refresh();
                            Rhino.RhinoDoc.ActiveDoc?.Views.Redraw();
                        }
                    });
                }
                else
                {
                    msg = "State '" + loadName + "' not found!";
                    _lastAction = "NOT FOUND: " + loadName;
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
                    _states = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, NodeState>>>(json) ?? new Dictionary<string, Dictionary<string, NodeState>>();
                }
            }
            catch 
            { 
                _states = new Dictionary<string, Dictionary<string, NodeState>>();
            }
            return base.Read(reader);
        }
        

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-fill Load ValueList", (s, e) => AutoFillValueList());
        }

        private void AutoFillValueList()
        {
            if (_states.Count == 0) return;

            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            var loadParam = this.Params.Input[3];
            Grasshopper.Kernel.Special.GH_ValueList vl = null;

            foreach (var source in loadParam.Sources)
            {
                if (source is Grasshopper.Kernel.Special.GH_ValueList v)
                {
                    vl = v;
                    break;
                }
            }

            bool isNew = false;
            if (vl == null)
            {
                vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 60);
                vl.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.DropDown;
                isNew = true;
            }

            vl.ListItems.Clear();
            foreach (var key in _states.Keys)
            {
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(key, $"\"{key}\""));
            }

            if (isNew)
            {
                doc.AddObject(vl, false);
                loadParam.AddSource(vl);
            }

            vl.ExpireSolution(true);
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

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("canvasStateManager.png");
        
        public override Guid ComponentGuid => new Guid("7F2A3B9D-1C4E-4A8D-9B1C-E3D2F4A5B6C7");
    }
}
