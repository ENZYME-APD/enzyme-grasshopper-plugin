using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class AutoListItem : GH_Component
    {
        private bool _wrap = true;

        public AutoListItem()
          : base("Auto List Item", "AutoItem",
              "Extracts an item from a list. Can automatically wire and adjust a slider to match the list's length.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("List", "L", "List to extract item from", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Index", "i", "Index of the item to retrieve", GH_ParamAccess.item, 0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Item", "I", "Extracted item", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<IGH_Goo> list = new List<IGH_Goo>();
            if (!DA.GetDataList(0, list)) return;
            if (list.Count == 0) return;

            int index = 0;
            if (!DA.GetData(1, ref index)) return;

            // Automatically update the domain of the connected slider
            var indexParam = Params.Input[1];
            if (indexParam.SourceCount > 0)
            {
                if (indexParam.Sources[0] is Grasshopper.Kernel.Special.GH_NumberSlider slider)
                {
                    int maxVal = list.Count - 1;
                    if (maxVal < 0) maxVal = 0;

                    if ((int)slider.Slider.Maximum != maxVal)
                    {
                        GH_Document doc = OnPingDocument();
                        if (doc != null)
                        {
                            // Schedule a solution to safely modify the slider outside of the current solve instance
                            doc.ScheduleSolution(5, (d) =>
                            {
                                slider.Slider.Maximum = maxVal;
                                if (slider.Slider.Value > maxVal)
                                    slider.Slider.Value = maxVal;
                                slider.ExpireSolution(false);
                            });
                        }
                    }
                }
            }

            int count = list.Count;
            if (_wrap)
            {
                index = index % count;
                if (index < 0) index += count;
            }
            else
            {
                if (index < 0) index = 0;
                if (index >= count) index = count - 1;
            }

            DA.SetData(0, list[index]);
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Wrap Indices", (s, e) => { _wrap = !_wrap; ExpireSolution(true); }, true, _wrap);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Auto-wire Index Slider", (s, e) => AutoWireSlider());
        }

        private void AutoWireSlider()
        {
            var doc = OnPingDocument();
            if (doc == null) return;
            var indexParam = Params.Input[1];

            // Only wire if nothing is connected
            if (indexParam.SourceCount == 0)
            {
                var slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
                slider.CreateAttributes();
                slider.Attributes.Pivot = new System.Drawing.PointF(Attributes.Pivot.X - 180, Attributes.Pivot.Y + 20);
                slider.Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Integer;
                slider.Slider.Minimum = 0;
                slider.Slider.Maximum = 10; // This will auto-adjust in the next solve instance
                slider.Slider.Value = 0;
                
                doc.AddObject(slider, false);
                indexParam.AddSource(slider);
                slider.ExpireSolution(true);
            }
        }

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetBoolean("WrapIndices", _wrap);
            return base.Write(writer);
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (reader.ItemExists("WrapIndices"))
                _wrap = reader.GetBoolean("WrapIndices");
            return base.Read(reader);
        }

        protected override System.Drawing.Bitmap Icon => null; // Fallback to default
        
        public override Guid ComponentGuid => new Guid("B5D1F6A8-A2C4-4395-8D9E-E2A4B1C9F3A1");
    }
}
