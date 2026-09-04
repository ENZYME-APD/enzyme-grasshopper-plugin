using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class GradientGenerator : GH_Component
    {
        public GradientGenerator()
          : base("Gradient Generator", "GradientGen",
              "Creates an interpolated color gradient based on a list of input colors and a number of steps.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddColourParameter("Colors", "C", "List of input colors to interpolate", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Steps", "N", "Number of output colors to generate", GH_ParamAccess.item, 10);
            
            pManager[0].Optional = true;
        }

        private bool hasSources = false;
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                var defaultColors = new Color[] {
                    Color.FromArgb(0, 50, 150),
                    Color.FromArgb(0, 180, 200),
                    Color.FromArgb(150, 220, 100),
                    Color.FromArgb(255, 200, 50),
                    Color.FromArgb(255, 50, 0)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 0, defaultColors, 121, -10);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 1, 2, 100, 10, 247, 58);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddColourParameter("Generated Colors", "C", "The interpolated list of colors", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Color> inColors = new List<Color>();
            if (!DA.GetDataList(0, inColors) || inColors.Count == 0)
            {
                // Fallback palette
                inColors = new List<Color> { Color.Blue, Color.Red };
            }

            int steps = 10;
            DA.GetData(1, ref steps);

            if (steps < 1) steps = 1;

            List<Color> outColors = new List<Color>();

            for (int i = 0; i < steps; i++)
            {
                double t = steps == 1 ? 0.0 : (double)i / (steps - 1);
                outColors.Add(GetInterpolatedColor(t, inColors));
            }

            DA.SetDataList(0, outColors);
            
            Message = $"Gradient Generator\n---\nInput Colors: {inColors.Count}\nSteps: {steps}";
        }

        private Color GetInterpolatedColor(double t, List<Color> palette)
        {
            if (palette.Count == 1) return palette[0];
            
            double scaledT = t * (palette.Count - 1);
            int index = (int)scaledT;
            
            if (index >= palette.Count - 1) return palette[palette.Count - 1];
            if (index < 0) return palette[0];
            
            double remainder = scaledT - index;
            
            Color c1 = palette[index];
            Color c2 = palette[index + 1];
            
            int r = (int)(c1.R + (c2.R - c1.R) * remainder);
            int g = (int)(c1.G + (c2.G - c1.G) * remainder);
            int b = (int)(c1.B + (c2.B - c1.B) * remainder);
            
            return Color.FromArgb(r, g, b);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Enzyme.IconLoader.Load("Gradient Generator.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("B5D2F6B2-82A1-4F9C-91B2-C3D4E5F6A7B8"); }
        }
    }
}
