using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;

namespace Enzyme.Components
{
    public class InitKeysComponent : GH_Component
    {
        public InitKeysComponent()
          : base("BIM Key Initializer", "INITKEYS",
              "Safely injects default BIM attributes into referenced Rhino curves.",
              Enzyme.Utils.TabInfo.TabName, "Masterplan (Beta)")
        {
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
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 1, 210, 0);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Guids", "G", "Referenced Rhino geometry IDs.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "R", "Wire a Button here to execute the injection.", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Guids", "G", "Pass-through for the Serializer.", GH_ParamAccess.list);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();

            List<Grasshopper.Kernel.Types.IGH_Goo> guidsInGoo = new List<Grasshopper.Kernel.Types.IGH_Goo>();
            DA.GetDataList(0, guidsInGoo);

            bool runBtn = false;
            DA.GetData(1, ref runBtn);

            List<string> statusMsg = new List<string>();
            List<Guid> cleanGuids = new List<Guid>();

            foreach (var goo in guidsInGoo)
            {
                if (goo == null) continue;
                
                Guid g = Guid.Empty;
                if (goo.CastTo<Guid>(out g))
                {
                    if (g != Guid.Empty)
                        cleanGuids.Add(g);
                }
                else if (goo is Grasshopper.Kernel.Types.GH_String ghStr)
                {
                    if (Guid.TryParse(ghStr.Value, out Guid parsedG) && parsedG != Guid.Empty)
                        cleanGuids.Add(parsedG);
                }
                else if (goo is Grasshopper.Kernel.Types.GH_Guid ghGuid)
                {
                    if (ghGuid.Value != Guid.Empty)
                        cleanGuids.Add(ghGuid.Value);
                }
            }

            if (cleanGuids.Count == 0)
            {
                statusMsg.Add("Error: No Curves Connected");
            }
            else if (!runBtn)
            {
                statusMsg.Add("Ready.");
                statusMsg.Add("Press Button.");
            }
            else
            {
                RhinoDoc doc = RhinoDoc.ActiveDoc;
                int objectsModified = 0;
                int invalidObjects = 0;

                var defaultSchema = new Dictionary<string, string>
                {
                    { "BuildingID", "Building_01" },
                    { "TowerID", "Main_Tower" },
                    { "Program", "Residential" },
                    { "Phase", "0" },
                    { "Floors", "1" },
                    { "FloorHeight", "4.0" }
                };

                foreach (Guid item in cleanGuids)
                {
                    RhinoObject obj = doc?.Objects.FindId(item);
                    if (obj == null)
                    {
                        invalidObjects++;
                        continue;
                    }

                    bool modified = false;

                    foreach (var kvp in defaultSchema)
                    {
                        string existingVal = obj.Attributes.GetUserString(kvp.Key);
                        if (existingVal == null)
                        {
                            obj.Attributes.SetUserString(kvp.Key, kvp.Value);
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        obj.CommitChanges();
                        objectsModified++;
                    }
                }

                if (invalidObjects > 0 && objectsModified == 0)
                {
                    statusMsg.Add($"Failed: {invalidObjects} unreferenced");
                    statusMsg.Add("(Check Type Hint = Guid)");
                }
                else if (invalidObjects > 0)
                {
                    statusMsg.Add($"Injected: {objectsModified}");
                    statusMsg.Add($"Skipped: {invalidObjects} (Not ref'd)");
                }
                else
                {
                    statusMsg.Add($"Success: {objectsModified} objects");
                }
            }

            sw.Stop();
            long execTime = sw.ElapsedMilliseconds;

            string finalMsg = $"{this.NickName}\nTime: {execTime} ms\n---\n" + string.Join("\n", statusMsg);
            this.Message = finalMsg;

            DA.SetDataList(0, cleanGuids);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("INITKEYS.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("14d6ea4e-1234-4bc9-93af-e0dc2857053e"); }
        }
    }
}
