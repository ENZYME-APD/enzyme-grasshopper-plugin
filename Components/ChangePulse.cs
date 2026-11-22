using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class ChangePulse : GH_Component
    {
        // Previous text form per solve iteration (so list inputs compare item i with item i of the previous solution).
        // A missing key means "never seen": the first value only initialises and does not pulse (e.g. on file open).
        private readonly System.Collections.Generic.Dictionary<int, string> _prevByIteration = new System.Collections.Generic.Dictionary<int, string>();
        private const string EmptyMarker = "\u0000<empty>";

        public ChangePulse()
          : base("Change Pulse", "Pulse",
              "Monitors an input value (like a Slider) and outputs a True pulse (imitating a button) whenever the data changes.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Data", "Data", "Data to monitor (e.g., a Number Slider).", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Pulse", "Pulse", "Outputs True for one solution when Data changes, then resets to False.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            int iteration = DA.Iteration;
            bool seen = _prevByIteration.TryGetValue(iteration, out string prevHash);

            IGH_Goo data = null;
            if (!DA.GetData(0, ref data) || data == null)
            {
                // If data stream is disconnected or empty
                _prevByIteration[iteration] = EmptyMarker;
                if (seen && prevHash != EmptyMarker)
                {
                    DA.SetData(0, true);
                    ScheduleReset();
                }
                else
                {
                    DA.SetData(0, false);
                }
                return;
            }

            string currentHash = data.ToString();
            _prevByIteration[iteration] = currentHash;

            if (seen && currentHash != prevHash)
            {
                DA.SetData(0, true);
                ScheduleReset();
            }
            else
            {
                DA.SetData(0, false);
            }
        
            stopwatch.Stop();
            Hud.Done(this, stopwatch.Elapsed.TotalMilliseconds);
        }

        private void ScheduleReset()
        {
            var doc = OnPingDocument();
            if (doc != null)
            {
                // Schedule a quick solution 5ms later to drop the pulse back to False
                doc.ScheduleSolution(5, d => {
                    this.ExpireSolution(false);
                });
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("ChangePulse.png");

        
        public override GH_Exposure Exposure => GH_Exposure.quinary;
public override Guid ComponentGuid => new Guid("B8C2D3A1-774A-4F11-8890-E5A721C8B493");
    }
}
