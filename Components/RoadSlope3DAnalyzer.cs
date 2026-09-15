using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Newtonsoft.Json.Linq;

namespace Enzyme.Components
{
    public class RoadSlope3DAnalyzer : GH_Component
    {
        public RoadSlope3DAnalyzer()
            : base("3D Road Slope Analysis", "RoadSlope3D",
                "Analyzes road slopes directly from 3D centerline curves without needing a terrain mesh.",
                "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "Curves", "3D curves representing road centerlines", GH_ParamAccess.list);
            pManager.AddNumberParameter("Threshold", "Threshold", "Slope threshold", GH_ParamAccess.item, 8.0);
            pManager.AddNumberParameter("Segment Size", "Segment Size", "Size of segments for analysis", GH_ParamAccess.item, 5.0);
            pManager.AddIntegerParameter("Threshold Mode", "Mode", "0: Degrees, 1: Percentage, 2: Ratio 1:X", GH_ParamAccess.item, 1);
            pManager.AddColourParameter("Colors", "Colors", "Custom colors (Compliant, Non-Compliant)", GH_ParamAccess.list);
            
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Analyzed Segments", "Segments", "Segmented 3D road curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Slope Values", "Slopes", "Slope values for each segment", GH_ParamAccess.tree);
            pManager.AddPointParameter("Center Points", "Centers", "Center points of segments", GH_ParamAccess.tree);
            pManager.AddTextParameter("Segment Status", "Status", "Status (Compliant / Non-Compliant) per segment", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Compliance Percentage", "Compliance %", "Overall compliance percentage", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON legend data", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Component information", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            List<Curve> curves = new List<Curve>();
            double threshold = 8.0;
            double segmentSize = 5.0;
            int mode = 1;
            List<System.Drawing.Color> customColors = new List<System.Drawing.Color>();

            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref threshold);
            DA.GetData(2, ref segmentSize);
            DA.GetData(3, ref mode);
            DA.GetDataList(4, customColors);

            if (customColors.Count < 2)
            {
                customColors = new List<System.Drawing.Color>
                {
                    System.Drawing.Color.LimeGreen, // Compliant
                    System.Drawing.Color.Red        // Non-Compliant
                };
            }

            GH_Structure<GH_Curve> outSegments = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Number> outSlopes = new GH_Structure<GH_Number>();
            GH_Structure<GH_Point> outCenters = new GH_Structure<GH_Point>();
            GH_Structure<GH_String> outStatus = new GH_Structure<GH_String>();

            int compliantCount = 0;
            int totalCount = 0;

            for (int i = 0; i < curves.Count; i++)
            {
                Curve curve = curves[i];
                if (curve == null || !curve.IsValid) continue;

                double length = curve.GetLength();
                int numSegments = Math.Max(1, (int)Math.Ceiling(length / segmentSize));
                
                GH_Path path = new GH_Path(i);

                for (int j = 0; j < numSegments; j++)
                {
                    double t0, t1;
                    curve.LengthParameter((double)j / numSegments * length, out t0);
                    curve.LengthParameter((double)(j + 1) / numSegments * length, out t1);

                    Curve segment = curve.Trim(t0, t1);
                    if (segment == null) continue;

                    Point3d p0 = segment.PointAtStart;
                    Point3d p1 = segment.PointAtEnd;

                    double horizontalDistance = Math.Sqrt(Math.Pow(p1.X - p0.X, 2) + Math.Pow(p1.Y - p0.Y, 2));
                    double verticalDistance = Math.Abs(p1.Z - p0.Z);
                    double slopeValue = 0.0;

                    if (horizontalDistance > 0)
                    {
                        if (mode == 0) // Degrees
                        {
                            slopeValue = Math.Atan(verticalDistance / horizontalDistance) * (180.0 / Math.PI);
                        }
                        else if (mode == 1) // Percentage
                        {
                            slopeValue = (verticalDistance / horizontalDistance) * 100.0;
                        }
                        else if (mode == 2) // Ratio 1:X
                        {
                            slopeValue = horizontalDistance / verticalDistance;
                        }
                    }

                    bool isCompliant = false;
                    if (mode == 2)
                    {
                        isCompliant = slopeValue >= threshold || verticalDistance == 0;
                    }
                    else
                    {
                        isCompliant = slopeValue <= threshold;
                    }

                    outSegments.Append(new GH_Curve(segment), path);
                    outSlopes.Append(new GH_Number(slopeValue), path);
                    outCenters.Append(new GH_Point(segment.PointAtNormalizedLength(0.5)), path);
                    outStatus.Append(new GH_String(isCompliant ? "Compliant" : "Non-Compliant"), path);

                    if (isCompliant) compliantCount++;
                    totalCount++;
                }
            }

            double compliancePct = totalCount > 0 ? Math.Round((double)compliantCount / totalCount * 100.0, 1) : 0.0;

            string unit = mode == 0 ? "°" : (mode == 1 ? "%" : " Ratio");

            var jColors = new JArray();
            jColors.Add(new JObject { ["R"] = customColors[0].R, ["G"] = customColors[0].G, ["B"] = customColors[0].B });
            jColors.Add(new JObject { ["R"] = customColors[1].R, ["G"] = customColors[1].G, ["B"] = customColors[1].B });

            var legendObj = new JObject
            {
                ["Type"] = "Binary",
                ["Title"] = "3D Road Slope",
                ["Colors"] = jColors,
                ["Labels"] = new JArray($"<= {threshold}{unit}", $"> {threshold}{unit}"),
                ["SubLabels"] = new JArray($"Compliance: {compliancePct}%")
            };

            sw.Stop();

            Message = $"3D Road Slope\n{sw.ElapsedMilliseconds} ms\n---\nCompliance: {compliancePct}%";

            string infoText = "3D ROAD SLOPE ANALYZER\n\n" + 
                              "HOW IT WORKS:\n" + 
                              "Evaluates 3D curves directly to calculate the longitudinal slope at discrete intervals. Does not require a terrain mesh.\n\n" + 
                              "INTERPRETATION & IMPORTANCE:\n" + 
                              "Ensures road networks comply with accessibility and vehicular safety standards. Used for finalized grading analysis.";

            DA.SetDataTree(0, outSegments);
            DA.SetDataTree(1, outSlopes);
            DA.SetDataTree(2, outCenters);
            DA.SetDataTree(3, outStatus);
            DA.SetData(4, compliancePct);
            DA.SetData(5, legendObj.ToString());
            DA.SetData(6, infoText);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[3].SourceCount == 0)
            {
                var vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 45);
                vl.ListItems.Clear();
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Degrees", "0"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Percentage", "1"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Ratio (1:X)", "2"));
                vl.SelectItem(1);
                document.AddObject(vl, false);
                this.Params.Input[3].AddSource(vl);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("5c6978b2-654e-4f12-9c17-90c74b10547d");
    }
}
