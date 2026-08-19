using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Components;

namespace Enzyme.Utils
{
    public static class AutoWireHelper
    {
        public static void WireSlider(GH_Component comp, GH_Document doc, int paramIndex, double min, double max, double val, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            GH_NumberSlider slider = new GH_NumberSlider();
            slider.CreateAttributes();
            slider.Slider.Minimum = (decimal)min;
            slider.Slider.Maximum = (decimal)max;
            slider.Slider.Value = (decimal)val;
            slider.Slider.Type = GH_SliderAccuracy.Float;
            slider.Slider.DecimalPlaces = 2;
            
            PointF pivot = comp.Attributes.Pivot;
            slider.Attributes.Pivot = new PointF(pivot.X - offsetX, pivot.Y + offsetY);
            
            doc.AddObject(slider, false);
            comp.Params.Input[paramIndex].AddSource(slider);
        }

        public static void WireToggle(GH_Component comp, GH_Document doc, int paramIndex, bool val, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            GH_BooleanToggle toggle = new GH_BooleanToggle();
            toggle.CreateAttributes();
            toggle.Value = val;

            PointF pivot = comp.Attributes.Pivot;
            toggle.Attributes.Pivot = new PointF(pivot.X - offsetX, pivot.Y + offsetY);

            doc.AddObject(toggle, false);
            comp.Params.Input[paramIndex].AddSource(toggle);
        }

        public static void WirePanel(GH_Component comp, GH_Document doc, int paramIndex, string text, int offsetX, int offsetY, int width = 120, int height = 60)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            GH_Panel panel = new GH_Panel();
            panel.CreateAttributes();
            panel.UserText = text;
            
            panel.Attributes.Bounds = new RectangleF(0, 0, width, height);

            PointF compPivot = comp.Attributes.Pivot;
            panel.Attributes.Pivot = new PointF(compPivot.X - offsetX, compPivot.Y + offsetY);

            doc.AddObject(panel, false);
            comp.Params.Input[paramIndex].AddSource(panel);
        }

        public static void WireCustomPreview(GH_Component comp, GH_Document doc, int paramIndex, Color color, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Output.Count) return;
            if (comp.Params.Output[paramIndex].Recipients.Count > 0) return;

            GH_CustomPreviewComponent preview = new GH_CustomPreviewComponent();
            preview.CreateAttributes();
            PointF compPivot = comp.Attributes.Pivot;
            preview.Attributes.Pivot = new PointF(compPivot.X + offsetX, compPivot.Y + offsetY);
            
            GH_ColourSwatch swatch = new GH_ColourSwatch();
            swatch.CreateAttributes();
            swatch.SwatchColour = color;
            swatch.Attributes.Pivot = new PointF(preview.Attributes.Pivot.X - 90, preview.Attributes.Pivot.Y);

            doc.AddObject(preview, false);
            doc.AddObject(swatch, false);
            
            preview.Params.Input[1].AddSource(swatch);
            preview.Params.Input[0].AddSource(comp.Params.Output[paramIndex]);
        }
    }
}
