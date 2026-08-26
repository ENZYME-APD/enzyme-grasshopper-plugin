import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

if "WireCurvePreview" not in content:
    new_method = """
        public static void WireCurvePreview(GH_Component comp, GH_Document doc, int paramIndex, System.Drawing.Color color, double thickness, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Output.Count) return;
            if (comp.Params.Output[paramIndex].Recipients.Count > 0) return;

            Grasshopper.Kernel.Components.GH_CustomPreviewComponent preview = new Grasshopper.Kernel.Components.GH_CustomPreviewComponent();
            preview.CreateAttributes();
            System.Drawing.PointF compPivot = comp.Attributes.Pivot;
            preview.Attributes.Pivot = new System.Drawing.PointF(compPivot.X + offsetX, compPivot.Y + offsetY);

            Grasshopper.Kernel.Special.GH_ColourSwatch swatch = new Grasshopper.Kernel.Special.GH_ColourSwatch();
            swatch.CreateAttributes();
            swatch.SwatchColour = color;
            swatch.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 100, preview.Attributes.Pivot.Y - 10);
            
            doc.AddObject(preview, false);
            doc.AddObject(swatch, false);
            
            preview.Params.Input[1].AddSource(swatch);
            preview.Params.Input[0].AddSource(comp.Params.Output[paramIndex]);

            if (preview.Params.Input.Count > 2)
            {
                Grasshopper.Kernel.Special.GH_NumberSlider slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
                slider.CreateAttributes();
                slider.Slider.Minimum = 0.0m;
                slider.Slider.Maximum = 2.0m;
                slider.Slider.Value = (decimal)thickness;
                slider.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 150, preview.Attributes.Pivot.Y + 30);
                doc.AddObject(slider, false);
                preview.Params.Input[2].AddSource(slider);

                if (preview.Params.Input.Count > 3)
                {
                    Grasshopper.Kernel.Special.GH_BooleanToggle toggle = new Grasshopper.Kernel.Special.GH_BooleanToggle();
                    toggle.CreateAttributes();
                    toggle.Value = true;
                    toggle.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 100, preview.Attributes.Pivot.Y + 60);
                    doc.AddObject(toggle, false);
                    preview.Params.Input[3].AddSource(toggle);
                }
            }
        }
"""
    # Find closing brace of class
    content = re.sub(r'(\s*}\s*}\s*)$', new_method + r'\1', content)
    with open('Utils/AutoWireHelper.cs', 'w') as f:
        f.write(content)

