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
            panel.Properties.Multiline = false;
            
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
            swatch.Attributes.Pivot = new System.Drawing.PointF(preview.Attributes.Pivot.X - 80, preview.Attributes.Pivot.Y + 25);

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

        public static void WireOutputPanel(GH_Component comp, GH_Document doc, int paramIndex, int offsetX, int offsetY, int width = 120, int height = 40)
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
                case "generic": param = new Grasshopper.Kernel.Parameters.Param_GenericObject(); break;
                case "line": param = new Grasshopper.Kernel.Parameters.Param_Line(); break;
            }
            if (param == null) return;

            param.CreateAttributes();
            PointF pivot = comp.Attributes.Pivot;
            param.Attributes.Pivot = new PointF(pivot.X + offsetX, pivot.Y + offsetY);

            doc.AddObject(param, false);
            param.AddSource(comp.Params.Output[paramIndex]);
        }
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
        public static void WireMultilinePanel(GH_Component comp, GH_Document doc, int paramIndex, string text, int offsetX, int offsetY, int width = 120, int height = 80)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            Grasshopper.Kernel.Special.GH_Panel panel = new Grasshopper.Kernel.Special.GH_Panel();
            panel.CreateAttributes();
            panel.UserText = text;
            panel.Properties.Multiline = false;
            
            panel.Attributes.Bounds = new System.Drawing.RectangleF(0, 0, width, height);

            System.Drawing.PointF compPivot = comp.Attributes.Pivot;
            panel.Attributes.Pivot = new System.Drawing.PointF(compPivot.X - offsetX, compPivot.Y + offsetY);

            doc.AddObject(panel, false);
            comp.Params.Input[paramIndex].AddSource(panel);
        }
        public static void WireInputParam(GH_Component comp, GH_Document doc, int paramIndex, string paramType, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            Grasshopper.Kernel.IGH_Param param = null;
            paramType = paramType.ToLower();
            if (paramType == "curve") param = new Grasshopper.Kernel.Parameters.Param_Curve();
            else if (paramType == "point") param = new Grasshopper.Kernel.Parameters.Param_Point();
            else if (paramType == "mesh") param = new Grasshopper.Kernel.Parameters.Param_Mesh();
            else if (paramType == "line") param = new Grasshopper.Kernel.Parameters.Param_Line();

            if (param != null)
            {
                param.CreateAttributes();
                System.Drawing.PointF compPivot = comp.Attributes.Pivot;
                param.Attributes.Pivot = new System.Drawing.PointF(compPivot.X - offsetX, compPivot.Y + offsetY);
                doc.AddObject(param, false);
                comp.Params.Input[paramIndex].AddSource(param);
            }
        }
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




    }
}
