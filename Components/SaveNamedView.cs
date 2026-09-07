using System;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;

namespace Enzyme.Components
{
    public class SaveNamedView : GH_Component
    {
        private string _lastAction = "";

        public SaveNamedView()
          : base("Save Named View", "SaveView",
              "Saves the current active Rhino viewport as a Named View.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Name for the saved view", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Save", "S", "Button to trigger saving", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Overwrite", "O", "Overwrite if a view with the same name exists", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Info", "Info", "Component information and methodology", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            DA.SetData(0, "SAVE NAMED VIEW\n\nHOW IT WORKS:\nSaves the current active Rhino viewport as a Named View.\n\nINTERPRETATION & IMPORTANCE:\nAutomates viewpoint documentation so you don't have to manually save angles during parametric iterations.");

            try
            {
                string name = string.Empty;
                bool save = false;
                bool overwrite = true;

                if (!DA.GetData(0, ref name)) return;
                if (!DA.GetData(1, ref save)) return;
                DA.GetData(2, ref overwrite);

                if (!save || string.IsNullOrWhiteSpace(name))
                {
                    if (string.IsNullOrWhiteSpace(_lastAction)) _lastAction = "Idle";
                    return;
                }

                var doc = RhinoDoc.ActiveDoc;
                if (doc == null || doc.Views.ActiveView == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No active Rhino document or view found.");
                    return;
                }

                int existingIndex = doc.NamedViews.FindByName(name);
                if (existingIndex >= 0)
                {
                    if (overwrite)
                    {
                        // Update existing
                        doc.NamedViews.Delete(existingIndex);
                        doc.NamedViews.Add(name, doc.Views.ActiveView.ActiveViewport.Id);
                        _lastAction = "Overwrote:\n" + name;
                    }
                    else
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"View '{name}' already exists and Overwrite is false.");
                        _lastAction = "Skipped (Exists)";
                    }
                }
                else
                {
                    doc.NamedViews.Add(name, doc.Views.ActiveView.ActiveViewport.Id);
                    _lastAction = "Saved:\n" + name;
                }
            }
            finally
            {
                sw.Stop();
                this.Message = $"SAVE NAMED VIEW\nTime: {sw.ElapsedMilliseconds} ms\n---\n{_lastAction}";
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("SaveNamedViews.png");

        public override Guid ComponentGuid => new Guid("D4E5F6A1-B2C3-4D5E-6F7A-1B2C3D4E5F6A");

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            if (this.Params.Input[0].SourceCount == 0)
            {
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "", 160, -40, 120, 25);
            }
            if (this.Params.Input[1].SourceCount == 0)
            {
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 1, 120, 0);
            }
            if (this.Params.Input[2].SourceCount == 0)
            {
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 2, true, 120, 40);
            }
        }
    }
}
