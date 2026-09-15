using System;
using System.Drawing;
using System.Diagnostics;
using Grasshopper.Kernel;
using Enzyme; // for IconLoader

namespace Enzyme.Components
{
    public class RoadSlopeAnalyzer : GH_Component
    {
        public RoadSlopeAnalyzer()
            : base("Road Slope Analyzer", "RoadSlope",
                "Analyzes road slopes by projecting 2D curves onto a terrain mesh",
                "Enzyme", "Site Analysis")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                Bitmap icon = IconLoader.Load("road_slope_icon.png");
                if (icon == null)
                {
                    this.Message = "Icon missing";
                }
                return icon;
            }
        }

        public override Guid ComponentGuid => new Guid("D4E5F6A7-B8C9-4D0E-A1F2-93A4B5C6D7E8");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 16, 8.0, 330, -40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 10.0, 5.0, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, false, 210, 40);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 220, -68);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "point", 220, -23);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "point", 220, 22);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "line", 220, 67);
            }
        }

        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "Curves", "2D curves representing roads", GH_ParamAccess.list);
            pManager.AddMeshParameter("Terrain", "Terrain", "Terrain mesh for projection", GH_ParamAccess.item);
            pManager.AddNumberParameter("Threshold", "Threshold", "Slope threshold in percentage", GH_ParamAccess.item, 8.0);
            pManager.AddNumberParameter("Segment Size", "Segment Size", "Size of segments for analysis", GH_ParamAccess.item, 5.0);
            pManager.AddBooleanParameter("Ray Upward", "Ray Upward", "Cast rays upward instead of both directions", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Analyzed Segments", "Analyzed Segments", "Road segments with slope analysis", GH_ParamAccess.list);
            pManager.AddNumberParameter("Slope Values", "Slope Values", "Slope values for each segment", GH_ParamAccess.list);
            pManager.AddPointParameter("Center Points", "Center Points", "Center points of segments", GH_ParamAccess.list);
            pManager.AddNumberParameter("Compliance Percentage", "Compliance Percentage", "Percentage of compliant/non-compliant segments", GH_ParamAccess.item);
            pManager.AddPointParameter("Projected Points", "Projected Points", "Points projected onto terrain", GH_ParamAccess.list);
            pManager.AddLineParameter("Projection Lines", "Projection Lines", "Lines showing projection from original to terrain", GH_ParamAccess.list);
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON legend data", GH_ParamAccess.item);
        }

                protected override void SolveInstance(IGH_DataAccess DA)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            System.Collections.Generic.List<Rhino.Geometry.Curve> curves = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            Rhino.Geometry.Mesh terrain = null;
            double threshold = 8.0;
            double segmentSize = 5.0;
            int mode = 1;

            if (!DA.GetDataList(0, curves)) return;
            if (!DA.GetData(1, ref terrain)) return;
            DA.GetData(2, ref threshold);
            DA.GetData(3, ref segmentSize);
            DA.GetData(4, ref mode);

            var result = AnalyzeRoadSlopes(curves, terrain, threshold, segmentSize, mode);

            sw.Stop();
            
            // Generate JSON Dashboard Data
            var jColors = new Newtonsoft.Json.Linq.JArray();
            jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = 0, ["G"] = 255, ["B"] = 0 }); // Compliant
            jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = 255, ["G"] = 0, ["B"] = 0 }); // Non-Compliant
            
            string unit = mode == 0 ? "°" : (mode == 1 ? "%" : " Ratio");
            
            var legendObj = new Newtonsoft.Json.Linq.JObject
            {
                ["Type"] = "Binary",
                ["Title"] = "Road Slope Compliance",
                ["Colors"] = jColors,
                ["Labels"] = new Newtonsoft.Json.Linq.JArray($"<= {threshold}{unit}", $"> {threshold}{unit}"),
                ["SubLabels"] = new Newtonsoft.Json.Linq.JArray($"Compliance: {result.CompliancePercentage}%")
            };

            DA.SetDataTree(0, result.AnalyzedSegments);
            DA.SetDataTree(1, result.SlopeValues);
            DA.SetDataTree(2, result.CenterPoints);
            DA.SetData(3, result.CompliancePercentage);
            DA.SetDataTree(4, result.ProjectedPoints);
            DA.SetDataTree(5, result.ProjectionLines);
            DA.SetData(6, legendObj.ToString());
            
            Message = $"Road Slope\n{sw.ElapsedMilliseconds} ms\n---\nCompliance: {result.CompliancePercentage}%";
        }

        private RoadAnalysisResult AnalyzeRoadSlopes(
            System.Collections.Generic.List<Rhino.Geometry.Curve> curves,
            Rhino.Geometry.Mesh terrain,
            double threshold,
            double segmentSize,
            int mode)
        {
            var result = new RoadAnalysisResult();
            terrain.FaceNormals.ComputeFaceNormals();

            for (int curveIndex = 0; curveIndex < curves.Count; curveIndex++)
            {
                var curve = curves[curveIndex];
                if (curve == null || !curve.IsValid) continue;

                double curveLength = curve.GetLength();
                int segmentCount = System.Math.Max(1, (int)System.Math.Ceiling(curveLength / segmentSize));
                double actualSegmentSize = curveLength / segmentCount;

                for (int i = 0; i < segmentCount; i++)
                {
                    double t0 = curve.Domain.ParameterAt((double)i / segmentCount);
                    double t1 = curve.Domain.ParameterAt((double)(i + 1) / segmentCount);

                    Rhino.Geometry.Point3d p0 = curve.PointAt(t0);
                    Rhino.Geometry.Point3d p1 = curve.PointAt(t1);

                    Rhino.Geometry.Point3d p0Projected = ProjectPointToMesh(p0, terrain);
                    Rhino.Geometry.Point3d p1Projected = ProjectPointToMesh(p1, terrain);

                    if (p0Projected.IsValid && p1Projected.IsValid)
                    {
                        var segment = new Rhino.Geometry.Line(p0Projected, p1Projected).ToNurbsCurve();

                        double horizontalDistance = System.Math.Sqrt(System.Math.Pow(p1Projected.X - p0Projected.X, 2) + System.Math.Pow(p1Projected.Y - p0Projected.Y, 2));
                        double verticalDistance = System.Math.Abs(p1Projected.Z - p0Projected.Z);
                        double slopeValue = 0;

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
                        if (mode == 2) {
                            // For ratio, a larger number means flatter, so compliant if >= threshold
                            isCompliant = slopeValue >= threshold || verticalDistance == 0;
                        } else {
                            isCompliant = slopeValue <= threshold;
                        }

                        int branchIndex = isCompliant ? 0 : 1;
                        var path = new Grasshopper.Kernel.Data.GH_Path(branchIndex);

                        result.AnalyzedSegments.Append(new Grasshopper.Kernel.Types.GH_Curve(segment), path);
                        result.SlopeValues.Append(new Grasshopper.Kernel.Types.GH_Number(slopeValue), path);
                        result.CenterPoints.Append(new Grasshopper.Kernel.Types.GH_Point(segment.PointAtNormalizedLength(0.5)), path);
                        result.ProjectedPoints.Append(new Grasshopper.Kernel.Types.GH_Point(p0Projected), path);
                        result.ProjectedPoints.Append(new Grasshopper.Kernel.Types.GH_Point(p1Projected), path);
                        result.ProjectionLines.Append(new Grasshopper.Kernel.Types.GH_Line(new Rhino.Geometry.Line(p0, p0Projected)), path);
                        result.ProjectionLines.Append(new Grasshopper.Kernel.Types.GH_Line(new Rhino.Geometry.Line(p1, p1Projected)), path);

                        if (isCompliant) result.CompliantSegmentCount++;
                        result.TotalSegmentCount++;
                    }
                }
            }

            if (result.TotalSegmentCount > 0)
            {
                result.CompliancePercentage = System.Math.Round((double)result.CompliantSegmentCount / result.TotalSegmentCount * 100.0, 1);
            }
            return result;
        }

        private Rhino.Geometry.Point3d ProjectPointToMesh(Rhino.Geometry.Point3d point, Rhino.Geometry.Mesh mesh)
        {
            // Automatic robust bi-directional projection (downwards first, then upwards)
            var rayDown = new Rhino.Geometry.Ray3d(new Rhino.Geometry.Point3d(point.X, point.Y, mesh.GetBoundingBox(false).Max.Z + 1000), -Rhino.Geometry.Vector3d.ZAxis);
            double tDown = Rhino.Geometry.Intersect.Intersection.MeshRay(mesh, rayDown);
            if (tDown >= 0) return rayDown.PointAt(tDown);

            var rayUp = new Rhino.Geometry.Ray3d(new Rhino.Geometry.Point3d(point.X, point.Y, mesh.GetBoundingBox(false).Min.Z - 1000), Rhino.Geometry.Vector3d.ZAxis);
            double tUp = Rhino.Geometry.Intersect.Intersection.MeshRay(mesh, rayUp);
            if (tUp >= 0) return rayUp.PointAt(tUp);

            return Rhino.Geometry.Point3d.Unset;
        }

private class RoadAnalysisResult
        {
            public Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve> AnalyzedSegments { get; set; } 
                = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Curve>();
            public Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number> SlopeValues { get; set; } 
                = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number>();
            public Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> CenterPoints { get; set; } 
                = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();
            public Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point> ProjectedPoints { get; set; } 
                = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Point>();
            public Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Line> ProjectionLines { get; set; } 
                = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Line>();
            public int CompliantSegmentCount { get; set; } = 0;
            public int TotalSegmentCount { get; set; } = 0;
            public double CompliancePercentage { get; set; } = 0.0;
        }
    }
}
