using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class OptimizedRemap : GH_Component
    {
        public OptimizedRemap()
          : base("Optimized Remap", "OptRemap",
              "Remaps a list of numbers to a new target domain without needing a Bounds component.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "Values", "List of numbers to remap", GH_ParamAccess.list);
            pManager.AddIntervalParameter("Target", "Target", "The target domain to remap the values into", GH_ParamAccess.item, new Interval(0.0, 1.0));
            
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Remapped", "Remapped", "The remapped numbers", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<double> values = new List<double>();
            if (!DA.GetDataList(0, values)) return;

            Interval target = new Interval(0.0, 1.0);
            DA.GetData(1, ref target);

            if (values.Count == 0) return;

            // Single pass to find bounds
            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (double v in values)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }

            List<double> remapped = new List<double>(values.Count);
            double range = max - min;
            double targetRange = target.T1 - target.T0;

            if (range == 0.0)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    remapped.Add(target.T0);
                }
            }
            else
            {
                for (int i = 0; i < values.Count; i++)
                {
                    double normalized = (values[i] - min) / range;
                    remapped.Add(target.T0 + normalized * targetRange);
                }
            }

            DA.SetDataList(0, remapped);
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("B5D1F6A8-D5B4-4ABC-8D9E-F12345678901");
    }
}
