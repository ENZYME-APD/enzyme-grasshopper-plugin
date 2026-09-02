import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

new_method = """        public static void WireBooleanToggle(GH_Component component, GH_Document doc, int paramIndex, bool defaultVal, int offsetX, int offsetY)
        {
            var toggle = new Grasshopper.Kernel.Special.GH_BooleanToggle();
            toggle.CreateAttributes();
            toggle.Value = defaultVal;
            
            float x = component.Attributes.Pivot.X - offsetX;
            float y = component.Attributes.Pivot.Y + offsetY;
            toggle.Attributes.Pivot = new System.Drawing.PointF(x, y);
            toggle.Attributes.Bounds = new System.Drawing.RectangleF(x - 15, y - 10, 30, 20);
            
            doc.AddObject(toggle, false);
            component.Params.Input[paramIndex].AddSource(toggle);
        }
    }
}"""
content = re.sub(r'\s*\}\s*\}$', '\n' + new_method, content)

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)
