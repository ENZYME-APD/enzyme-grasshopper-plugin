import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

new_method = """        public static void WireSlider1Dec(GH_Component comp, GH_Document doc, int paramIndex, double min, double max, double val, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            var slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
            slider.CreateAttributes();
            slider.Slider.Minimum = (decimal)min;
            slider.Slider.Maximum = (decimal)max;
            slider.Slider.Value = (decimal)val;
            slider.Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Float;
            slider.Slider.DecimalPlaces = 1;

            float x = comp.Attributes.Pivot.X - offsetX;
            float y = comp.Attributes.Pivot.Y + offsetY;
            slider.Attributes.Pivot = new System.Drawing.PointF(x, y);
            
            doc.AddObject(slider, false);
            comp.Params.Input[paramIndex].AddSource(slider);
        }
"""

# Insert the new method before the last two closing braces
content = re.sub(r'(\s*\}\s*\})$', r'\n\n' + new_method + r'\1', content)

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)
