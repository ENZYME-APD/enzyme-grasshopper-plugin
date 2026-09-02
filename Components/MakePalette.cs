using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json;

namespace Enzyme.Components
{
    public class MakePalette : GH_Component
    {
        public MakePalette()
          : base("JSON Palette Builder", "MakePalette",
              "Zips strings and GH Colors into a JSON dictionary.",
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
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -11, 180, 22);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Programs", "Programs", "Names of the architectural programs.", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "System.Drawing.Color objects from GH Swatches.", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Palette", "JSON_Palette", "Formatted JSON string ready for the OOP Engine.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> progs = new List<string>();
            List<Color> colors = new List<Color>();

            bool hasProgs = DA.GetDataList(0, progs);
            bool hasColors = DA.GetDataList(1, colors);

            if (!hasProgs || !hasColors || progs.Count == 0 || colors.Count == 0)
            {
                DA.SetData(0, "{\n}");
                Message = "Awaiting Data";
                return;
            }

            int limit = Math.Min(progs.Count, colors.Count);
            var paletteDict = new Dictionary<string, int[]>();

            for (int i = 0; i < limit; i++)
            {
                string pName = progs[i]?.Trim();
                if (string.IsNullOrEmpty(pName)) continue;
                
                Color c = colors[i];
                paletteDict[pName] = new int[] { c.R, c.G, c.B };
            }

            string jsonStr = JsonConvert.SerializeObject(paletteDict, Formatting.Indented);
            
            string msg = $"Mapped {limit} Programs";
            if (progs.Count != colors.Count)
            {
                msg += "\nWarning: List length mismatch!";
            }
            
            Message = msg;
            
            DA.SetData(0, jsonStr);
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("MakePalette.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("d67c9c22-b43a-4f51-b062-8be2f42e2098"); }
        }
    }
}
