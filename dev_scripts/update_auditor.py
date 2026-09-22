with open('Components/CanvasAuditor.cs', 'r') as f:
    content = f.read()

# 1. RegisterInputParams
inputs_orig = """        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Select Enzyme", "Select Enzyme", "Selects all Enzyme components on the canvas.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Errors", "Select Errors", "Selects all components currently throwing errors (red nodes).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Outdated", "Select Outdated", "Selects outdated, obsolete, or unrecognized 'placeholder' components.", GH_ParamAccess.item, false);
        }"""
inputs_new = """        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Select Enzyme", "Select Enzyme", "Selects all Enzyme components on the canvas.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Errors", "Select Errors", "Selects all components currently throwing errors (red nodes).", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Outdated", "Select Outdated", "Selects outdated, obsolete, or unrecognized 'placeholder' components.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Disabled", "Select Disabled", "Selects all disabled components.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Select Preview-On", "Select Preview-On", "Selects all components with preview toggled ON.", GH_ParamAccess.item, false);
        }"""
content = content.replace(inputs_orig, inputs_new)

# 2. SolveInstance Vars
solve_vars_orig = """            bool selEnzyme = false;
            bool selErrors = false;
            bool selOutdated = false;

            DA.GetData("Select Enzyme", ref selEnzyme);
            DA.GetData("Select Errors", ref selErrors);
            DA.GetData("Select Outdated", ref selOutdated);

            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            int enzymeCount = 0;
            int errorCount = 0;
            int outdatedCount = 0;

            bool redrawNeeded = selEnzyme || selErrors || selOutdated;"""
solve_vars_new = """            bool selEnzyme = false;
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

            bool redrawNeeded = selEnzyme || selErrors || selOutdated || selDisabled || selPreviewOn;"""
content = content.replace(solve_vars_orig, solve_vars_new)

# 3. Object loop
loop_orig = """                bool isEnzyme = obj.Category != null && obj.Category.Equals("Enzyme", StringComparison.OrdinalIgnoreCase);
                bool isError = obj is IGH_ActiveObject act && act.RuntimeMessageLevel == GH_RuntimeMessageLevel.Error;
                bool isOutdated = obj.Obsolete || obj.GetType().Name.Contains("Placeholder");

                if (isEnzyme) enzymeCount++;
                if (isError) errorCount++;
                if (isOutdated) outdatedCount++;

                if (redrawNeeded)
                {
                    bool selectMe = false;
                    if (selEnzyme && isEnzyme) selectMe = true;
                    if (selErrors && isError) selectMe = true;
                    if (selOutdated && isOutdated) selectMe = true;

                    if (selectMe)
                    {
                        obj.Attributes.Selected = true;
                    }
                }"""
loop_new = """                bool isEnzyme = obj.Category != null && obj.Category.Equals("Enzyme", StringComparison.OrdinalIgnoreCase);
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
                }"""
content = content.replace(loop_orig, loop_new)

# 4. Report string
report_orig = """            string report = 
                "CANVAS AUDIT REPORT\\n" +
                "===================\\n" +
                $"Total Objects: {doc.Objects.Count}\\n" +
                $"Enzyme Components: {enzymeCount}\\n" +
                $"Nodes with Errors: {errorCount}\\n" +
                $"Outdated/Broken Nodes: {outdatedCount}\\n\\n" +
                "Connect a Button to inputs to select specific groups.";"""
report_new = """            string report = 
                "CANVAS AUDIT REPORT\\n" +
                "===================\\n" +
                $"Total Objects: {doc.Objects.Count}\\n" +
                $"Enzyme Components: {enzymeCount}\\n" +
                $"Nodes with Errors: {errorCount}\\n" +
                $"Outdated/Broken Nodes: {outdatedCount}\\n" +
                $"Disabled Nodes: {disabledCount}\\n" +
                $"Preview-On Nodes: {previewOnCount}\\n\\n" +
                "Connect a Button to inputs to select specific groups.";"""
content = content.replace(report_orig, report_new)

# 5. AddedToDocument
added_orig = """                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 160, 0);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 1, 160, 30);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 2, 160, 60);
                document.ExpireSolution();"""
added_new = """                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 160, 0);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 1, 160, 30);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 2, 160, 60);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 3, 160, 90);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 4, 160, 120);
                document.ExpireSolution();"""
content = content.replace(added_orig, added_new)

with open('Components/CanvasAuditor.cs', 'w') as f:
    f.write(content)
