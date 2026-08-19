using System;
using System.Drawing;
using System.Diagnostics;
using Grasshopper.Kernel;
using Enzyme; // for IconLoader

namespace Enzyme.Components
{
    public class LegendGeometry : GH_Component
    {
        public LegendGeometry()
            : base("Legend Geometry", "Legend",
                "Creates a legend of colors with geometric representation",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                Bitmap icon = IconLoader.Load("legend_icon.png");
                if (icon == null)
                {
                    this.Message = "Icon missing";
                }
                return icon;
            }
        }

        public override Guid ComponentGuid => new Guid("C3D9F4E6-B8A2-4C7D-A0F3-D6E5B7C8A9F0");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ix = 200, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 1.0, ix, -120);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Color Legend", "Color Legend", "Legend data from analysis components", GH_ParamAccess.item);
            pManager.AddPointParameter("Base Point", "Base Point", "Base point for legend placement", GH_ParamAccess.item, new Rhino.Geometry.Point3d(0, 0, 0));
            pManager.AddNumberParameter("Scale", "Scale", "Scale factor for legend geometry", GH_ParamAccess.item, 1.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Legend Rectangles", "Rectangles", "Rectangle curves for legend", GH_ParamAccess.list);
            pManager.AddTextParameter("Legend Labels", "Labels", "Text labels for legend", GH_ParamAccess.list);
            pManager.AddPointParameter("Label Positions", "Label Positions", "Positions for text labels", GH_ParamAccess.list);
            pManager.AddColourParameter("Legend Colors", "Legend Colors", "Colors for legend elements", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Input variables
            object legendData = null;
            Rhino.Geometry.Point3d basePoint = new Rhino.Geometry.Point3d(0, 0, 0);
            double scale = 1.0;

            // Get input data
            if (!DA.GetData(0, ref legendData)) return;
            if (!DA.GetData(1, ref basePoint)) return;
            if (!DA.GetData(2, ref scale)) return;

            // Validate input
            if (legendData == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid legend data input");
                return;
            }

            // Process the legend data and create geometry
            var result = CreateLegendGeometry(legendData, basePoint, scale);

            // Set output data
            DA.SetDataList(0, result.Rectangles);
            DA.SetDataList(1, result.Labels);
            DA.SetDataList(2, result.LabelPositions);
            DA.SetDataList(3, result.Colors);
        }

        private LegendResult CreateLegendGeometry(object legendData, Rhino.Geometry.Point3d basePoint, double scale)
        {
            // Initialize result containers
            var rectangles = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            var labels = new System.Collections.Generic.List<string>();
            var labelPositions = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
            var colors = new System.Collections.Generic.List<Color>();

            // Default dimensions
            double rectWidth = 1.0 * scale;
            double rectHeight = 0.5 * scale;
            double spacing = 0.2 * scale;
            double textOffset = 0.3 * scale;

            // Determine the type of legend data and process accordingly
            // This is a simplified implementation that handles the two types of legend data we created

            // Check if it's a slope analysis legend
            if (legendData.GetType().GetProperty("Threshold") != null)
            {
                // Extract properties using reflection (simplified for this example)
                var threshold = Convert.ToDouble(legendData.GetType().GetProperty("Threshold").GetValue(legendData));
                var percentOverThreshold = Convert.ToDouble(legendData.GetType().GetProperty("PercentOverThreshold").GetValue(legendData));
                var startColor = (Color)legendData.GetType().GetProperty("StartColor").GetValue(legendData);
                var endColor = (Color)legendData.GetType().GetProperty("EndColor").GetValue(legendData);
                var title = legendData.GetType().GetProperty("Title").GetValue(legendData).ToString();

                // Create title
                labels.Add(title);
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y + rectHeight + textOffset, basePoint.Z));

                // Create gradient rectangles
                int segments = 10;
                for (int i = 0; i < segments; i++)
                {
                    double t = i / (double)(segments - 1);
                    double x = basePoint.X + i * (rectWidth / segments);

                    // Create rectangle
                    var rect = new Rhino.Geometry.Rectangle3d(
                        Rhino.Geometry.Plane.WorldXY,
                        new Rhino.Geometry.Point3d(x, basePoint.Y, basePoint.Z),
                        new Rhino.Geometry.Point3d(x + rectWidth / segments, basePoint.Y + rectHeight, basePoint.Z)
                    );
                    rectangles.Add(rect.ToNurbsCurve());

                    // Interpolate color
                    Color color = InterpolateColor(startColor, endColor, t);
                    colors.Add(color);
                }

                // Add min/max labels
                labels.Add("0°");
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y - textOffset, basePoint.Z));

                labels.Add($"{threshold:F1}°+");
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X + rectWidth, basePoint.Y - textOffset, basePoint.Z));

                // Add percentage label
                labels.Add($"{percentOverThreshold:F1}% over threshold");
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X + rectWidth / 2, basePoint.Y - textOffset * 2, basePoint.Z));
            }
            // Check if it's a height map legend
            else if (legendData.GetType().GetProperty("MinHeight") != null)
            {
                // Extract properties using reflection (simplified for this example)
                var minHeight = Convert.ToDouble(legendData.GetType().GetProperty("MinHeight").GetValue(legendData));
                var maxHeight = Convert.ToDouble(legendData.GetType().GetProperty("MaxHeight").GetValue(legendData));
                var colorsList = (System.Collections.Generic.List<Color>)legendData.GetType().GetProperty("Colors").GetValue(legendData);
                var title = legendData.GetType().GetProperty("Title").GetValue(legendData).ToString();

                // Create title
                labels.Add(title);
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y + rectHeight + textOffset, basePoint.Z));

                // Create color blocks
                int colorCount = colorsList.Count;
                for (int i = 0; i < colorCount; i++)
                {
                    double segmentWidth = rectWidth / colorCount;
                    double x = basePoint.X + i * segmentWidth;

                    // Create rectangle
                    var rect = new Rhino.Geometry.Rectangle3d(
                        Rhino.Geometry.Plane.WorldXY,
                        new Rhino.Geometry.Point3d(x, basePoint.Y, basePoint.Z),
                        new Rhino.Geometry.Point3d(x + segmentWidth, basePoint.Y + rectHeight, basePoint.Z)
                    );
                    rectangles.Add(rect.ToNurbsCurve());

                    // Add color
                    colors.Add(colorsList[i]);
                }

                // Add min/max height labels
                labels.Add($"{minHeight:F2}");
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y - textOffset, basePoint.Z));

                labels.Add($"{maxHeight:F2}");
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X + rectWidth, basePoint.Y - textOffset, basePoint.Z));
            }
            else
            {
                // Generic fallback for unknown legend data
                // Create a simple rectangle
                var rect = new Rhino.Geometry.Rectangle3d(
                    Rhino.Geometry.Plane.WorldXY,
                    new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y, basePoint.Z),
                    new Rhino.Geometry.Point3d(basePoint.X + rectWidth, basePoint.Y + rectHeight, basePoint.Z)
                );
                rectangles.Add(rect.ToNurbsCurve());
                colors.Add(Color.Gray);

                // Add a generic label
                labels.Add("Legend");
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X + rectWidth / 2, basePoint.Y - textOffset, basePoint.Z));
            }

            return new LegendResult
            {
                Rectangles = rectangles,
                Labels = labels,
                LabelPositions = labelPositions,
                Colors = colors
            };
        }

        private Color InterpolateColor(Color color1, Color color2, double t)
        {
            int r = (int)(color1.R * (1 - t) + color2.R * t);
            int g = (int)(color1.G * (1 - t) + color2.G * t);
            int b = (int)(color1.B * (1 - t) + color2.B * t);
            return Color.FromArgb(r, g, b);
        }

        private class LegendResult
        {
            public System.Collections.Generic.List<Rhino.Geometry.Curve> Rectangles { get; set; } = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            public System.Collections.Generic.List<string> Labels { get; set; } = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<Rhino.Geometry.Point3d> LabelPositions { get; set; } = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
            public System.Collections.Generic.List<Color> Colors { get; set; } = new System.Collections.Generic.List<Color>();
        }
    }
}
