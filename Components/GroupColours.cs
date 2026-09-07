using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Enzyme.Components
{
    public class GroupColours : GH_Component
    {
        public GroupColours()
          : base("Group Colours", "GroupColours",
              "Changes the colors of groups on the canvas based on their names. Case-insensitive. Multi-separator.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Group Names", "Names", "List of group name prefixes", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "List of colors corresponding to the names", GH_ParamAccess.list);
            pManager.AddColourParameter("Default Color", "Default", "Default color for unmatched groups", GH_ParamAccess.item, Color.FromArgb(255, 214, 206, 206));
            
            pManager[0].Optional = true;
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> names = new List<string>();
            List<Color> colors = new List<Color>();
            Color defaultColor = Color.Empty;

            DA.GetDataList(0, names);
            DA.GetDataList(1, colors);
            DA.GetData(2, ref defaultColor);

            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            char[] separators = new char[] { '-', '_', ':', '|' };

            foreach (IGH_DocumentObject obj in doc.Objects)
            {
                if (obj is GH_Group group)
                {
                    string groupName = group.NickName.ToLowerInvariant();
                    string key = groupName.Split(separators)[0].Trim();

                    bool matched = false;
                    for (int i = 0; i < names.Count; i++)
                    {
                        if (string.Equals(key, names[i].Trim(), StringComparison.InvariantCultureIgnoreCase))
                        {
                            Color c = i < colors.Count ? colors[i] : colors.LastOrDefault();
                            group.Colour = c;
                            matched = true;
                            break;
                        }
                    }

                    if (!matched)
                    {
                        group.Colour = defaultColor;
                    }
                }
            }
            
            this.Message = "Case-insensitive\nMulti-separator";
        }

        public override Guid ComponentGuid => new Guid("A1B2C3D4-E5F6-4A1B-8C9D-E0F1A2B3C4D5");
    }
}
