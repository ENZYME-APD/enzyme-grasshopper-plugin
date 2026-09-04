using Newtonsoft.Json.Linq;
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

        
        private System.Collections.Generic.List<Rhino.Geometry.Mesh> m_displayMeshes = new System.Collections.Generic.List<Rhino.Geometry.Mesh>();
        private System.Collections.Generic.List<System.Drawing.Color> m_displayColors = new System.Collections.Generic.List<System.Drawing.Color>();
        private System.Collections.Generic.List<string> m_displayTexts = new System.Collections.Generic.List<string>();
        private System.Collections.Generic.List<Rhino.Geometry.Point3d> m_displayPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
        private Rhino.Geometry.BoundingBox m_displayBox = Rhino.Geometry.BoundingBox.Empty;
        private double m_lastScale = 1.0;

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            m_displayMeshes.Clear();
            m_displayColors.Clear();
            m_displayTexts.Clear();
            m_displayPoints.Clear();
            m_displayBox = Rhino.Geometry.BoundingBox.Empty;
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            base.DrawViewportMeshes(args);
            if (this.Hidden || this.Locked) return;

            for (int i = 0; i < m_displayMeshes.Count; i++)
            {
                var mat = new Rhino.Display.DisplayMaterial(m_displayColors[i]);
                args.Display.DrawMeshShaded(m_displayMeshes[i], mat);
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            base.DrawViewportWires(args);
            if (this.Hidden || this.Locked) return;
            
            foreach (var mesh in m_displayMeshes)
            {
                args.Display.DrawMeshWires(mesh, System.Drawing.Color.Black, 1);
            }

            double textHeight = 0.2 * m_lastScale;
            for (int i = 0; i < m_displayTexts.Count; i++)
            {
                Rhino.Geometry.Plane pln = new Rhino.Geometry.Plane(m_displayPoints[i], Rhino.Geometry.Vector3d.ZAxis);
                args.Display.Draw3dText(m_displayTexts[i], System.Drawing.Color.Black, pln, textHeight, "Arial");
            }
        }

        public override Rhino.Geometry.BoundingBox ClippingBox
        {
            get
            {
                var box = base.ClippingBox;
                box.Union(m_displayBox);
                return box;
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 1.0, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 220, -45);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, -11, 180, 22);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "point", 220, 45);
            }
        }

        
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

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
                    pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
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
            
            m_lastScale = scale;
            for (int i = 0; i < result.Rectangles.Count; i++)
            {
                if (result.Rectangles[i].TryGetPolyline(out Rhino.Geometry.Polyline pl) && pl.Count >= 4)
                {
                    var mesh = new Rhino.Geometry.Mesh();
                    mesh.Vertices.Add(pl[0]);
                    mesh.Vertices.Add(pl[1]);
                    mesh.Vertices.Add(pl[2]);
                    mesh.Vertices.Add(pl[3]);
                    mesh.Faces.AddFace(0, 1, 2, 3);
                    mesh.Normals.ComputeNormals();
                    mesh.VertexColors.CreateMonotoneMesh(result.Colors[i]);
                    m_displayMeshes.Add(mesh);
                    m_displayBox.Union(mesh.GetBoundingBox(false));
                }
            }
            m_displayColors.AddRange(result.Colors);

            for (int i = 0; i < result.Labels.Count; i++)
            {
                m_displayTexts.Add(result.Labels[i]);
                m_displayPoints.Add(result.LabelPositions[i]);
                m_displayBox.Union(result.LabelPositions[i]);
            }

            DA.SetDataList(0, result.Rectangles);
            DA.SetDataList(1, result.Labels);
            DA.SetDataList(2, result.LabelPositions);
            DA.SetDataList(3, result.Colors);
                    DA.SetData(4, "LEGEND GEOMETRY\n" + "\n" + "HOW IT WORKS:\n" + "Reads the domains and color gradients from the analysis components (Slope, Height, Flow) and bakes a scaled 3D legend into the Rhino scene.\n\n" + "INTERPRETATION & IMPORTANCE:\n" + "Ensures that visual diagrams are scientifically readable. Without a legend, a heatmap is just pretty colors with it, it becomes an actionable data map.");
        }

                private LegendResult CreateLegendGeometry(object legendData, Rhino.Geometry.Point3d basePoint, double scale)
        {
            var rectangles = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            var labels = new System.Collections.Generic.List<string>();
            var labelPositions = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
            var colors = new System.Collections.Generic.List<Color>();

            double rectWidth = 1.0 * scale;
            double rectHeight = 0.5 * scale;
            double textOffset = 0.3 * scale;

            try
            {
                JObject parsed = null;
                
                // Backwards compatibility with the old anonymous objects just in case
                if (legendData.GetType().GetProperty("Threshold") != null)
                {
                    parsed = new JObject();
                    parsed["Type"] = "Gradient";
                    parsed["Title"] = legendData.GetType().GetProperty("Title").GetValue(legendData).ToString();
                    var startColor = (Color)legendData.GetType().GetProperty("StartColor").GetValue(legendData);
                    var endColor = (Color)legendData.GetType().GetProperty("EndColor").GetValue(legendData);
                    parsed["Colors"] = new JArray(
                        new JObject { ["R"] = startColor.R, ["G"] = startColor.G, ["B"] = startColor.B },
                        new JObject { ["R"] = endColor.R, ["G"] = endColor.G, ["B"] = endColor.B }
                    );
                    var thresh = Convert.ToDouble(legendData.GetType().GetProperty("Threshold").GetValue(legendData));
                    var pct = Convert.ToDouble(legendData.GetType().GetProperty("PercentOverThreshold").GetValue(legendData));
                    parsed["Labels"] = new JArray("0°", $"{thresh:F1}°+");
                    parsed["SubLabels"] = new JArray($"{pct:F1}% over threshold");
                }
                else if (legendData.GetType().GetProperty("MinHeight") != null)
                {
                    parsed = new JObject();
                    parsed["Type"] = "Blocks";
                    parsed["Title"] = legendData.GetType().GetProperty("Title").GetValue(legendData).ToString();
                    var cList = (System.Collections.Generic.List<Color>)legendData.GetType().GetProperty("Colors").GetValue(legendData);
                    var jcolors = new JArray();
                    foreach (var c in cList) jcolors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                    parsed["Colors"] = jcolors;
                    var minH = Convert.ToDouble(legendData.GetType().GetProperty("MinHeight").GetValue(legendData));
                    var maxH = Convert.ToDouble(legendData.GetType().GetProperty("MaxHeight").GetValue(legendData));
                    parsed["Labels"] = new JArray($"{minH:F1}m", $"{maxH:F1}m");
                }
                else
                {
                    // Parse the new standardized JSON string format
                    string jsonString = legendData.ToString();
                    parsed = JObject.Parse(jsonString);
                }

                if (parsed == null) return new LegendResult { Rectangles = rectangles, Labels = labels, LabelPositions = labelPositions, Colors = colors };

                string legendType = parsed["Type"]?.ToString();
                string title = parsed["Title"]?.ToString() ?? "Legend";
                
                labels.Add(title);
                labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y + rectHeight + textOffset, basePoint.Z));

                var cArray = parsed["Colors"] as JArray;
                var lArray = parsed["Labels"] as JArray;

                if (legendType == "Gradient" && cArray != null && cArray.Count >= 2)
                {
                    Color startColor = Color.FromArgb((int)cArray[0]["R"], (int)cArray[0]["G"], (int)cArray[0]["B"]);
                    Color endColor = Color.FromArgb((int)cArray[1]["R"], (int)cArray[1]["G"], (int)cArray[1]["B"]);

                    int segments = 10;
                    for (int i = 0; i < segments; i++)
                    {
                        double t = i / (double)(segments - 1);
                        double x = basePoint.X + i * (rectWidth / segments);
                        var rect = new Rhino.Geometry.Rectangle3d(Rhino.Geometry.Plane.WorldXY,
                            new Rhino.Geometry.Point3d(x, basePoint.Y, basePoint.Z),
                            new Rhino.Geometry.Point3d(x + rectWidth / segments, basePoint.Y + rectHeight, basePoint.Z));
                        rectangles.Add(rect.ToNurbsCurve());
                        colors.Add(InterpolateColor(startColor, endColor, t));
                    }

                    if (lArray != null && lArray.Count >= 2)
                    {
                        labels.Add(lArray[0].ToString());
                        labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y - textOffset, basePoint.Z));
                        labels.Add(lArray[1].ToString());
                        labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X + rectWidth, basePoint.Y - textOffset, basePoint.Z));
                    }

                    var subArray = parsed["SubLabels"] as JArray;
                    if (subArray != null && subArray.Count > 0)
                    {
                        labels.Add(subArray[0].ToString());
                        labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X + rectWidth / 2, basePoint.Y - textOffset * 2, basePoint.Z));
                    }
                }
                else if (legendType == "Blocks" && cArray != null)
                {
                    int colorCount = cArray.Count;
                    for (int i = 0; i < colorCount; i++)
                    {
                        double segmentWidth = rectWidth / colorCount;
                        double x = basePoint.X + i * segmentWidth;
                        var rect = new Rhino.Geometry.Rectangle3d(Rhino.Geometry.Plane.WorldXY,
                            new Rhino.Geometry.Point3d(x, basePoint.Y, basePoint.Z),
                            new Rhino.Geometry.Point3d(x + segmentWidth, basePoint.Y + rectHeight, basePoint.Z));
                        rectangles.Add(rect.ToNurbsCurve());
                        colors.Add(Color.FromArgb((int)cArray[i]["R"], (int)cArray[i]["G"], (int)cArray[i]["B"]));
                    }

                    if (lArray != null && lArray.Count >= 2)
                    {
                        labels.Add(lArray[0].ToString());
                        labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X, basePoint.Y - textOffset, basePoint.Z));
                        labels.Add(lArray[lArray.Count - 1].ToString());
                        labelPositions.Add(new Rhino.Geometry.Point3d(basePoint.X + rectWidth, basePoint.Y - textOffset, basePoint.Z));
                    }
                }
            }
            catch (Exception ex)
            {
                labels.Add("Error parsing JSON legend");
                labelPositions.Add(basePoint);
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
