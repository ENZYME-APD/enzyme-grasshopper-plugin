using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using GH_IO.Serialization;

namespace Enzyme.Components
{
    public class FalseStartToggle : GH_BooleanToggle
    {
        public FalseStartToggle() : base()
        {
            this.Name = "False Start Toggle";
            this.NickName = "False";
            this.Description = "A boolean toggle that always initializes as False when the Grasshopper document opens. Prevents heavy scripts from auto-running on open.";
            this.Category = "Enzyme";
            this.SubCategory = "Utilities";
            this.Value = false;
        }

        public override Guid ComponentGuid => new Guid("A3F192B4-8457-4C3A-9B21-E8D5F6A7B1C9");

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override bool Read(GH_IReader reader)
        {
            bool result = base.Read(reader);
            this.Value = false; // Always force to false on load
            return result;
        }
    }
}
