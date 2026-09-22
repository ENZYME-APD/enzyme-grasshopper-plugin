import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

new_method = """        public static void WireOutputPanel(GH_Component comp, GH_Document doc, int paramIndex, int offsetX, int offsetY, int width = 120, int height = 40)
        {
            if (paramIndex >= comp.Params.Output.Count) return;
            if (comp.Params.Output[paramIndex].Recipients.Count > 0) return;

            Grasshopper.Kernel.Special.GH_Panel panel = new Grasshopper.Kernel.Special.GH_Panel();
            panel.CreateAttributes();
            panel.Properties.Multiline = false;
            
            panel.Attributes.Bounds = new System.Drawing.RectangleF(0, 0, width, height);

            System.Drawing.PointF compPivot = comp.Attributes.Pivot;
            panel.Attributes.Pivot = new System.Drawing.PointF(compPivot.X + offsetX, compPivot.Y + offsetY);

            doc.AddObject(panel, false);
            panel.AddSource(comp.Params.Output[paramIndex]);
        }

        public static void WireOutputParam"""

content = content.replace("        public static void WireOutputParam", new_method)

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)

