using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Newtonsoft.Json.Linq;

namespace Enzyme.Components
{
    public class RoadProfileUnroller : GH_Component
    {
        public RoadProfileUnroller()
            : base("Road Profile & Slope Unroller", "ProfileUnroll",
                "Unrolls a 3D road centerline into a 2D profile graph (elevation vs. distance) alongside its segmented slope analysis.",
                "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "Curves", "3D curves representing road centerlines", GH_ParamAccess.list);
            pManager.AddNumberParameter("Threshold", "Threshold", "Slope threshold", GH_ParamAccess.item, 8.0);
            pManager.AddNumberParameter("Segment Size", "Segment Size", "Size of segments for analysis", GH_ParamAccess.item, 5.0);
            pManager.AddIntegerParameter("Threshold Mode", "Mode", "0: Degrees, 1: Percentage, 2: Ratio 1:X", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Z Exaggeration", "Z Scale", "Vertical scale multiplier for the profile graph", GH_ParamAccess.item, 1.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Profile Curves", "Profile", "Continuous unrolled 2D profile curve", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Analyzed Segments", "Segments", "2D lines representing the slope segments", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Slope Values", "Slopes", "Slope values for each segment", GH_ParamAccess.tree);
            pManager.AddPointParameter("Center Points 2D", "Centers", "2D center points for tagging", GH_ParamAccess.tree);
            pManager.AddTextParameter("Segment Status", "Status", "Status (Compliant / Non-Compliant)", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Compliance Percentage", "Compliance %", "Overall compliance percentage", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON legend data", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            List<Curve> curves = new List<Curve>();
            double threshold = 8.0;
            double segmentSize = 5.0;
            int mode = 1;
            double zScale = 1.0;

            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref threshold);
            DA.GetData(2, ref segmentSize);
            DA.GetData(3, ref mode);
            DA.GetData(4, ref zScale);

            GH_Structure<GH_Curve> outProfiles = new GH_Structure<GH_Curve>();
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

                GH_Path path = new GH_Path(i);
                double totalLength = curve.GetLength();

                // 1. Generate the continuous 2D profile curve
                int samples = 200;
                double[] tParams = curve.DivideByCount(samples, true);
                List<Point3d> graphPts = new List<Point3d>();

                if (tParams != null)
                {
                    foreach (double t in tParams)
                    {
                        double l = curve.GetLength(new Interval(curve.Domain.Min, t));
                        Point3d p3d = curve.PointAt(t);
                        graphPts.Add(new Point3d(l, p3d.Z * zScale, 0));
                    }
                    if (graphPts.Count > 1)
                    {
                        Curve profileCrv = Curve.CreateInterpolatedCurve(graphPts, 3);
                        if (profileCrv != null)
                        {
                            outProfiles.Append(new GH_Curve(profileCrv), path);
                        }
                    }
                }

                // 2. Perform the Segment Slope Analysis
                int numSegments = Math.Max(1, (int)Math.Ceiling(totalLength / segmentSize));
                
                for (int j = 0; j < numSegments; j++)
                {
                    double l0 = j * (totalLength / numSegments);
                    double l1 = (j + 1) * (totalLength / numSegments);

                    double t0, t1;
                    curve.LengthParameter(l0, out t0);
                    curve.LengthParameter(l1, out t1);

                    Point3d p0 = curve.PointAt(t0);
                    Point3d p1 = curve.PointAt(t1);

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

                    // Map to 2D Graph space
                    Point3d g0 = new Point3d(l0, p0.Z * zScale, 0);
                    Point3d g1 = new Point3d(l1, p1.Z * zScale, 0);
                    Curve segment2D = new Line(g0, g1).ToNurbsCurve();
                    Point3d center2D = new Point3d((l0 + l1) / 2.0, (g0.Y + g1.Y) / 2.0, 0);

                    outSegments.Append(new GH_Curve(segment2D), path);
                    outSlopes.Append(new GH_Number(slopeValue), path);
                    outCenters.Append(new GH_Point(center2D), path);
                    outStatus.Append(new GH_String(isCompliant ? "Compliant" : "Non-Compliant"), path);

                    if (isCompliant) compliantCount++;
                    totalCount++;
                }
            }

            double compliancePct = totalCount > 0 ? Math.Round((double)compliantCount / totalCount * 100.0, 1) : 0.0;
            string unit = mode == 0 ? "°" : (mode == 1 ? "%" : " Ratio");

            var jColors = new JArray();
            jColors.Add(new JObject { ["R"] = 50, ["G"] = 205, ["B"] = 50 }); // LimeGreen
            jColors.Add(new JObject { ["R"] = 255, ["G"] = 0, ["B"] = 0 });   // Red

            var legendObj = new JObject
            {
                ["Type"] = "Binary",
                ["Title"] = "Unrolled Slope Profile",
                ["Colors"] = jColors,
                ["Labels"] = new JArray($"<= {threshold}{unit}", $"> {threshold}{unit}"),
                ["SubLabels"] = new JArray($"Compliance: {compliancePct}%")
            };

            sw.Stop();

            Message = $"Road Profile\n{sw.ElapsedMilliseconds} ms\n---\nCompliance: {compliancePct}%";

            DA.SetDataTree(0, outProfiles);
            DA.SetDataTree(1, outSegments);
            DA.SetDataTree(2, outSlopes);
            DA.SetDataTree(3, outCenters);
            DA.SetDataTree(4, outStatus);
            DA.SetData(5, compliancePct);
            DA.SetData(6, legendObj.ToString());
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
        public override Guid ComponentGuid => new Guid("78E89D42-B12A-4F33-9111-5A8D3F9A1E4B");
    }
}
