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
            pManager.AddNumberParameter("Reference Y", "Ref Y", "Base elevation (Y-coordinate) to drop alignment lines down to", GH_ParamAccess.item, 0.0);
            
            pManager[5].Optional = true;
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
            pManager.AddCurveParameter("Alignment Lines", "Align Lines", "Vertical lines dropping from breakpoints to Ref Y", GH_ParamAccess.tree);
            pManager.AddTextParameter("Alignment Labels", "Align Labels", "Structural geometry labels (Line, Arc, Spline)", GH_ParamAccess.tree);
            pManager.AddPointParameter("Alignment Points", "Align Pts", "Placement points for alignment labels", GH_ParamAccess.tree);
        }

                protected override void SolveInstance(IGH_DataAccess DA)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            System.Collections.Generic.List<Rhino.Geometry.Curve> curves = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            double threshold = 8.0;
            double segmentSize = 5.0;
            int mode = 1;
            double zScale = 1.0;
            double refY = 0.0;

            if (!DA.GetDataList(0, curves)) return;
            DA.GetData(1, ref threshold);
            DA.GetData(2, ref segmentSize);
            DA.GetData(3, ref mode);
            DA.GetData(4, ref zScale);
            DA.GetData(5, ref refY);

            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve> outProfiles = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve> outSegments = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number> outSlopes = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> outCenters = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_String> outStatus = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_String>();
            
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve> outAlignLines = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_String> outAlignLabels = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_String>();
            Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> outAlignPts = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();

            int compliantCount = 0;
            int totalCount = 0;

            for (int i = 0; i < curves.Count; i++)
            {
                Rhino.Geometry.Curve curve = curves[i];
                if (curve == null || !curve.IsValid) continue;

                Grasshopper.Kernel.Data.GH_Path path = new Grasshopper.Kernel.Data.GH_Path(i);
                double totalLength = curve.GetLength();

                // 1. Generate the continuous 2D profile curve
                int samples = 200;
                double[] tParams = curve.DivideByCount(samples, true);
                System.Collections.Generic.List<Rhino.Geometry.Point3d> graphPts = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

                if (tParams != null)
                {
                    foreach (double t in tParams)
                    {
                        double l = curve.GetLength(new Rhino.Geometry.Interval(curve.Domain.Min, t));
                        Rhino.Geometry.Point3d p3d = curve.PointAt(t);
                        graphPts.Add(new Rhino.Geometry.Point3d(l, p3d.Z * zScale, 0));
                    }
                    if (graphPts.Count > 1)
                    {
                        Rhino.Geometry.Curve profileCrv = Rhino.Geometry.Curve.CreateInterpolatedCurve(graphPts, 3);
                        if (profileCrv != null)
                        {
                            outProfiles.Append(new Grasshopper.Kernel.Types.GH_Curve(profileCrv), path);
                        }
                    }
                }

                // 2. Perform the Segment Slope Analysis
                int numSegments = System.Math.Max(1, (int)System.Math.Ceiling(totalLength / segmentSize));
                
                for (int j = 0; j < numSegments; j++)
                {
                    double l0 = j * (totalLength / numSegments);
                    double l1 = (j + 1) * (totalLength / numSegments);

                    double t0, t1;
                    curve.LengthParameter(l0, out t0);
                    curve.LengthParameter(l1, out t1);

                    Rhino.Geometry.Point3d p0 = curve.PointAt(t0);
                    Rhino.Geometry.Point3d p1 = curve.PointAt(t1);

                    double horizontalDistance = System.Math.Sqrt(System.Math.Pow(p1.X - p0.X, 2) + System.Math.Pow(p1.Y - p0.Y, 2));
                    double verticalDistance = System.Math.Abs(p1.Z - p0.Z);
                    double slopeValue = 0.0;

                    if (horizontalDistance > 0)
                    {
                        if (mode == 0) // Degrees
                        {
                            slopeValue = System.Math.Atan(verticalDistance / horizontalDistance) * (180.0 / System.Math.PI);
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
                    Rhino.Geometry.Point3d g0 = new Rhino.Geometry.Point3d(l0, p0.Z * zScale, 0);
                    Rhino.Geometry.Point3d g1 = new Rhino.Geometry.Point3d(l1, p1.Z * zScale, 0);
                    Rhino.Geometry.Curve segment2D = new Rhino.Geometry.Line(g0, g1).ToNurbsCurve();
                    Rhino.Geometry.Point3d center2D = new Rhino.Geometry.Point3d((l0 + l1) / 2.0, (g0.Y + g1.Y) / 2.0, 0);

                    outSegments.Append(new Grasshopper.Kernel.Types.GH_Curve(segment2D), path);
                    outSlopes.Append(new Grasshopper.Kernel.Types.GH_Number(slopeValue), path);
                    outCenters.Append(new Grasshopper.Kernel.Types.GH_Point(center2D), path);
                    outStatus.Append(new Grasshopper.Kernel.Types.GH_String(isCompliant ? "Compliant" : "Non-Compliant"), path);

                    if (isCompliant) compliantCount++;
                    totalCount++;
                }
                
                // 3. Plan Alignment Markers (Structural Segments)
                Rhino.Geometry.Curve[] structuralSegments = curve.DuplicateSegments();
                if (structuralSegments == null || structuralSegments.Length == 0)
                {
                    structuralSegments = new Rhino.Geometry.Curve[] { curve };
                }

                double accumulatedLength = 0;
                foreach (Rhino.Geometry.Curve sSeg in structuralSegments)
                {
                    double sLen = sSeg.GetLength();
                    double l0 = accumulatedLength;
                    double l1 = accumulatedLength + sLen;
                    
                    double y0 = sSeg.PointAtStart.Z * zScale;
                    Rhino.Geometry.Line vLine = new Rhino.Geometry.Line(new Rhino.Geometry.Point3d(l0, y0, 0), new Rhino.Geometry.Point3d(l0, refY, 0));
                    outAlignLines.Append(new Grasshopper.Kernel.Types.GH_Curve(vLine.ToNurbsCurve()), path);
                    
                    string label = "Spline";
                    if (sSeg.IsLinear(0.01)) 
                    {
                        label = $"Line L={System.Math.Round(sLen, 1)}";
                    }
                    else if (sSeg.IsArc(0.01))
                    {
                        Rhino.Geometry.Arc arc;
                        if (sSeg.TryGetArc(out arc))
                        {
                            label = $"Arc R={System.Math.Round(arc.Radius, 1)}";
                        }
                    }
                    else 
                    {
                        label = $"Spline L={System.Math.Round(sLen, 1)}";
                    }
                    
                    outAlignLabels.Append(new Grasshopper.Kernel.Types.GH_String(label), path);
                    outAlignPts.Append(new Grasshopper.Kernel.Types.GH_Point(new Rhino.Geometry.Point3d((l0 + l1) / 2.0, refY - (2.0 * zScale), 0)), path);
                    
                    accumulatedLength = l1;
                }
                
                // Add final vertical line at the end
                if (structuralSegments.Length > 0)
                {
                    double yEnd = structuralSegments[structuralSegments.Length - 1].PointAtEnd.Z * zScale;
                    Rhino.Geometry.Line vEnd = new Rhino.Geometry.Line(new Rhino.Geometry.Point3d(accumulatedLength, yEnd, 0), new Rhino.Geometry.Point3d(accumulatedLength, refY, 0));
                    outAlignLines.Append(new Grasshopper.Kernel.Types.GH_Curve(vEnd.ToNurbsCurve()), path);
                }
            }

            double compliancePct = totalCount > 0 ? System.Math.Round((double)compliantCount / totalCount * 100.0, 1) : 0.0;
            string unit = mode == 0 ? "°" : (mode == 1 ? "%" : " Ratio");

            var jColors = new Newtonsoft.Json.Linq.JArray();
            jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = 50, ["G"] = 205, ["B"] = 50 });
            jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = 255, ["G"] = 0, ["B"] = 0 });

            var legendObj = new Newtonsoft.Json.Linq.JObject
            {
                ["Type"] = "Binary",
                ["Title"] = "Unrolled Slope Profile",
                ["Colors"] = jColors,
                ["Labels"] = new Newtonsoft.Json.Linq.JArray($"<= {threshold}{unit}", $"> {threshold}{unit}"),
                ["SubLabels"] = new Newtonsoft.Json.Linq.JArray($"Compliance: {compliancePct}%")
            };

            sw.Stop(); //
            double maxVal = double.MinValue;
            double minVal = double.MaxValue;
            double sumVal = 0;
            int countVal = 0;
            foreach (Grasshopper.Kernel.Types.GH_Number val in outSlopes.AllData(true))
            {
                if(val != null) {
                    double v = val.Value;
                    if(v > maxVal) maxVal = v;
                    if(v < minVal) minVal = v;
                    sumVal += v;
                    countVal++;
                }
            }
            if(countVal == 0) { maxVal = 0; minVal = 0; }
            double avgVal = countVal > 0 ? sumVal / countVal : 0;
            string modeName = mode == 0 ? "Degrees" : (mode == 1 ? "Percentage" : "Ratio");
            string unitStr = mode == 0 ? "°" : (mode == 1 ? "%" : "");
            string prefix = mode == 2 ? "1:" : "";
            string statMax = countVal > 0 ? $"{prefix}{System.Math.Round((mode == 2 ? minVal : maxVal), 1)}{unitStr}" : "N/A";
            string statMin = countVal > 0 ? $"{prefix}{System.Math.Round((mode == 2 ? maxVal : minVal), 1)}{unitStr}" : "N/A";
            string statAvg = countVal > 0 ? $"{prefix}{System.Math.Round(avgVal, 1)}{unitStr}" : "N/A";
            
            Message = $"Road Profile\n{sw.ElapsedMilliseconds} ms\n---\nMode: {modeName}\nMax: {statMax}\nMin: {statMin}\nAvg: {statAvg}\nCompliance: {compliancePct}%";


            DA.SetDataTree(0, outProfiles);
            DA.SetDataTree(1, outSegments);
            DA.SetDataTree(2, outSlopes);
            DA.SetDataTree(3, outCenters);
            DA.SetDataTree(4, outStatus);
            DA.SetData(5, compliancePct);
            DA.SetData(6, legendObj.ToString());
            DA.SetDataTree(7, outAlignLines);
            DA.SetDataTree(8, outAlignLabels);
            DA.SetDataTree(9, outAlignPts);
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
