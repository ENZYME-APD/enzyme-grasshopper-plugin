using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Enzyme.Components
{
    public class KeypointFinder : GH_Component
    {
        public KeypointFinder()
          : base("Keypoint Finder", "Keypoint",
              "Analyzes stream slopes to find the inflection point (Keypoint) and extracts the Master Keyline contour.",
              "Enzyme", "LEAP")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "M", "The base topography mesh", GH_ParamAccess.item);
            pManager.AddCurveParameter("Streams", "S", "Stream curves from Hydro-DEM", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Smoothing", "Sm", "Smoothing window for slope analysis (helps ignore mesh noise)", GH_ParamAccess.item, 2);
            
            pManager[2].Optional = true;
        }

        private bool hasSources = false;
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 1, 10, 2, 330, 20);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Keypoints", "P", "The identified points of inflection (steep to flat)", GH_ParamAccess.list);
            pManager.AddCurveParameter("Master Keylines", "K", "The specific horizontal terrain contours passing through the Keypoints", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh terrain = null;
            if (!DA.GetData(0, ref terrain) || terrain == null) return;

            List<Curve> streams = new List<Curve>();
            if (!DA.GetDataList(1, streams) || streams.Count == 0) return;

            int window = 2;
            DA.GetData(2, ref window);
            if (window < 0) window = 0;

            List<Point3d> keypoints = new List<Point3d>();
            List<Curve> keylines = new List<Curve>();

            foreach (Curve stream in streams)
            {
                if (stream == null) continue;

                Polyline poly;
                if (!stream.TryGetPolyline(out poly))
                {
                    // If it's not a polyline, convert it
                    PolylineCurve pc = stream.ToPolyline(0.1, 0.1, 0.1, 1000);
                    if (pc == null || !pc.TryGetPolyline(out poly)) continue;
                }

                if (poly.Count < 4) continue; // Too short to analyze inflection

                List<double> slopes = new List<double>();
                
                // 1. Calculate raw slopes between vertices
                for (int i = 0; i < poly.Count - 1; i++)
                {
                    Point3d p1 = poly[i];
                    Point3d p2 = poly[i + 1];
                    double distXY = new Point3d(p1.X, p1.Y, 0).DistanceTo(new Point3d(p2.X, p2.Y, 0));
                    double dz = p1.Z - p2.Z; // positive means flowing down
                    slopes.Add(distXY > 0 ? dz / distXY : 0);
                }

                // 2. Smooth slopes using moving average to ignore tiny local bumps in the mesh
                List<double> smoothSlopes = new List<double>();
                for (int i = 0; i < slopes.Count; i++)
                {
                    double sum = 0;
                    int count = 0;
                    int start = Math.Max(0, i - window);
                    int end = Math.Min(slopes.Count - 1, i + window);
                    for (int j = start; j <= end; j++)
                    {
                        sum += slopes[j];
                        count++;
                    }
                    smoothSlopes.Add(sum / count);
                }

                // 3. Find the greatest transition from Steep to Flat (Maximum deceleration)
                double maxChange = double.MinValue;
                int bestIndex = -1;

                // We want a high positive slope transitioning to a low positive slope
                for (int i = 0; i < smoothSlopes.Count - 1; i++)
                {
                    double change = smoothSlopes[i] - smoothSlopes[i + 1];
                    if (change > maxChange)
                    {
                        maxChange = change;
                        bestIndex = i + 1; // The vertex between the two segments
                    }
                }

                if (bestIndex != -1)
                {
                    Point3d keypoint = poly[bestIndex];
                    keypoints.Add(keypoint);

                    // 4. Generate the Master Keyline (Contour at Keypoint Z)
                    Plane plane = new Plane(new Point3d(0, 0, keypoint.Z), Vector3d.ZAxis);
                    Polyline[] contours = Intersection.MeshPlane(terrain, plane);

                    if (contours != null && contours.Length > 0)
                    {
                        // Find the specific contour that actually intersects this valley
                        Curve bestContour = null;
                        double minDist = double.MaxValue;

                        foreach (Polyline c_poly in contours)
                        {
                            c_poly.ToPolylineCurve().ClosestPoint(keypoint, out double t);
                            double d = c_poly.ToPolylineCurve().PointAt(t).DistanceTo(keypoint);
                            if (d < minDist)
                            {
                                minDist = d;
                                bestContour = c_poly.ToPolylineCurve();
                            }
                        }

                        // If the contour is reasonably close to the stream
                        if (bestContour != null && minDist <= 5.0) // 5 units tolerance
                        {
                            keylines.Add(bestContour);
                        }
                    }
                }
            }

            Message = $"Keypoint Finder\n---\nSmoothing: {window}\nFound: {keypoints.Count}";
            DA.SetDataList(0, keypoints);
            DA.SetDataList(1, keylines);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("AA112233-4455-6677-8899-AABBCCDDEEFF"); }
        }
    }
}
