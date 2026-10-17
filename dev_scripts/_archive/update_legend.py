import re

with open('Components/LegendGeometry.cs', 'r') as f:
    content = f.read()

# Make sure we have Newtonsoft.Json.Linq
if 'using Newtonsoft.Json.Linq;' not in content:
    content = 'using Newtonsoft.Json.Linq;\n' + content

# We need to replace the CreateLegendGeometry method
# It currently starts with private LegendResult CreateLegendGeometry(object legendData, Rhino.Geometry.Point3d basePoint, double scale)

new_method = """        private LegendResult CreateLegendGeometry(object legendData, Rhino.Geometry.Point3d basePoint, double scale)
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
"""

content = re.sub(r'private LegendResult CreateLegendGeometry\(object legendData, Rhino\.Geometry\.Point3d basePoint, double scale\).*?return new LegendResult[^}]*};[^}]*}', new_method, content, flags=re.DOTALL)

with open('Components/LegendGeometry.cs', 'w') as f:
    f.write(content)

