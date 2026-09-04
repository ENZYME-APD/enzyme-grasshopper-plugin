import re

with open("Utils/AutoWireHelper.cs", "r") as f:
    ts = f.read()

new_method = '''        public static void WireSliderInt(GH_Component comp, GH_Document doc, int paramIndex, int min, int max, int val, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            var slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
            slider.CreateAttributes();
            slider.Slider.Minimum = (decimal)min;
            slider.Slider.Maximum = (decimal)max;
            slider.Slider.Value = (decimal)val;
            slider.Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Integer;
            slider.Slider.DecimalPlaces = 0;

            float x = comp.Attributes.Pivot.X - offsetX;
            float y = comp.Attributes.Pivot.Y + offsetY;
            slider.Attributes.Pivot = new System.Drawing.PointF(x, y);
            
            doc.AddObject(slider, false);
            comp.Params.Input[paramIndex].AddSource(slider);
        }'''

if "WireSliderInt" not in ts:
    ts = ts.replace("public static void WireSlider1Dec", new_method + "\n\n        public static void WireSlider1Dec")
    with open("Utils/AutoWireHelper.cs", "w") as f:
        f.write(ts)
    print("Added WireSliderInt")
else:
    print("WireSliderInt already exists")
