using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Enzyme.Utils;

namespace Enzyme.Components
{
    /// <summary>Named, perceptually even palettes (incl. colour-blind safe ones) for every analysis component.</summary>
    public class ColorPalette : EnzymeComponent
    {
        public ColorPalette()
          : base("Color Palette", "Palette",
              "Ready-made analysis gradients: perceptual (Viridis, Cividis - colour-blind safe), Turbo, Ladybug, thermal, wind and diverging sets for differences. Feed the colours to any Colors / CustomColors input.",
              "Enzyme", "Site Analysis")
        { }

        public override Guid ComponentGuid => new Guid("D7F3A9B2-5C81-4E6D-A0F4-3B92E1C8D605");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => IconLoader.Load("color_palette.png");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "Name", "Palette name (see Names output)", GH_ParamAccess.item, "Viridis");
            pManager.AddIntegerParameter("Count", "Count", "Number of colours to return (0 = the palette's own stops)", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Reverse", "Reverse", "Flip the gradient", GH_ParamAccess.item, false);
            pManager[1].Optional = true; pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddColourParameter("Colors", "Colors", "Gradient colours, low to high", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "Names", "All available palette names (diverging ones marked)", GH_ParamAccess.list);
        }

        protected override void AutoWire(GH_Document doc)
        {
            var names = Palettes.Names.ToArray();
            AutoWireHelper.WireValueList(this, doc, 0, names, names.Select(n => "\"" + n + "\"").ToArray(), 200, 0);
        }

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            string name = "Viridis"; DA.GetData(0, ref name);
            int count = 0; DA.GetData(1, ref count);
            bool rev = false; DA.GetData(2, ref rev);
            Color[] stops;
            if (!Palettes.TryGet(name, out stops))
                throw new ArgumentException("Unknown palette '" + name + "'. See the Names output.");
            if (rev) Array.Reverse(stops);
            Color[] outCols = count >= 2 ? Palettes.Resample(stops, Math.Min(count, 1024)) : stops;
            DA.SetDataList(0, outCols);
            DA.SetDataList(1, Palettes.All.Select(pd => pd.Diverging ? pd.Name + " (diverging)" : pd.Name));
            Hud.Done(this, sw, Hud.Metric("Palette", name), Hud.Metric("Colours", outCols.Length));
        }
    }
}
