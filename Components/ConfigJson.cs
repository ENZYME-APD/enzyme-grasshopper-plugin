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
              Enzyme.Utils.TabInfo.TabName, "Masterplan (Beta)")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 0, "Office\nResidential\nServ.Apt\nRetail\nAmenities\nHotel\nPodium\nParking\nDefault", 250, -80, 100, 150);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 1, "30000\n56500\n5000\n150000\n300000\n10000\n15000\n60000\n0", 140, -80, 100, 150);
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(80, 180, 220),
                    System.Drawing.Color.FromArgb(160, 120, 180),
                    System.Drawing.Color.FromArgb(160, 220, 80),
                    System.Drawing.Color.FromArgb(250, 180, 100),
                    System.Drawing.Color.FromArgb(230, 130, 170),
                    System.Drawing.Color.FromArgb(110, 130, 200),
                    System.Drawing.Color.FromArgb(230, 100, 70),
                    System.Drawing.Color.FromArgb(190, 180, 160),
                    System.Drawing.Color.FromArgb(255, 255, 255)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 2, colors, 150, 120);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -34, 180, 22);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 11, 180, 22);
            }
        }

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
