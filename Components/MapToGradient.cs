using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class MapToGradient : GH_Component
    {
        public MapToGradient()
          : base("Map to Gradient", "GradientMap",
              "Remaps a list of numbers into a gradient of colors.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "Values", "List of numbers to map to colors.", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Source Domain", "Source Domain", "Optional source domain. If left empty, it automatically uses the min and max of the input values.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "Colors", "List of colors to form the gradient. Defaults to a standard Blue-to-Red spectrum if empty.", GH_ParamAccess.list);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddColourParameter("Mapped Colors", "Mapped Colors", "The resulting interpolated colors.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<double> values = new List<double>();
            if (!DA.GetDataList(0, values)) return;
            if (values.Count == 0) return;

            Interval domain = Interval.Unset;
            bool hasDomain = DA.GetData(1, ref domain);

            if (!hasDomain || !domain.IsValid)
            {
                double min = double.MaxValue;
                double max = double.MinValue;
                foreach (double v in values)
                {
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                domain = new Interval(min, max);
            }

            // Protect against zero-length domains
            if (domain.Length == 0)
            {
                domain = new Interval(domain.Min, domain.Min + 1e-9);
            }

            List<Color> customColors = new List<Color>();
            DA.GetDataList(2, customColors);

            if (customColors.Count == 0)
            {
                // Default Blue -> Cyan -> Green -> Yellow -> Red
                customColors.Add(Color.Blue);
                customColors.Add(Color.Cyan);
                customColors.Add(Color.Lime);
                customColors.Add(Color.Yellow);
                customColors.Add(Color.Red);
            }
            else if (customColors.Count == 1)
            {
                customColors.Add(customColors[0]);
            }

            List<Color> mappedColors = new List<Color>(values.Count);

            foreach (double val in values)
            {
                double t = (val - domain.Min) / domain.Length;
                if (t < 0.0) t = 0.0;
                if (t > 1.0) t = 1.0;

                double scaled = t * (customColors.Count - 1);
                int idx = (int)scaled;
                if (idx >= customColors.Count - 1) idx = customColors.Count - 2;
                if (idx < 0) idx = 0; // safety

                double blend = scaled - idx;
                Color c1 = customColors[idx];
                Color c2 = customColors[idx + 1];

                int r = (int)(c1.R + (c2.R - c1.R) * blend);
                int g = (int)(c1.G + (c2.G - c1.G) * blend);
                int b = (int)(c1.B + (c2.B - c1.B) * blend);

                mappedColors.Add(Color.FromArgb(255, r, g, b));
            }

            DA.SetDataList(0, mappedColors);
        }

        protected override System.Drawing.Bitmap Icon => null; // Uses default fallback
        
        public override Guid ComponentGuid => new Guid("D345F12A-99B7-4C10-A1E2-F4D3C5B1709A");
    }
}
