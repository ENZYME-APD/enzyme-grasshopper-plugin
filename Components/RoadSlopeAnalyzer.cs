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
                "Enzyme", "Terrain")
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

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "2D curves representing roads", GH_ParamAccess.list);
            pManager.AddMeshParameter("Terrain", "T", "Terrain mesh for projection", GH_ParamAccess.item);
            pManager.AddNumberParameter("Threshold", "Th", "Slope threshold in percentage", GH_ParamAccess.item, 8.0);
            pManager.AddNumberParameter("Segment Size", "S", "Size of segments for analysis", GH_ParamAccess.item, 5.0);
            pManager.AddBooleanParameter("Ray Upward", "R", "Cast rays upward instead of both directions", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Analyzed Segments", "A", "Road segments with slope analysis", GH_ParamAccess.list);
            pManager.AddNumberParameter("Slope Values", "S", "Slope values for each segment", GH_ParamAccess.list);
            pManager.AddPointParameter("Center Points", "C", "Center points of segments", GH_ParamAccess.list);
            pManager.AddNumberParameter("Compliance Percentage", "P", "Percentage of compliant/non-compliant segments", GH_ParamAccess.item);
            pManager.AddPointParameter("Projected Points", "PP", "Points projected onto terrain", GH_ParamAccess.list);
            pManager.AddLineParameter("Projection Lines", "PL", "Lines showing projection from original to terrain", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Input variables
            var curves = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            Rhino.Geometry.Mesh terrain = null;
            double threshold = 8.0;
            double segmentSize = 5.0;
            bool rayUpward = false;

            // Get input data
            if (!DA.GetDataList(0, curves)) return;
            if (!DA.GetData(1, ref terrain)) return;
            if (!DA.GetData(2, ref threshold)) return;
            if (!DA.GetData(3, ref segmentSize)) return;
            if (!DA.GetData(4, ref rayUpward)) return;

            // Validate input
            if (curves.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No curves provided");
                return;
            }

            if (terrain == null || !terrain.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid terrain mesh");
                return;
            }

            if (segmentSize <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Segment size must be greater than zero");
                return;
            }

            // Process the curves and analyze road slopes
            var result = AnalyzeRoadSlopes(curves, terrain, threshold, segmentSize, rayUpward);

            // Set output data
            DA.SetDataList(0, result.AnalyzedSegments);
            DA.SetDataList(1, result.SlopeValues);
            DA.SetDataList(2, result.CenterPoints);
            DA.SetData(3, result.CompliancePercentage);
            DA.SetDataList(4, result.ProjectedPoints);
            DA.SetDataList(5, result.ProjectionLines);
        }

        private RoadAnalysisResult AnalyzeRoadSlopes(System.Collections.Generic.List<Rhino.Geometry.Curve> curves,
            Rhino.Geometry.Mesh terrain, double threshold, double segmentSize, bool rayUpward)
        {
            var result = new RoadAnalysisResult();

            // Ensure the mesh has face normals
            terrain.FaceNormals.ComputeFaceNormals();

            // Process each curve
            foreach (var curve in curves)
            {
                // Skip invalid curves
                if (curve == null || !curve.IsValid) continue;

                // Divide the curve into segments
                double curveLength = curve.GetLength();
                int segmentCount = Math.Max(1, (int)Math.Ceiling(curveLength / segmentSize));
                double actualSegmentSize = curveLength / segmentCount;

                // Process each segment
                for (int i = 0; i < segmentCount; i++)
                {
                    // Get the segment start and end parameters
                    double t0 = curve.Domain.ParameterAt((double)i / segmentCount);
                    double t1 = curve.Domain.ParameterAt((double)(i + 1) / segmentCount);

                    // Get the segment points
                    Rhino.Geometry.Point3d p0 = curve.PointAt(t0);
                    Rhino.Geometry.Point3d p1 = curve.PointAt(t1);

                    // Project points onto the terrain
                    Rhino.Geometry.Point3d p0Projected = ProjectPointToMesh(p0, terrain, rayUpward);
                    Rhino.Geometry.Point3d p1Projected = ProjectPointToMesh(p1, terrain, rayUpward);

                    // Skip if projection failed
                    if (p0Projected.IsValid && p1Projected.IsValid)
                    {
                        // Create the projected segment
                        var segment = new Rhino.Geometry.Line(p0Projected, p1Projected).ToNurbsCurve();

                        // Calculate the segment slope
                        double horizontalDistance = Math.Sqrt(Math.Pow(p1Projected.X - p0Projected.X, 2) + Math.Pow(p1Projected.Y - p0Projected.Y, 2));
                        double verticalDistance = Math.Abs(p1Projected.Z - p0Projected.Z);
                        double slopePercentage = 0;

                        if (horizontalDistance > 0)
                        {
                            slopePercentage = (verticalDistance / horizontalDistance) * 100.0;
                        }

                        // Determine if the segment complies with the threshold
                        bool isCompliant = slopePercentage <= threshold;

                        // Add to results
                        result.AnalyzedSegments.Add(segment);
                        result.SlopeValues.Add(slopePercentage);
                        result.CenterPoints.Add(segment.PointAtNormalizedLength(0.5));
                        result.ProjectedPoints.Add(p0Projected);
                        result.ProjectedPoints.Add(p1Projected);
                        result.ProjectionLines.Add(new Rhino.Geometry.Line(p0, p0Projected));
                        result.ProjectionLines.Add(new Rhino.Geometry.Line(p1, p1Projected));

                        if (isCompliant)
                        {
                            result.CompliantSegmentCount++;
                        }

                        result.TotalSegmentCount++;
                    }
                }
            }

            // Calculate compliance percentage
            if (result.TotalSegmentCount > 0)
            {
                result.CompliancePercentage = (double)result.CompliantSegmentCount / result.TotalSegmentCount * 100.0;
            }

            return result;
        }

        private Rhino.Geometry.Point3d ProjectPointToMesh(Rhino.Geometry.Point3d point, Rhino.Geometry.Mesh mesh, bool rayUpward)
        {
            // Create a ray for projection
            Rhino.Geometry.Ray3d ray;

            if (rayUpward)
            {
                // Ray pointing upward from below the mesh
                ray = new Rhino.Geometry.Ray3d(
                    new Rhino.Geometry.Point3d(point.X, point.Y, mesh.GetBoundingBox(false).Min.Z - 1000),
                    Rhino.Geometry.Vector3d.ZAxis
                );
            }
            else
            {
                // Ray pointing straight down from above the mesh
                ray = new Rhino.Geometry.Ray3d(
                    new Rhino.Geometry.Point3d(point.X, point.Y, mesh.GetBoundingBox(false).Max.Z + 1000),
                    -Rhino.Geometry.Vector3d.ZAxis
                );
            }

            // Perform the ray-mesh intersection
            double t = Rhino.Geometry.Intersect.Intersection.MeshRay(mesh, ray);
            if (t >= 0)
            {
                return ray.PointAt(t);
            }

            // If no intersection, try the opposite direction
            if (!rayUpward)
            {
                ray = new Rhino.Geometry.Ray3d(
                    new Rhino.Geometry.Point3d(point.X, point.Y, mesh.GetBoundingBox(false).Min.Z - 1000),
                    Rhino.Geometry.Vector3d.ZAxis
                );

                t = Rhino.Geometry.Intersect.Intersection.MeshRay(mesh, ray);
                if (t >= 0)
                {
                    return ray.PointAt(t);
                }
            }

            // Return an invalid point if no intersection found
            return Rhino.Geometry.Point3d.Unset;
        }

        private class RoadAnalysisResult
        {
            public System.Collections.Generic.List<Rhino.Geometry.Curve> AnalyzedSegments { get; set; } = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            public System.Collections.Generic.List<double> SlopeValues { get; set; } = new System.Collections.Generic.List<double>();
            public System.Collections.Generic.List<Rhino.Geometry.Point3d> CenterPoints { get; set; } = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
            public System.Collections.Generic.List<Rhino.Geometry.Point3d> ProjectedPoints { get; set; } = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
            public System.Collections.Generic.List<Rhino.Geometry.Line> ProjectionLines { get; set; } = new System.Collections.Generic.List<Rhino.Geometry.Line>();
            public int CompliantSegmentCount { get; set; } = 0;
            public int TotalSegmentCount { get; set; } = 0;
            public double CompliancePercentage { get; set; } = 0.0;
        }
    }
}
