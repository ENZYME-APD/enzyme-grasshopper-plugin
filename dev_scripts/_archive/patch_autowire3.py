import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

if "WireMultilinePanel" not in content:
    new_method = """
        public static void WireMultilinePanel(GH_Component comp, GH_Document doc, int paramIndex, string text, int offsetX, int offsetY, int width = 120, int height = 80)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            Grasshopper.Kernel.Special.GH_Panel panel = new Grasshopper.Kernel.Special.GH_Panel();
            panel.CreateAttributes();
            panel.UserText = text;
            panel.Properties.Multiline = true;
            
            panel.Attributes.Bounds = new System.Drawing.RectangleF(0, 0, width, height);

            System.Drawing.PointF compPivot = comp.Attributes.Pivot;
            panel.Attributes.Pivot = new System.Drawing.PointF(compPivot.X - offsetX, compPivot.Y + offsetY);

            doc.AddObject(panel, false);
            comp.Params.Input[paramIndex].AddSource(panel);
        }
"""
    content = re.sub(r'(\s*}\s*}\s*)$', new_method + r'\1', content)
    with open('Utils/AutoWireHelper.cs', 'w') as f:
        f.write(content)
