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
            slider.Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Float;
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
        public static void WireColorSwatch(GH_Component comp, GH_Document doc, int paramIndex, Color color, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            GH_ColourSwatch swatch = new GH_ColourSwatch();
            swatch.CreateAttributes();
            swatch.SwatchColour = color;

            PointF pivot = comp.Attributes.Pivot;
            swatch.Attributes.Pivot = new PointF(pivot.X - offsetX, pivot.Y + offsetY);

            doc.AddObject(swatch, false);
            comp.Params.Input[paramIndex].AddSource(swatch);
        }

        public static void WireButton(GH_Component comp, GH_Document doc, int paramIndex, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            GH_ButtonObject btn = new GH_ButtonObject();
            btn.CreateAttributes();

            PointF pivot = comp.Attributes.Pivot;
            btn.Attributes.Pivot = new PointF(pivot.X - offsetX, pivot.Y + offsetY);

            doc.AddObject(btn, false);
            comp.Params.Input[paramIndex].AddSource(btn);
        }

        public static void WireValueList(GH_Component comp, GH_Document doc, int paramIndex, string[] keys, string[] values, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            GH_ValueList vl = new GH_ValueList();
            vl.CreateAttributes();
            vl.ListItems.Clear();
            for (int i = 0; i < keys.Length; i++)
            {
                vl.ListItems.Add(new GH_ValueListItem(keys[i], values[i]));
            }

            PointF pivot = comp.Attributes.Pivot;
            vl.Attributes.Pivot = new PointF(pivot.X - offsetX, pivot.Y + offsetY);

            doc.AddObject(vl, false);
            comp.Params.Input[paramIndex].AddSource(vl);
        }

        public static void WireOutputParam(GH_Component comp, GH_Document doc, int paramIndex, string paramType, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Output.Count) return;
            if (comp.Params.Output[paramIndex].Recipients.Count > 0) return;

            IGH_Param param = null;
            switch(paramType.ToLower())
            {
                case "curve": param = new Grasshopper.Kernel.Parameters.Param_Curve(); break;
                case "point": param = new Grasshopper.Kernel.Parameters.Param_Point(); break;
                case "mesh": param = new Grasshopper.Kernel.Parameters.Param_Mesh(); break;
                case "brep": param = new Grasshopper.Kernel.Parameters.Param_Brep(); break;
                case "surface": param = new Grasshopper.Kernel.Parameters.Param_Surface(); break;
                case "integer": param = new Grasshopper.Kernel.Parameters.Param_Integer(); break;
                case "number": param = new Grasshopper.Kernel.Parameters.Param_Number(); break;
                case "string": param = new Grasshopper.Kernel.Parameters.Param_String(); break;
                case "color": param = new Grasshopper.Kernel.Parameters.Param_Colour(); break;
            }
            if (param == null) return;

            param.CreateAttributes();
            PointF pivot = comp.Attributes.Pivot;
            param.Attributes.Pivot = new PointF(pivot.X + offsetX, pivot.Y + offsetY);

            doc.AddObject(param, false);
            param.AddSource(comp.Params.Output[paramIndex]);
        }
    }
}
