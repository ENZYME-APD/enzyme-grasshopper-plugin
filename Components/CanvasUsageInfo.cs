using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Enzyme.Components
{
    public class CanvasUsageInfo : GH_Component
    {
        public CanvasUsageInfo()
          : base("Canvas Usage Info", "CanvasUsage",
              "Retrieves plug-in libraries used and counts all components being used in the definition.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Button to update the info panels", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            if (!DA.GetData(0, ref run) || !run) return;

            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            // 1. Plugins
            var coreLibraries = new Dictionary<Guid, GH_AssemblyInfo>();
            var addonLibraries = new Dictionary<Guid, GH_AssemblyInfo>();
            var objIds = new HashSet<Guid>();

            var server = Grasshopper.Instances.ComponentServer;
            foreach (var obj in doc.Objects)
            {
                if (objIds.Contains(obj.ComponentGuid)) continue;
                objIds.Add(obj.ComponentGuid);

                var lib = server.FindAssemblyByObject(obj);
                if (lib == null) continue;
                if (coreLibraries.ContainsKey(lib.Id) || addonLibraries.ContainsKey(lib.Id)) continue;

                if (lib.IsCoreLibrary)
                    coreLibraries[lib.Id] = lib;
                else
                    addonLibraries[lib.Id] = lib;
            }

            List<string> pluginStrs = new List<string>();
            foreach (var lib in addonLibraries.Values)
            {
                if (!string.IsNullOrEmpty(lib.Name))
                {
                    pluginStrs.Add($"{lib.Name} {lib.Version}");
                }
            }
            string pluginText = string.Join("\n", pluginStrs);

            // 2. Component Usage
            int compCount = 0;
            var dict = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
            
            foreach (var obj in doc.Objects)
            {
                if (obj is GH_Group) continue;
                
                compCount++;
                string cat = obj.Category ?? "Unknown";
                string sub = obj.SubCategory ?? "Unknown";
                string name = obj.Name ?? "Unknown";

                if (!dict.ContainsKey(cat)) dict[cat] = new Dictionary<string, Dictionary<string, int>>();
                if (!dict[cat].ContainsKey(sub)) dict[cat][sub] = new Dictionary<string, int>();
                if (!dict[cat][sub].ContainsKey(name)) dict[cat][sub][name] = 0;

                dict[cat][sub][name]++;
            }

            string userName = Environment.UserName;
            string dateStr = DateTime.Now.ToString("DATE- yyyy.MM.dd TIME- HH:mmh");
            string compsText = $"{dateStr}\nLast saved by - {userName}\nTotal Components - {compCount}\n";

            foreach (var cat in dict.OrderBy(k => k.Key))
            {
                compsText += $"\n{cat.Key}:\n";
                foreach (var sub in cat.Value.OrderBy(k => k.Key))
                {
                    compsText += $"   {sub.Key}:\n";
                    foreach (var comp in sub.Value.OrderBy(k => k.Key))
                    {
                        compsText += $"      {comp.Key} - {comp.Value}\n";
                    }
                }
            }

            CheckOrMakePanel(doc, "PlugIns Used", pluginText, Color.LightSkyBlue, new PointF(-690, 0), new RectangleF(-330, 0, 330, 150));
            CheckOrMakePanel(doc, "All Components Used", compsText, Color.LightSkyBlue, new PointF(-345, 0), new RectangleF(-330, 0, 330, 450));
        }

        private void CheckOrMakePanel(GH_Document doc, string nickname, string text, Color color, PointF pivot, RectangleF bounds)
        {
            GH_Panel panel = null;
            foreach (var obj in doc.Objects)
            {
                if (obj is GH_Panel p && p.NickName == nickname)
                {
                    panel = p;
                    break;
                }
            }

            if (panel == null)
            {
                panel = new GH_Panel();
                panel.CreateAttributes();
                panel.NickName = nickname;
                panel.Properties.Colour = color;
                panel.Properties.Multiline = false;
                
                panel.Attributes.Pivot = pivot;
                panel.Attributes.Bounds = bounds;
                
                doc.AddObject(panel, false, doc.ObjectCount + 1);
            }

            panel.UserText = text;
            panel.ExpireSolution(true);
        }

        public override Guid ComponentGuid => new Guid("C3D4E5F6-A1B2-4C3D-0E1F-A2B3C4D5E6F7");
    }
}
