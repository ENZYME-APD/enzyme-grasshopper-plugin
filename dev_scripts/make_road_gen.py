import os

code = """using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel.Geometry.Delaunay;

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
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "T", "Modified terrain mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("Road Table", "R", "Asphalt surface mesh", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lanes", "L", "Lane centerlines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Railings", "B", "Road boundaries and shoulders", GH_ParamAccess.list);
            pManager.AddCurveParameter("Pillars", "P", "Bridge pillar lines", GH_ParamAccess.list);
            pManager.AddMeshParameter("Cut/Fill", "V", "Debug/Volumes", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> centerlines = new List<Curve>();
            if (!DA.GetDataList(0, centerlines) || centerlines.Count == 0) return;

            Mesh terrain = null;
            DA.GetData(1, ref terrain);

            int dirs = 2, lanes = 2;
            double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0;

            DA.GetData(2, ref dirs);
            DA.GetData(3, ref lanes);
            DA.GetData(4, ref laneW);
            DA.GetData(5, ref shoulderW);
            DA.GetData(6, ref threshold);
            DA.GetData(7, ref pillarSep);
            DA.GetData(8, ref angle);
            DA.GetData(9, ref subDist);

            double totalLanes = dirs * lanes;
            double roadHalfWidth = (totalLanes * laneW) / 2.0;
            double totalHalfWidth = roadHalfWidth + shoulderW;
            double tanAngle = Math.Tan(angle * Math.PI / 180.0);
            if (tanAngle < 0.01) tanAngle = 0.01;

            List<Mesh> roadMeshes = new List<Mesh>();
            List<Curve> laneCurves = new List<Curve>();
            List<Curve> railingCurves = new List<Curve>();
            List<Curve> pillars = new List<Curve>();
            List<Mesh> volumes = new List<Mesh>();

            // For terrain modification
            List<Point3d> extraPoints = new List<Point3d>();

            foreach (Curve crv in centerlines)
            {
                if (crv == null) continue;

                double length = crv.GetLength();
                int divs = Math.Max(2, (int)(length / subDist));
                double[] tParams = crv.DivideByCount(divs, true);

                List<Point3d> leftPts = new List<Point3d>();
                List<Point3d> rightPts = new List<Point3d>();
                
                // Track lane points
                List<List<Point3d>> allLanes = new List<List<Point3d>>();
                for(int i=0; i<totalLanes; i++) allLanes.Add(new List<Point3d>());

                double lastPillarDist = 0;

                for (int i = 0; i < tParams.Length; i++)
                {
                    double t = tParams[i];
                    Point3d pt = crv.PointAt(t);
                    Vector3d tangent = crv.TangentAt(t);
                    tangent.Z = 0; // Flatten tangent to avoid banking for now
                    tangent.Unitize();
                    Vector3d normal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                    normal.Unitize();

                    Point3d left = pt + normal * totalHalfWidth;
                    Point3d right = pt - normal * totalHalfWidth;
                    
                    leftPts.Add(left);
                    rightPts.Add(right);

                    // Lanes
                    double startOffset = -roadHalfWidth + (laneW / 2.0);
                    for(int j=0; j<totalLanes; j++)
                    {
                        double offset = startOffset + j * laneW;
                        allLanes[j].Add(pt - normal * offset); // -normal to match left->right
                    }

                    // Terrain Analysis
                    if (terrain != null)
                    {
                        double zTerrain = pt.Z;
                        // Fast Z cast
                        var pt2d = new Point3d(pt.X, pt.Y, 0);
                        Point3d pOnMesh = terrain.ClosestPoint(pt); // simplified, assume top down
                        
                        // Proper raycast
                        Ray3d ray = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double rayT = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, ray);
                        if (rayT >= 0.0) zTerrain = ray.PointAt(rayT).Z;

                        double deltaZ = pt.Z - zTerrain;

                        if (deltaZ > threshold)
                        {
                            // BRIDGE
                            // Check for pillar
                            double currDist = length * ((double)i / divs);
                            if (currDist - lastPillarDist >= pillarSep)
                            {
                                pillars.Add(new LineCurve(pt, new Point3d(pt.X, pt.Y, zTerrain)));
                                lastPillarDist = currDist;
                            }
                        }
                        else
                        {
                            // GROUND (Cut or Fill)
                            extraPoints.Add(pt);
                            extraPoints.Add(left);
                            extraPoints.Add(right);
                            
                            // Blend points
                            double horizontalBlend = Math.Abs(deltaZ) / tanAngle;
                            if (horizontalBlend > 0.1)
                            {
                                Point3d leftBlend = left + normal * horizontalBlend;
                                leftBlend.Z = zTerrain;
                                Point3d rightBlend = right - normal * horizontalBlend;
                                rightBlend.Z = zTerrain;
                                extraPoints.Add(leftBlend);
                                extraPoints.Add(rightBlend);
                            }
                        }
                    }
                }

                // Build Road Mesh
                Mesh roadMesh = new Mesh();
                for (int i = 0; i < leftPts.Count; i++)
                {
                    roadMesh.Vertices.Add(leftPts[i]);
                    roadMesh.Vertices.Add(rightPts[i]);
                }
                for (int i = 0; i < leftPts.Count - 1; i++)
                {
                    int v0 = i * 2;
                    int v1 = i * 2 + 1;
                    int v2 = (i + 1) * 2;
                    int v3 = (i + 1) * 2 + 1;
                    roadMesh.Faces.AddFace(v0, v1, v3, v2);
                }
                roadMesh.Normals.ComputeNormals();
                roadMeshes.Add(roadMesh);

                railingCurves.Add(new PolylineCurve(leftPts));
                railingCurves.Add(new PolylineCurve(rightPts));

                foreach(var lanePts in allLanes)
                {
                    laneCurves.Add(new PolylineCurve(lanePts));
                }
            }

            // Terrain Integration
            Mesh modTerrain = null;
            if (terrain != null && extraPoints.Count > 0)
            {
                modTerrain = terrain.DuplicateMesh();
                
                // Very simple 2.5D integration for V1
                // Add points to mesh, then Delaunay.
                var pts = new List<Point3d>();
                var origPts = terrain.Vertices.ToPoint3dArray();
                
                // Keep original points that are far enough from the road points
                foreach (var op in origPts)
                {
                    bool tooClose = false;
                    // Simple distance check (can be slow for huge meshes, but okay for moderate)
                    // Optimised: we only check Z if XY is close
                    for (int i=0; i<extraPoints.Count; i+=5) // sparse check
                    {
                        var ep = extraPoints[i];
                        if (Math.Abs(op.X - ep.X) < totalHalfWidth * 2 && Math.Abs(op.Y - ep.Y) < totalHalfWidth * 2)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose) pts.Add(op);
                }
                
                pts.AddRange(extraPoints);

                // Run Delaunay
                var nodes = new Node2List();
                foreach (var p in pts) nodes.Append(new Node2(p.X, p.Y));
                var faces = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Connectivity(nodes, 1e-6, false);
                
                Mesh newTerrain = new Mesh();
                foreach (var p in pts) newTerrain.Vertices.Add(p);
                foreach (var f in faces)
                {
                    // Filter long edges on boundary
                    var pA = pts[f.A];
                    var pB = pts[f.B];
                    var pC = pts[f.C];
                    
                    if (pA.DistanceTo(pB) > 150 || pB.DistanceTo(pC) > 150 || pC.DistanceTo(pA) > 150)
                        continue;
                        
                    newTerrain.Faces.AddFace(f.A, f.B, f.C);
                }
                newTerrain.Normals.ComputeNormals();
                modTerrain = newTerrain;
            }
            else
            {
                modTerrain = terrain;
            }

            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(4, laneCurves);
            DA.SetDataList(5, railingCurves);
            DA.SetDataList(6, pillars);
            DA.SetDataList(7, volumes); // Cut/fill placeholder
            
            Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m";
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
