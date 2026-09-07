using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;

namespace Enzyme.Components
{
    public class CanvasStyle : GH_Component
    {
        public CanvasStyle()
          : base("Canvas Style", "CanvasStyle",
              "Modifies the global canvas background, grid, and panel default colors.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddColourParameter("Panel Default Color", "PanelColor", "Default color for panels", GH_ParamAccess.item, Color.FromArgb(255, 255, 250, 90));
            pManager.AddColourParameter("Canvas Background Color", "CanvasBg", "Background color for the canvas", GH_ParamAccess.item, Color.FromArgb(255, 212, 208, 200));
            pManager.AddColourParameter("Canvas Gridline Color", "GridColor", "Color for the canvas gridlines", GH_ParamAccess.item, Color.FromArgb(30, 0, 0, 0));
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Color panelColor = Color.Empty;
            Color canvasBg = Color.Empty;
            Color gridColor = Color.Empty;

            DA.GetData(0, ref panelColor);
            DA.GetData(1, ref canvasBg);
            DA.GetData(2, ref gridColor);

            GH_Skin.panel_back = panelColor;
            GH_Skin.canvas_back = canvasBg;
            GH_Skin.canvas_grid = gridColor;

            Grasshopper.Instances.ActiveCanvas?.Refresh();

            this.Message = "Love your style!";
        }

        public override Guid ComponentGuid => new Guid("B2C3D4E5-F6A1-4B2C-9D0E-F1A2B3C4D5E6");
    }
}
