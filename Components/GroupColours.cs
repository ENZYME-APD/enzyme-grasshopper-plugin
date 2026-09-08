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
            pManager.AddColourParameter("Default Color", "Default", "Default color for unmatched groups", GH_ParamAccess.item, Color.FromArgb(255, 214, 206, 206));
            pManager.AddTextParameter("Group Names", "Names", "List of group name prefixes", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "List of colors corresponding to the names", GH_ParamAccess.list);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Color defaultColor = Color.Empty;
            List<string> names = new List<string>();
            List<Color> colors = new List<Color>();

            int idxDefault = Params.IndexOfInputParam("Default Color");
            int idxNames = Params.IndexOfInputParam("Group Names");
            int idxColors = Params.IndexOfInputParam("Colors");

            if (idxDefault != -1) DA.GetData(idxDefault, ref defaultColor);
            if (idxNames != -1) DA.GetDataList(idxNames, names);
            if (idxColors != -1) DA.GetDataList(idxColors, colors);

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
                            Color c = defaultColor;
                            if (colors.Count > 0) {
                                c = i < colors.Count ? colors[i] : colors.Last();
                            }
                            
                            // Ensure it's not totally transparent (Color.Empty)
                            if (c.IsEmpty || c.A == 0) {
                                c = Color.FromArgb(255, 100, 100, 100);
                            }

                            if (group.Colour.ToArgb() != c.ToArgb())
                            {
                                group.Colour = c;
                            }
                            matched = true;
                            break;
                        }
                    }

                    if (!matched)
                    {
                        Color c = defaultColor;
                        if (c.IsEmpty || c.A == 0) {
                            c = Color.FromArgb(255, 214, 206, 206);
                        }

                        if (group.Colour.ToArgb() != c.ToArgb())
                        {
                            group.Colour = c;
                        }
                    }
                }
            }
            
            this.Message = "Group Colours\nCase-insensitive\nMulti-separator";
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[0].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 0, Color.FromArgb(255, 214, 206, 206), 160, -80);
            
            if (this.Params.Input[1].SourceCount == 0)
            {
                string text = "Parameters\nProcess\nAnalysis\nOutput\nBake\nDisplay\nArchicad\nSpeckle\nAnnotation\nTest";
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 1, text, 160, -40, 100, 180);
            }

            if (this.Params.Input[2].SourceCount == 0)
            {
                Color[] defaultColors = new Color[] {
                    Color.FromArgb(255, 64, 64, 64),
                    Color.FromArgb(255, 209, 149, 71),
                    Color.FromArgb(255, 242, 170, 170),
                    Color.FromArgb(255, 112, 255, 198),
                    Color.FromArgb(255, 253, 255, 201),
                    Color.FromArgb(255, 35, 84, 219),
                    Color.FromArgb(255, 214, 175, 154),
                    Color.FromArgb(255, 227, 123, 163),
                    Color.FromArgb(255, 255, 0, 98),
                    Color.FromArgb(255, 255, 0, 98)
                };

                for(int i = 0; i < defaultColors.Length; i++)
                {
                    var swatch = new Grasshopper.Kernel.Special.GH_ColourSwatch();
                    swatch.CreateAttributes();
                    swatch.SwatchColour = defaultColors[i];
                    swatch.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 160, this.Attributes.Pivot.Y + (i * 25) - 40);
                    document.AddObject(swatch, false);
                    this.Params.Input[2].AddSource(swatch);
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("GroupColors.png");

        public override Guid ComponentGuid => new Guid("A1B2C3D4-E5F6-4A1B-8C9D-E0F1A2B3C4D5");
    }
}
