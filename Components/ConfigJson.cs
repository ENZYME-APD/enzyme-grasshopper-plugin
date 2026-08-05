using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json;

namespace Enzyme.Components
{
    public class ConfigJsonComponent : GH_Component
    {
        public ConfigJsonComponent()
          : base("Master JSON Config Builder", "ConfigJSON",
              "Generates Target and Palette JSONs from 3 parallel lists.",
              "Enzyme", "Masterplan")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Programs", "Programs", "Names of the architectural programs.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Targets", "Targets", "Target area numbers (0 will be ignored).", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "System.Drawing.Color from GH Swatches.", GH_ParamAccess.list);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Targets", "JSON_Targets", "Connect to the Dashboard's TargetJSON input.", GH_ParamAccess.item);
            pManager.AddTextParameter("JSON_Palette", "JSON_Palette", "Connect to the OOP Engine's ColorPalette input.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> progs = new List<string>();
            List<double> targets = new List<double>();
            List<Color> colors = new List<Color>();

            DA.GetDataList(0, progs);
            DA.GetDataList(1, targets);
            DA.GetDataList(2, colors);

            if (progs.Count == 0)
            {
                DA.SetData(0, "{\n}");
                DA.SetData(1, "{\n}");
                this.Message = this.NickName + "\n" + "Awaiting Programs Data";
                return;
            }

            var targetDict = new Dictionary<string, double>();
            var paletteDict = new Dictionary<string, int[]>();

            for (int i = 0; i < progs.Count; i++)
            {
                string pName = progs[i]?.Trim();
                if (string.IsNullOrEmpty(pName)) continue;

                if (i < targets.Count)
                {
                    double tVal = targets[i];
                    if (tVal > 0)
                    {
                        targetDict[pName] = tVal;
                    }
                }

                if (i < colors.Count)
                {
                    Color c = colors[i];
                    paletteDict[pName] = new int[] { c.R, c.G, c.B };
                }
            }

            string jsonTargets = JsonConvert.SerializeObject(targetDict, Formatting.Indented);
            string jsonPalette = JsonConvert.SerializeObject(paletteDict, Formatting.Indented);

            DA.SetData(0, jsonTargets);
            DA.SetData(1, jsonPalette);

            string msg = $"Mapped:\n{targetDict.Count} Targets\n{paletteDict.Count} Colors";

            if (progs.Count != targets.Count || progs.Count != colors.Count)
            {
                msg += "\n(Warning: List lengths differ)";
            }

            this.Message = this.NickName + "\n" + msg;
        }

        protected override Bitmap Icon => IconLoader.Load("ConfigJSON.png");

        public override Guid ComponentGuid => new Guid("4e1837f4-d8bc-4cc0-843b-bd9fc1d234a5");
    }
}
