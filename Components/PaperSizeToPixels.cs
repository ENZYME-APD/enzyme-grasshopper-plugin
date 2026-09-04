using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class PaperSizeToPixels : GH_Component
    {
        public PaperSizeToPixels()
          : base("Document Size to Pixels", "Doc2Px",
              "Converts standard document sizes (A4, A3, 16:9, etc.) into pixel dimensions for use with Export Named Views.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Format", "F", "Paper format (e.g., A4, A3, 16:9 FHD).", GH_ParamAccess.item, "A4");
            pManager.AddBooleanParameter("Landscape", "L", "True for Landscape, False for Portrait. (Applies to A-series paper).", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("DPI", "DPI", "Print DPI to use for the calculation (default 300).", GH_ParamAccess.item, 300);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Width", "W", "Width in pixels.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Height", "H", "Height in pixels.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("DPI", "DPI", "Pass-through DPI to wire into Export Views.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string format = "A4";
            if (!DA.GetData(0, ref format)) return;

            bool landscape = true;
            if (!DA.GetData(1, ref landscape)) return;

            int dpi = 300;
            if (!DA.GetData(2, ref dpi)) return;

            double widthMM = 297.0;
            double heightMM = 210.0;
            bool isPhysical = true;

            int exactWidth = 0;
            int exactHeight = 0;

            string f = format.Trim().ToUpper();

            if (f == "A0") { widthMM = 841; heightMM = 1189; }
            else if (f == "A1") { widthMM = 594; heightMM = 841; }
            else if (f == "A2") { widthMM = 420; heightMM = 594; }
            else if (f == "A3") { widthMM = 297; heightMM = 420; }
            else if (f == "A4") { widthMM = 210; heightMM = 297; }
            else if (f == "A5") { widthMM = 148; heightMM = 210; }
            else if (f == "16:9 FHD" || f == "FHD") { exactWidth = 1920; exactHeight = 1080; isPhysical = false; }
            else if (f == "16:9 QHD" || f == "QHD" || f == "2K") { exactWidth = 2560; exactHeight = 1440; isPhysical = false; }
            else if (f == "16:9 4K" || f == "4K") { exactWidth = 3840; exactHeight = 2160; isPhysical = false; }
            else if (f == "1:1 INSTAGRAM") { exactWidth = 1080; exactHeight = 1080; isPhysical = false; }
            else { 
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Unknown format '{format}'. Defaulting to A4.");
                widthMM = 210; heightMM = 297; 
            }

            if (isPhysical)
            {
                // Ensure correct orientation
                double shortSide = Math.Min(widthMM, heightMM);
                double longSide = Math.Max(widthMM, heightMM);

                if (landscape)
                {
                    widthMM = longSide;
                    heightMM = shortSide;
                }
                else
                {
                    widthMM = shortSide;
                    heightMM = longSide;
                }

                exactWidth = (int)Math.Round((widthMM / 25.4) * dpi);
                exactHeight = (int)Math.Round((heightMM / 25.4) * dpi);
            }
            else
            {
                // For exact pixels, if user forces portrait, flip them.
                if (!landscape && exactWidth > exactHeight)
                {
                    int temp = exactWidth;
                    exactWidth = exactHeight;
                    exactHeight = temp;
                }
            }

            DA.SetData(0, exactWidth);
            DA.SetData(1, exactHeight);
            DA.SetData(2, dpi);
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendItem(menu, "Auto-create Format List", Menu_AutoCreateFormatList_Clicked);
        }

        private void Menu_AutoCreateFormatList_Clicked(object sender, EventArgs e)
        {
            Grasshopper.Kernel.Special.GH_ValueList vl = new Grasshopper.Kernel.Special.GH_ValueList();
            vl.CreateAttributes();
            vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y - 20);
            vl.ListItems.Clear();

            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A0", "\"A0\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A1", "\"A1\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A2", "\"A2\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A3", "\"A3\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A4", "\"A4\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A5", "\"A5\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 FHD", "\"16:9 FHD\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 QHD", "\"16:9 QHD\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 4K", "\"16:9 4K\""));
            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("1:1 Instagram", "\"1:1 INSTAGRAM\""));

            OnPingDocument().AddObject(vl, false);
            this.Params.Input[0].AddSource(vl);
            vl.ExpireSolution(true);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 1, true, 210, 0);

                var vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 180, this.Attributes.Pivot.Y - 20);
                vl.ListItems.Clear();
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A0", "\"A0\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A1", "\"A1\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A2", "\"A2\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A3", "\"A3\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A4", "\"A4\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("A5", "\"A5\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 FHD", "\"16:9 FHD\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 QHD", "\"16:9 QHD\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("16:9 4K", "\"16:9 4K\""));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("1:1 Instagram", "\"1:1 INSTAGRAM\""));
                vl.SelectItem(4); // Select A4

                document.AddObject(vl, false);
                this.Params.Input[0].AddSource(vl);
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return Enzyme.IconLoader.Load("PaperSizeToPixels.png"); }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("C92461D2-835A-4B6A-949B-5A3841D6B630"); }
        }
    }
}
