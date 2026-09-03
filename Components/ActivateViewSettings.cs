using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;
using Grasshopper.Kernel.Special;

namespace Enzyme.Components
{
    public class ActivateViewSettings : GH_Component
    {
        public ActivateViewSettings()
          : base("Activate View Settings", "ActView",
              "Activates a specified Named View, Layer State, and/or Display Style in the current Rhino viewport. If an input is left blank, that setting will not be changed.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "R", "Wire a Button here. When pressed, activates the given settings.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("View", "V", "Optional. The exact name of the Named View to activate.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Display Style", "DS", "Optional. The name of the Display Mode (e.g., 'Rendered') to activate.", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Layer State", "LS", "Optional. The name of the saved Layer State to restore.", GH_ParamAccess.item, "");

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Info", "I", "Component information and execution status.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            bool run = false;
            DA.GetData("Run", ref run);

            string viewName = "";
            DA.GetData("View", ref viewName);

            string displayStyle = "";
            DA.GetData("Display Style", ref displayStyle);

            string layerState = "";
            DA.GetData("Layer State", ref layerState);

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var activeView = doc.Views.ActiveView;
            if (activeView == null) return;

            string info = "ACTIVATE VIEW SETTINGS\n======================\n\nHOW IT WORKS:\nUpdates the Rhino viewport to match the given View, Display Style, and Layer State. Leaving an input empty means that property will not change.\n\nINTERPRETATION & IMPORTANCE:\nAutomates viewport configuration, ensuring you can quickly switch presentation or working states without manually clicking through Rhino panels.";

            string statusView = "UNCHANGED";
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
            }

            DA.SetData(0, info);

            sw.Stop();
            this.Message = $"ACTIVATE SETTINGS\nTime: {sw.ElapsedMilliseconds} ms\n---\nVIEW: {statusView}\nSTYLE: {statusStyle}\nLAYER: {statusLayer}";
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create View List", Menu_AutoCreateViewList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Display Style List", Menu_AutoCreateDisplayStyleList_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Layer State List", Menu_AutoCreateLayerStateList_Clicked);
        }

        private void Menu_AutoCreateViewList_Clicked(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            
            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 20);
            vl.ListItems.Clear();

            for (int i = 0; i < doc.NamedViews.Count; i++)
            {
                string n = doc.NamedViews[i].Name;
                vl.ListItems.Add(new GH_ValueListItem(n, $"\"{n}\""));
            }

            if (vl.ListItems.Count == 0)
            {
                RhinoApp.WriteLine("No named views found.");
                return;
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[1].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateDisplayStyleList_Clicked(object sender, EventArgs e)
        {
            var modes = Rhino.Display.DisplayModeDescription.GetDisplayModes();
            if (modes.Length == 0) return;

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 10);
            vl.ListItems.Clear();

            foreach (var mode in modes)
            {
                vl.ListItems.Add(new GH_ValueListItem(mode.EnglishName, $"\"{mode.EnglishName}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[2].AddSource(vl);
            vl.ExpireSolution(true);
        }

        private void Menu_AutoCreateLayerStateList_Clicked(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var names = doc.NamedLayerStates.Names;
            if (names.Length == 0)
            {
                RhinoApp.WriteLine("No saved layer states found.");
                return;
            }

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 40);
            vl.ListItems.Clear();

            foreach (string n in names)
            {
                vl.ListItems.Add(new GH_ValueListItem(n, $"\"{n}\""));
            }

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[3].AddSource(vl);
            vl.ExpireSolution(true);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 210, -50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 150, 0, 300, 300);
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return Enzyme.IconLoader.Load("ActivateViewSettings.png"); }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("14A6B8F7-9D2E-47F1-B89C-215E58C4F1A2"); }
        }
    }
}
