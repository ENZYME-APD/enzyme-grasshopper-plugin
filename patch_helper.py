import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

new_methods = """        public static void WireIntegerSlider(GH_Component comp, GH_Document doc, int paramIndex, int min, int max, int val, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            Grasshopper.Kernel.Special.GH_NumberSlider slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
            slider.CreateAttributes();
            slider.Slider.Minimum = (decimal)min;
            slider.Slider.Maximum = (decimal)max;
            slider.Slider.Value = (decimal)val;
            slider.Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Integer;
            slider.Slider.DecimalPlaces = 0;
            
            System.Drawing.PointF pivot = comp.Attributes.Pivot;
            slider.Attributes.Pivot = new System.Drawing.PointF(pivot.X - offsetX, pivot.Y + offsetY);
            
            doc.AddObject(slider, false);
            comp.Params.Input[paramIndex].AddSource(slider);
        }

        public static void WireFilePath(GH_Component comp, GH_Document doc, int paramIndex, string defaultPath, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            Grasshopper.Kernel.Parameters.Param_FilePath param = new Grasshopper.Kernel.Parameters.Param_FilePath();
            param.CreateAttributes();
            
            System.Drawing.PointF pivot = comp.Attributes.Pivot;
            param.Attributes.Pivot = new System.Drawing.PointF(pivot.X - offsetX, pivot.Y + offsetY);
            
            doc.AddObject(param, false);
            comp.Params.Input[paramIndex].AddSource(param);
        }
"""

content = content.replace("public static void WireSlider", new_methods + "\n        public static void WireSlider")

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)
