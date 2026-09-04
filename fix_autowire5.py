with open("Utils/AutoWireHelper.cs", "r") as f:
    awh = f.read()

wire_human_fix = '''        public static void WireHumanCurvePreviewToParam(Grasshopper.Kernel.IGH_Param param, GH_Document doc, System.Drawing.Color color, double thickness, int offsetX, int offsetY)
        {
            var server = Grasshopper.Instances.ComponentServer;
            Grasshopper.Kernel.IGH_ObjectProxy proxy = null;
            foreach (var p in server.ObjectProxies)
            {
                if (p.Desc.Name == "Custom Preview Lineweights")
                {
                    proxy = p;
                    break;
                }
            }

            if (proxy == null)
            {
                return;
            }

            var preview = proxy.CreateInstance() as Grasshopper.Kernel.GH_Component;
            if (preview == null) return;

            preview.CreateAttributes();
            System.Drawing.PointF compPivot = param.Attributes.Pivot;
            preview.Attributes.Pivot = new System.Drawing.PointF(compPivot.X + offsetX, compPivot.Y + offsetY);

            Grasshopper.Kernel.Special.GH_ColourSwatch swatch = new Grasshopper.Kernel.Special.GH_ColourSwatch();
            swatch.CreateAttributes();
            swatch.SwatchColour = color;
            swatch.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 100, preview.Attributes.Pivot.Y - 25);
            
            Grasshopper.Kernel.Special.GH_NumberSlider slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
            slider.CreateAttributes();
            slider.Slider.Minimum = 0.0m;
            slider.Slider.Maximum = 2.0m;
            slider.Slider.Value = (decimal)thickness;
            slider.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 180, preview.Attributes.Pivot.Y + 15);

            Grasshopper.Kernel.Special.GH_BooleanToggle toggle = new Grasshopper.Kernel.Special.GH_BooleanToggle();
            toggle.CreateAttributes();
            toggle.Value = false;
            toggle.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 100, preview.Attributes.Pivot.Y + 45);

            doc.AddObject(preview, false);
            doc.AddObject(swatch, false);
            doc.AddObject(slider, false);
            doc.AddObject(toggle, false);

            preview.Params.Input[0].AddSource(param);
            preview.Params.Input[1].AddSource(swatch);
            preview.Params.Input[2].AddSource(slider);
            preview.Params.Input[3].AddSource(toggle);
        }
'''

awh = awh.replace("    }\\n}\\n", wire_human_fix + "    }\\n}\\n")

with open("Utils/AutoWireHelper.cs", "w") as f:
    f.write(awh)
