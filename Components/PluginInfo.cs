using System;
using System.IO;
using System.Reflection;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class PluginInfo : GH_Component
    {
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public PluginInfo()
          : base("Plugin info", "EnzVer",
              "Outputs the current version and build date of the Enzyme plugin.",
              "Enzyme", "Info")
        {
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Output)
                if (param.Recipients.Count > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                AutoWireDefaultInputs(document);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // No inputs required
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Version", "V", "Current plugin version", GH_ParamAccess.item);
            pManager.AddTextParameter("Build Date", "D", "Date and time of the last plugin update", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Assembly assembly = Assembly.GetExecutingAssembly();
            
            // Get Version
            string version = assembly.GetName().Version.ToString();
            
            // Get Build Date based on file creation/modified time
            string buildDate = "Unknown";
            try
            {
                string location = assembly.Location;
                if (!string.IsNullOrEmpty(location))
                {
                    DateTime lastWriteTime = File.GetLastWriteTime(location);
                    buildDate = lastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch
            {
                // Fallback if unable to read file info
            }

            DA.SetData(0, version);
            DA.SetData(1, buildDate);
        
            stopwatch.Stop();
            Message = $"{this.NickName}\n{stopwatch.Elapsed.TotalMilliseconds:F2} ms\n---\nDone";
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                try {
                    return IconLoader.Load("PluginInfo.png");
                } catch {
                    return null;
                }
            }
        }

        
        private void AutoWireDefaultInputs(GH_Document document)
        {
                int ox = 120;
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, ox, -45, 180, 40);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, ox, 5, 180, 40);
            }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc != null) AutoWireDefaultInputs(doc);
            });
        }

        public override Guid ComponentGuid => new Guid("fa77cbfa-e422-4e16-8a46-b447dd425067"); // Ensure unique GUID
    }
}
