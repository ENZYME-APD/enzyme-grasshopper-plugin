import os

# --- Update CanvasStyle.cs ---
style_path = "Components/CanvasStyle.cs"
with open(style_path, "r") as f:
    style_content = f.read()

style_content = style_content.replace(
    'Color.FromArgb(255, 255, 250, 90)',
    'Color.FromArgb(255, 255, 255, 255)'
).replace(
    'Color.FromArgb(255, 212, 208, 200)',
    'Color.FromArgb(255, 222, 220, 220)'
).replace(
    'Color.FromArgb(30, 0, 0, 0)',
    'Color.FromArgb(255, 224, 175, 175)'
)

with open(style_path, "w") as f:
    f.write(style_content)

# --- Update GroupColours.cs ---
group_path = "Components/GroupColours.cs"
with open(group_path, "r") as f:
    group_content = f.read()

new_register_inputs = """        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddColourParameter("Default Color", "Default", "Default color for unmatched groups", GH_ParamAccess.item, Color.FromArgb(255, 214, 206, 206));
            pManager.AddTextParameter("Group Names", "Names", "List of group name prefixes", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "List of colors corresponding to the names", GH_ParamAccess.list);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }"""

new_solve_instance = """        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Color defaultColor = Color.Empty;
            List<string> names = new List<string>();
            List<Color> colors = new List<Color>();

            DA.GetData(0, ref defaultColor);
            DA.GetDataList(1, names);
            DA.GetDataList(2, colors);

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
            
            this.Message = "Case-insensitive\\nMulti-separator";
        }"""

new_added_to_doc = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[0].SourceCount == 0)
            {
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 0, Color.FromArgb(255, 214, 206, 206), 120, -40);
            }
            if (this.Params.Input[1].SourceCount == 0)
            {
                string names = "Parameters\\nProcess\\nAnalysis\\nOutput\\nBake\\nDisplay\\nArchicad\\nSpeckle\\nAnnotation\\nTest";
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 1, names, 120, 0, 100, 160);
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
        }"""

import re
group_content = re.sub(r'protected override void RegisterInputParams.*?}\s+', new_register_inputs + '\n\n', group_content, flags=re.DOTALL)
group_content = re.sub(r'protected override void SolveInstance.*?this\.Message = "Case-insensitive\\nMulti-separator";\n        }', new_solve_instance, group_content, flags=re.DOTALL)
group_content = re.sub(r'public override void AddedToDocument.*?public override Guid', new_added_to_doc + '\n\n        public override Guid', group_content, flags=re.DOTALL)

with open(group_path, "w") as f:
    f.write(group_content)

