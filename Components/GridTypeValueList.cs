using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace Enzyme.Components
{
    public class GridTypeValueList : GH_ValueList
    {
        public GridTypeValueList() : base()
        {
            this.Category = "Enzyme";
            this.SubCategory = "Facade";
            this.Name = "Grid Types";
            this.NickName = "Grid Types";
            this.Description = "Pre-configured grid options: Rectangular, Offset, Hexagonal, Triangular.";
            
            this.ListItems.Clear();
            this.ListItems.Add(new GH_ValueListItem("Rectangular", "\"rectangular\""));
            this.ListItems.Add(new GH_ValueListItem("Offset Rectangular", "\"offset_rectangular\""));
            this.ListItems.Add(new GH_ValueListItem("Hexagonal", "\"hexagonal\""));
            this.ListItems.Add(new GH_ValueListItem("Triangular", "\"triangular\""));
        }

        public override Guid ComponentGuid => new Guid("A4B56C78-1D2E-4F3B-A9B1-2C3D4E5F6A7B");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
    }
}
