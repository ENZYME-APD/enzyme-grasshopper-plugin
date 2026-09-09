using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class ChangePulse : GH_Component
    {
        private string _prevHash = null;

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
            IGH_Goo data = null;
            if (!DA.GetData(0, ref data)) 
            {
                // If data stream is disconnected or empty
                if (_prevHash != null)
                {
                    _prevHash = null;
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
            
            if (_prevHash == null || currentHash != _prevHash)
            {
                _prevHash = currentHash;
                DA.SetData(0, true);
                ScheduleReset();
            }
            else
            {
                DA.SetData(0, false);
            }
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

        protected override System.Drawing.Bitmap Icon => null; // Fallback to default icon

        public override Guid ComponentGuid => new Guid("B8C2D3A1-774A-4F11-8890-E5A721C8B493");
    }
}
