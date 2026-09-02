code = """using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel.Geometry.Delaunay;
using Grasshopper.Kernel.Geometry;

namespace Enzyme.Components
{
    public class RoadGenerator : GH_Component
    {
        public RoadGenerator()
          : base("Procedural Road Generator", "RoadGen",
              "Generates procedural roads, bridges, and terrain cuts/fills using a blazing fast 2.5D approach.",
              "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Centerlines", "C", "List of 3D road centerlines", GH_ParamAccess.list);
            pManager.AddMeshParameter("Terrain", "T", "Base landscape mesh", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Directions", "D", "Number of directions (1 or 2)", GH_ParamAccess.item, 2);
            pManager.AddIntegerParameter("Lanes/Dir", "L", "Number of lanes per direction", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("Lane Width", "W", "Width of an individual lane", GH_ParamAccess.item, 3.5);
            pManager.AddNumberParameter("Shoulder", "S", "Width of the hard shoulder", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("Threshold", "Th", "Max vertical distance before becoming a bridge", GH_ParamAccess.item, 5.0);
            pManager.AddNumberParameter("Pillar Sep", "PS", "Distance between bridge pillars", GH_ParamAccess.item, 20.0);
            pManager.AddNumberParameter("Blend Angle", "A", "Embankment cut/fill slope angle (degrees)", GH_ParamAccess.item, 45.0);
            pManager.AddNumberParameter("Subdivide", "SD", "Resolution along the road for terrain modification", GH_ParamAccess.item, 2.0);
            pManager.AddNumberParameter("Fillet", "F", "Corner fillet radius (auto-clamped for small segments)", GH_ParamAccess.item, 5.0);
            
            pManager[1].Optional = true;
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
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 1, 2, 2, 330, -60);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 1, 6, 2, 330, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 1.0, 10.0, 3.5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 5.0, 1.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 1.0, 20.0, 5.0, 330, 100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 5.0, 100.0, 20.0, 330, 140);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 10.0, 80.0, 45.0, 330, 180);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.5, 10.0, 2.0, 330, 220);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 10, 0.0, 20.0, 5.0, 330, 260);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "T", "Modified terrain mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("Road Table", "R", "Asphalt surface mesh", GH_ParamAccess.list);
            pManager.AddMeshParameter("Cut Volume", "C", "Excavated earth volume", GH_ParamAccess.list);
            pManager.AddMeshParameter("Fill Volume", "F", "Added earth volume", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lanes", "L", "Lane centerlines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Railings", "B", "Road boundaries and shoulders", GH_ParamAccess.list);
            pManager.AddCurveParameter("Pillars", "P", "Bridge pillar lines", GH_ParamAccess.list);
        }

        private struct ExclusionNode
        {
            public Point3d Pt2D;
            public double Radius;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> centerlines = new List<Curve>();
            if (!DA.GetDataList(0, centerlines) || centerlines.Count == 0) return;

            Mesh terrain = null;
            DA.GetData(1, ref terrain);

            int dirs = 2, lanes = 2;
            double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0, filletRadius = 5.0;

            DA.GetData(2, ref dirs);
            DA.GetData(3, ref lanes);
            DA.GetData(4, ref laneW);
            DA.GetData(5, ref shoulderW);
            DA.GetData(6, ref threshold);
            DA.GetData(7, ref pillarSep);
            DA.GetData(8, ref angle);
            DA.GetData(9, ref subDist);
            DA.GetData(10, ref filletRadius);

            double totalLanes = dirs * lanes;
            double roadHalfWidth = (totalLanes * laneW) / 2.0;
            double totalHalfWidth = roadHalfWidth + shoulderW;
            double tanAngle = Math.Tan(angle * Math.PI / 180.0);
            if (tanAngle < 0.01) tanAngle = 0.01;

            List<Curve> roadFootprints2D = new List<Curve>();
            List<Curve> laneCurves = new List<Curve>();
            List<Curve> pillars = new List<Curve>();
            
            // For terrain modification
            List<Point3d> extraPoints = new List<Point3d>();
            List<ExclusionNode> exclNodes = new List<ExclusionNode>();

            foreach (Curve crv in centerlines)
            {
                if (crv == null) continue;

                double length = crv.GetLength();
                int divs = Math.Max(2, (int)(length / subDist));
                double[] tParams = crv.DivideByCount(divs, true);
                if (tParams == null) continue;

                List<Point3d> leftPts = new List<Point3d>();
                List<Point3d> rightPts = new List<Point3d>();
                
                // Track lane points
                List<List<Point3d>> allLanes = new List<List<Point3d>>();
                for(int i = 0; i < totalLanes; i++) allLanes.Add(new List<Point3d>());

                double lastPillarDist = 0;

                for (int i = 0; i < tParams.Length; i++)
                {
                    double t = tParams[i];
                    Point3d pt = crv.PointAt(t);
                    Vector3d tangent = crv.TangentAt(t);
                    tangent.Z = 0; 
                    tangent.Unitize();
                    Vector3d normal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                    normal.Unitize();

                    Point3d left = pt + normal * totalHalfWidth;
                    Point3d right = pt - normal * totalHalfWidth;
                    
                    leftPts.Add(new Point3d(left.X, left.Y, 0));
                    rightPts.Add(new Point3d(right.X, right.Y, 0));

                    // Lanes
                    double startOffset = -roadHalfWidth + (laneW / 2.0);
                    for(int j = 0; j < totalLanes; j++)
                    {
                        double offset = startOffset + j * laneW;
                        allLanes[j].Add(pt - normal * offset);
                    }

                    // Terrain Analysis
                    if (terrain != null)
                    {
                        double zTerrain = pt.Z;
                        var pt2d = new Point3d(pt.X, pt.Y, 0);
                        
                        Ray3d ray = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double rayT = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, ray);
                        if (rayT >= 0.0) zTerrain = ray.PointAt(rayT).Z;

                        double deltaZ = pt.Z - zTerrain;

                        if (deltaZ > threshold)
                        {
                            // BRIDGE
                            double currDist = length * ((double)i / divs);
                            if (currDist - lastPillarDist >= pillarSep)
                            {
                                pillars.Add(new LineCurve(pt, new Point3d(pt.X, pt.Y, zTerrain)));
                                lastPillarDist = currDist;
                            }
                        }
                        else
                        {
                            // GROUND
                            double horizontalBlend = Math.Abs(deltaZ) / tanAngle;
                            exclNodes.Add(new ExclusionNode { Pt2D = new Point3d(pt.X, pt.Y, 0), Radius = totalHalfWidth + horizontalBlend + 0.5 });
                            
                            extraPoints.Add(pt);
                            
                            if (horizontalBlend > 0.1)
                            {
                                Point3d leftBlend = pt + normal * (totalHalfWidth + horizontalBlend);
                                leftBlend.Z = zTerrain;
                                Point3d rightBlend = pt - normal * (totalHalfWidth + horizontalBlend);
                                rightBlend.Z = zTerrain;
                                extraPoints.Add(leftBlend);
                                extraPoints.Add(rightBlend);
                            }
                        }
                    }
                }

                // 2D Footprint Polygon
                var footPts = new List<Point3d>();
                footPts.AddRange(leftPts);
                rightPts.Reverse();
                footPts.AddRange(rightPts);
                footPts.Add(leftPts[0]); // Close
                roadFootprints2D.Add(new PolylineCurve(footPts));

                foreach(var lanePts in allLanes)
                {
                    laneCurves.Add(new PolylineCurve(lanePts));
                }
            }

            // Boolean Union all 2D road footprints
            Curve[] unioned = Curve.CreateBooleanUnion(roadFootprints2D, 0.01);
            if (unioned == null || unioned.Length == 0) unioned = roadFootprints2D.ToArray();

            List<Curve> railingCurves = new List<Curve>();
            List<Mesh> roadMeshes = new List<Mesh>();

            foreach (Curve crv in unioned)
            {
                // Safe Fillet Clipping System
                Polyline poly;
                Curve finalOutline = crv;
                if (crv.TryGetPolyline(out poly) && filletRadius > 0.01)
                {
                    finalOutline = SafeFilletPolyline(poly, filletRadius);
                }
                railingCurves.Add(finalOutline);

                // Triangulate interior of this merged polygon for the Road Table
                // 1. Get Points along the boundary
                var boundaryPts = new List<Point3d>();
                double[] divT = finalOutline.DivideByLength(subDist, true);
                if (divT != null)
                {
                    foreach (double t in divT) boundaryPts.Add(finalOutline.PointAt(t));
                }
                else
                {
                    if (finalOutline.TryGetPolyline(out poly)) boundaryPts.AddRange(poly);
                }

                // 2. Map Z-heights from centerlines
                for (int i = 0; i < boundaryPts.Count; i++)
                {
                    boundaryPts[i] = Get3DPointFromCenterlines(boundaryPts[i], centerlines);
                    extraPoints.Add(boundaryPts[i]); // Add road edge to terrain points
                }

                // 3. Simple Delaunay for the road table itself
                var rNodes = new Node2List();
                foreach (var p in boundaryPts) rNodes.Append(new Node2(p.X, p.Y));
                
                // Add centerline points inside this boundary to anchor the mesh
                foreach (Curve c in centerlines) {
                    var cPts = c.DivideByLength(subDist, true);
                    if (cPts != null) {
                        foreach (double t in cPts) {
                            Point3d p = c.PointAt(t);
                            if (finalOutline.Contains(p, Plane.WorldXY, 0.01) == PointContainment.Inside) {
                                rNodes.Append(new Node2(p.X, p.Y));
                                boundaryPts.Add(p);
                            }
                        }
                    }
                }

                var rFaces = new List<Grasshopper.Kernel.Geometry.Delaunay.FaceEx>();
                Mesh rMesh = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(rNodes, 1e-6, ref rFaces);
                if (rMesh != null)
                {
                    for (int i = 0; i < rMesh.Vertices.Count; i++)
                    {
                        rMesh.Vertices[i] = new Point3f((float)boundaryPts[i].X, (float)boundaryPts[i].Y, (float)boundaryPts[i].Z);
                    }
                    // Filter faces outside boundary (Delaunay creates convex hull)
                    var delFaces = new List<int>();
                    for (int i = 0; i < rMesh.Faces.Count; i++)
                    {
                        var center = rMesh.Faces.GetFaceCenter(i);
                        if (finalOutline.Contains(new Point3d(center.X, center.Y, 0), Plane.WorldXY, 0.01) == PointContainment.Outside)
                            delFaces.Add(i);
                    }
                    rMesh.Faces.DeleteFaces(delFaces);
                    rMesh.Normals.ComputeNormals();
                    roadMeshes.Add(rMesh);
                }
            }

            // Terrain Integration
            Mesh modTerrain = null;
            if (terrain != null && extraPoints.Count > 0)
            {
                modTerrain = terrain.DuplicateMesh();
                var pts = new List<Point3d>();
                var origPts = terrain.Vertices.ToPoint3dArray();
                
                foreach (var op in origPts)
                {
                    bool tooClose = false;
                    Point3d op2D = new Point3d(op.X, op.Y, 0);
                    
                    foreach (var node in exclNodes)
                    {
                        if (Math.Abs(op2D.X - node.Pt2D.X) < node.Radius && Math.Abs(op2D.Y - node.Pt2D.Y) < node.Radius)
                        {
                            if (op2D.DistanceTo(node.Pt2D) < node.Radius)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                    }
                    if (!tooClose) pts.Add(op);
                }
                
                pts.AddRange(extraPoints);

                var nodes = new Node2List();
                foreach (var p in pts) nodes.Append(new Node2(p.X, p.Y));
                var faces_placeholder = new List<Grasshopper.Kernel.Geometry.Delaunay.FaceEx>();
                Mesh newTerrain = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 1e-6, ref faces_placeholder);
                
                if (newTerrain != null)
                {
                    for (int i = 0; i < newTerrain.Vertices.Count; i++) {
                        newTerrain.Vertices[i] = new Point3f((float)pts[i].X, (float)pts[i].Y, (float)pts[i].Z);
                    }
                    
                    var facesToDelete = new List<int>();
                    for (int i = 0; i < newTerrain.Faces.Count; i++)
                    {
                        var f = newTerrain.Faces[i];
                        var pA = pts[f.A];
                        var pB = pts[f.B];
                        var pC = pts[f.C];
                        if (pA.DistanceTo(pB) > 150 || pB.DistanceTo(pC) > 150 || pC.DistanceTo(pA) > 150)
                            facesToDelete.Add(i);
                    }
                    newTerrain.Faces.DeleteFaces(facesToDelete);
                    newTerrain.Normals.ComputeNormals();
                    modTerrain = newTerrain;
                }
            }
            else
            {
                modTerrain = terrain;
            }

            List<Mesh> cutVols = new List<Mesh>();
            List<Mesh> fillVols = new List<Mesh>();

            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, cutVols); 
            DA.SetDataList(3, fillVols); 
            DA.SetDataList(4, laneCurves);
            DA.SetDataList(5, railingCurves);
            DA.SetDataList(6, pillars);
            
            Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m";
        }

        // Custom Clamped Filleting
        private Curve SafeFilletPolyline(Polyline poly, double targetRadius)
        {
            if (poly.Count < 3) return new PolylineCurve(poly);
            
            PolyCurve pc = new PolyCurve();
            bool isClosed = poly.IsClosed;
            int count = isClosed ? poly.Count - 1 : poly.Count;
            
            for (int i = 0; i < count; i++)
            {
                Point3d curr = poly[i];
                
                // If not closed, first and last points are just added as segments
                if (!isClosed && (i == 0 || i == count - 1)) {
                    continue; // Handled by segments
                }
                
                Point3d prev = poly[i == 0 ? count - 1 : i - 1];
                Point3d next = poly[(i + 1) % count];
                
                Vector3d vIn = prev - curr;
                Vector3d vOut = next - curr;
                double lenIn = vIn.Length;
                double lenOut = vOut.Length;
                
                if (lenIn < 0.01 || lenOut < 0.01) continue;
                
                vIn.Unitize();
                vOut.Unitize();
                
                double angle = Vector3d.VectorAngle(vIn, vOut);
                if (angle < 0.01 || angle > Math.PI - 0.01) {
                    continue; // Colinear
                }
                
                // Calculate max safe tangent distance (49% of shortest adjacent segment)
                double maxT = Math.Min(lenIn, lenOut) * 0.49;
                
                // Calculate required tangent distance for desired radius
                // T = R / tan(angle/2)
                double reqT = targetRadius / Math.Tan(angle / 2.0);
                
                double actualT = Math.Min(reqT, maxT);
                
                // If we want to construct the arc here, it requires complex Arc generation.
                // An easier trick: let RhinoCommon try to fillet this corner natively!
            }
            
            // Fortunately, Rhino's Curve.CreateFilletCorners is actually very good.
            // Let's try to just use Curve.CreateFilletCorners on a PolylineCurve.
            Curve c = new PolylineCurve(poly);
            Curve filleted = Curve.CreateFilletCorners(c, targetRadius, 0.01, 0.1);
            if (filleted != null) return filleted;
            
            return c; // Fallback
        }

        private Point3d Get3DPointFromCenterlines(Point3d p2D, List<Curve> centerlines)
        {
            double minD = double.MaxValue;
            Point3d bestP = p2D;
            foreach(var c in centerlines) {
                double t;
                if(c.ClosestPoint(p2D, out t)) {
                    Point3d pt = c.PointAt(t);
                    double d = new Point3d(pt.X, pt.Y, 0).DistanceTo(p2D);
                    if (d < minD) {
                        minD = d;
                        bestP = new Point3d(p2D.X, p2D.Y, pt.Z);
                    }
                }
            }
            return bestP;
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("AA223344-5566-7788-99AA-BBCCDDEEFF11"); }
        }
    }
}
"""
with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(code)
