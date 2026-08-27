import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

if "WirePointDisplay" not in content:
    new_method = """
        public static void WirePointDisplay(GH_Component comp, GH_Document doc, int paramIndex, System.Drawing.Color color, double size, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Output.Count) return;
            if (comp.Params.Output[paramIndex].Recipients.Count > 0) return;

            Grasshopper.Kernel.IGH_ObjectProxy proxy = Grasshopper.Instances.ComponentServer.FindObjectByName("Point Display", true, true);
            if (proxy == null) return;

            Grasshopper.Kernel.IGH_Component preview = proxy.CreateInstance() as Grasshopper.Kernel.IGH_Component;
            if (preview == null) return;

            preview.CreateAttributes();
            System.Drawing.PointF compPivot = comp.Attributes.Pivot;
            preview.Attributes.Pivot = new System.Drawing.PointF(compPivot.X + offsetX, compPivot.Y + offsetY);

            Grasshopper.Kernel.Special.GH_ColourSwatch swatch = new Grasshopper.Kernel.Special.GH_ColourSwatch();
            swatch.CreateAttributes();
            swatch.SwatchColour = color;
            swatch.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 100, preview.Attributes.Pivot.Y);
            
            Grasshopper.Kernel.Special.GH_NumberSlider slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
            slider.CreateAttributes();
            slider.Slider.Minimum = 0.0m;
            slider.Slider.Maximum = 20.0m;
            slider.Slider.Value = (decimal)size;
            slider.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 150, preview.Attributes.Pivot.Y + 30);
            
            doc.AddObject(preview, false);
            doc.AddObject(swatch, false);
            doc.AddObject(slider, false);
            
            preview.Params.Input[0].AddSource(comp.Params.Output[paramIndex]);
            preview.Params.Input[1].AddSource(swatch);
            preview.Params.Input[2].AddSource(slider);
        }
"""
    content = re.sub(r'(\s*}\s*}\s*)$', new_method + r'\1', content)
    with open('Utils/AutoWireHelper.cs', 'w') as f:
        f.write(content)
