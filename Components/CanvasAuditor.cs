using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class CanvasAuditor : GH_Component
    {
        public CanvasAuditor()
          : base("Canvas Auditor", "Auditor",
              "Scans the Grasshopper canvas to select components with errors, legacy/outdated components, or all Enzyme components.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Select Enzyme", "Select Enzyme", "Selects all Enzyme components on the canvas.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Errors", "Select Errors", "Selects all components currently throwing errors (red nodes).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Outdated", "Select Outdated", "Selects outdated, obsolete, or unrecognized 'placeholder' components.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Disabled", "Select Disabled", "Selects all disabled components.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Preview-On", "Select Preview-On", "Selects all components with preview toggled ON.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Report", "Report", "Audit results of the current canvas.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            bool selEnzyme = false;
            bool selErrors = false;
            bool selOutdated = false;
            bool selDisabled = false;
            bool selPreviewOn = false;

            DA.GetData("Select Enzyme", ref selEnzyme);
            DA.GetData("Select Errors", ref selErrors);
            DA.GetData("Select Outdated", ref selOutdated);
            DA.GetData("Select Disabled", ref selDisabled);
            DA.GetData("Select Preview-On", ref selPreviewOn);

            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            int enzymeCount = 0;
            int errorCount = 0;
            int outdatedCount = 0;
            int disabledCount = 0;
            int previewOnCount = 0;

            bool redrawNeeded = selEnzyme || selErrors || selOutdated || selDisabled || selPreviewOn;

            if (redrawNeeded)
            {
                doc.DeselectAll();
            }

            foreach (var obj in doc.Objects)
            {
                bool isEnzyme = obj.Category != null && obj.Category.Equals("Enzyme", StringComparison.OrdinalIgnoreCase);
                bool isError = obj is IGH_ActiveObject act && act.RuntimeMessageLevel == GH_RuntimeMessageLevel.Error;
                bool isOutdated = obj.Obsolete || obj.GetType().Name.Contains("Placeholder");
                bool isDisabled = obj is IGH_ActiveObject activeObj && activeObj.Locked;
                bool isPreviewOn = obj is IGH_PreviewObject previewObj && !previewObj.Hidden && previewObj.IsPreviewCapable;

                if (isEnzyme) enzymeCount++;
                if (isError) errorCount++;
                if (isOutdated) outdatedCount++;
                if (isDisabled) disabledCount++;
                if (isPreviewOn) previewOnCount++;

                if (redrawNeeded)
                {
                    bool selectMe = false;
                    if (selEnzyme && isEnzyme) selectMe = true;
                    if (selErrors && isError) selectMe = true;
                    if (selOutdated && isOutdated) selectMe = true;
                    if (selDisabled && isDisabled) selectMe = true;
                    if (selPreviewOn && isPreviewOn) selectMe = true;

                    if (selectMe)
                    {
                        obj.Attributes.Selected = true;
                    }
                }
            }

            if (redrawNeeded)
            {
                // Unwind selection redraw safely
                Rhino.RhinoApp.InvokeOnUiThread(new Action(() => {
                    Grasshopper.Instances.ActiveCanvas?.Invalidate();
                }));
            }

            string report = 
                "CANVAS AUDIT REPORT\n" +
                "===================\n" +
                $"Total Objects: {doc.Objects.Count}\n" +
                $"Enzyme Components: {enzymeCount}\n" +
                $"Nodes with Errors: {errorCount}\n" +
                $"Outdated/Broken Nodes: {outdatedCount}\n" +
                $"Disabled Nodes: {disabledCount}\n" +
                $"Preview-On Nodes: {previewOnCount}\n\n" +
                "Connect a Button to inputs to select specific groups.";

            DA.SetData(0, report);
        
            stopwatch.Stop();
            Message = $"{this.NickName}\n{stopwatch.ElapsedMilliseconds} ms\n---\nDone";
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[0].SourceCount == 0 &&
                this.Params.Input[1].SourceCount == 0 &&
                this.Params.Input[2].SourceCount == 0)
            {
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 160, 0);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 1, 160, 30);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 2, 160, 60);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 3, 160, 90);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 4, 160, 120);
                document.ExpireSolution();
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("CanvasAuditor.png");

        
        public override GH_Exposure Exposure => GH_Exposure.primary;
public override Guid ComponentGuid => new Guid("4A2B8D9F-E13C-4D2A-B981-F0C6A53D749B");
    }
}
