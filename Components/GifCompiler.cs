using System;
using System.IO;
using System.Linq;
using System.Drawing;
using Grasshopper.Kernel;
using AnimatedGif;

namespace Enzyme.Components
{
    public class GifCompiler : GH_Component
    {
        public GifCompiler()
          : base("GIF Compiler", "GIF",
              "Stitches a sequence of images into an animated GIF.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Directory", "Dir", "Directory containing the image sequence.", GH_ParamAccess.item);
            pManager.AddTextParameter("Prefix", "Prefix", "Prefix of the images to include (e.g. 'Turntable_').", GH_ParamAccess.item, "Turntable_");
            pManager.AddIntegerParameter("FPS", "FPS", "Frames per second.", GH_ParamAccess.item, 24);
            pManager.AddBooleanParameter("Run", "Run", "Trigger the GIF compilation.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("FilePath", "File", "Path to the compiled GIF.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string dir = "";
            if (!DA.GetData("Directory", ref dir)) return;

            string prefix = "";
            DA.GetData("Prefix", ref prefix);

            int fps = 24;
            DA.GetData("FPS", ref fps);

            bool run = false;
            if (!DA.GetData("Run", ref run) || !run)
                return;

            if (!Directory.Exists(dir))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Directory does not exist.");
                return;
            }

            var files = Directory.GetFiles(dir, $"{prefix}*.png")
                                 .OrderBy(f => f)
                                 .ToList();

            if (files.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No matching images found.");
                return;
            }

            int delayMs = 1000 / Math.Max(1, fps);
            string outPath = Path.Combine(dir, $"{prefix}Animation.gif");

            try
            {
                using (var gif = AnimatedGif.AnimatedGif.Create(outPath, delayMs, 0))
                {
                    foreach (var file in files)
                    {
                        using (var img = Image.FromFile(file))
                        {
                            gif.AddFrame(img, delayMs, GifQuality.Bit8);
                        }
                    }
                }
                DA.SetData(0, outPath);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        
            stopwatch.Stop();
            Message = $"{this.NickName}\n{stopwatch.Elapsed.TotalMilliseconds:F2} ms\n---\nDone";
        }
        
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("GifCompiler.png");
        public override Guid ComponentGuid => new Guid("0F6CAAE5-F729-4560-A656-9F7CBAF73F53");
    }
}
