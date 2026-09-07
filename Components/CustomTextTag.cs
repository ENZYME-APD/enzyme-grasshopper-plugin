using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Rhino.Display;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class CustomTextTag : GH_Component
    {
        private int _moveAxis = 2; // 0=X, 1=Y, 2=Z

        private class TagData
        {
            public Text3d Text;
            public Color Color;
        }

        private List<TagData> _tags = new List<TagData>();

        public CustomTextTag()
          : base("Custom Text Tag", "CustomTag",
              "Displays a 3D text tag with a custom move vector. Right-click to change the move axis.",
              "Enzyme", "Display")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPlaneParameter("Plane", "P", "Base plane for text location", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddTextParameter("Text", "T", "Text string to display", GH_ParamAccess.item, "Enzyme");
            pManager.AddNumberParameter("Size", "S", "Size of the text", GH_ParamAccess.item, 1.0);
            pManager.AddColourParameter("Colour", "C", "Colour of the text", GH_ParamAccess.item, Color.Black);
            pManager.AddIntegerParameter("Justification", "J", "Text justification (0=BL, 1=BC, 2=BR, 3=ML, 4=MC, 5=MR, 6=TL, 7=TC, 8=TR)", GH_ParamAccess.item, 4);
            pManager.AddNumberParameter("Move", "M", "Distance to move the text along the selected axis", GH_ParamAccess.item, 0.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Display component, no outputs needed typically.
        }

        private System.Diagnostics.Stopwatch _stopwatch;

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            _tags.Clear();
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
            _stopwatch?.Stop();
            long ms = _stopwatch != null ? _stopwatch.ElapsedMilliseconds : 0;
            string axisStr = _moveAxis == 0 ? "X" : _moveAxis == 1 ? "Y" : "Z";
            this.Message = $"CUSTOM TEXT TAG\nTime: {ms} ms\n---\nMove Axis: {axisStr}\nTags Generated: {_tags.Count}";
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Plane plane = Plane.WorldXY;
            string text = "";
            double size = 1.0;
            Color color = Color.Black;
            int just = 4;
            double move = 0.0;

            if (!DA.GetData(0, ref plane)) return;
            if (!DA.GetData(1, ref text)) return;
            if (!DA.GetData(2, ref size)) return;
            if (!DA.GetData(3, ref color)) return;
            if (!DA.GetData(4, ref just)) return;
            if (!DA.GetData(5, ref move)) return;

            Vector3d moveVec = Vector3d.Zero;
            if (_moveAxis == 0) moveVec = plane.XAxis * move;
            else if (_moveAxis == 1) moveVec = plane.YAxis * move;
            else moveVec = plane.ZAxis * move;

            plane.Origin += moveVec;

            Text3d tag = new Text3d(text, plane, size);

            switch (just)
            {
                case 0: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Left; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Bottom; break;
                case 1: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Bottom; break;
                case 2: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Right; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Bottom; break;
                case 3: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Left; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Middle; break;
                case 4: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Middle; break;
                case 5: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Right; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Middle; break;
                case 6: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Left; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top; break;
                case 7: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Center; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top; break;
                case 8: tag.HorizontalAlignment = Rhino.DocObjects.TextHorizontalAlignment.Right; tag.VerticalAlignment = Rhino.DocObjects.TextVerticalAlignment.Top; break;
            }

            _tags.Add(new TagData { Text = tag, Color = color });
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            base.DrawViewportWires(args);
            if (this.Hidden || this.Locked) return;

            foreach (var tag in _tags)
            {
                args.Display.Draw3dText(tag.Text, tag.Color);
            }
        }

        public override BoundingBox ClippingBox
        {
            get
            {
                BoundingBox box = BoundingBox.Empty;
                foreach (var t in _tags)
                {
                    box.Union(t.Text.BoundingBox);
                }
                return box;
            }
        }

        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Move along X", (s, e) => { _moveAxis = 0; ExpireSolution(true); }, true, _moveAxis == 0);
            Menu_AppendItem(menu, "Move along Y", (s, e) => { _moveAxis = 1; ExpireSolution(true); }, true, _moveAxis == 1);
            Menu_AppendItem(menu, "Move along Z", (s, e) => { _moveAxis = 2; ExpireSolution(true); }, true, _moveAxis == 2);
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("MoveAxis", _moveAxis);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("MoveAxis"))
                _moveAxis = reader.GetInt32("MoveAxis");
            return base.Read(reader);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            if (this.Params.Input[1].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 1, "Enzyme", 100, -20, 80, 25);
            if (this.Params.Input[2].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 2, "1.0", 100, 0, 50, 25);
            if (this.Params.Input[3].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 3, Color.Black, 100, 20);
            
            // Justification ValueList
            if (this.Params.Input[4].SourceCount == 0)
            {
                var vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 40);
                vl.ListMode = Grasshopper.Kernel.Special.GH_ValueListMode.DropDown;
                vl.ListItems.Clear();
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Bottom-Left", "0"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Bottom-Center", "1"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Bottom-Right", "2"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Middle-Left", "3"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Middle-Center", "4"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Middle-Right", "5"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Top-Left", "6"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Top-Center", "7"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Top-Right", "8"));
                document.AddObject(vl, false);
                this.Params.Input[4].AddSource(vl);
            }

            if (this.Params.Input[5].SourceCount == 0)
            {
                // Let's wire a slider for move
                var slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
                slider.CreateAttributes();
                slider.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 250, this.Attributes.Pivot.Y + 80);
                slider.Slider.Minimum = -10.0m;
                slider.Slider.Maximum = 10.0m;
                slider.Slider.Value = 0.0m;
                document.AddObject(slider, false);
                this.Params.Input[5].AddSource(slider);
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("custom tex tag.png");

        public override Guid ComponentGuid => new Guid("D8B6F9A2-4E1C-458B-8D7F-E2A4B1C9F3D5");
    }
}
