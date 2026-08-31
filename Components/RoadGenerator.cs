using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel.Geometry.Delaunay;
using System.Diagnostics;
using Grasshopper.Kernel.Geometry;

namespace Enzyme.Components
{
    public class RoadGenerator : GH_Component
    {
        private struct ExclusionNode
        {
            public Point3d Pt2D;
            public double Radius;
        }
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
            pManager.AddBooleanParameter("Colorize", "Col", "Colorize terrain/volumes for Cut (Red) and Fill (Blue)", GH_ParamAccess.item, true);
            
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
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 4, 1.0, 10.0, 3.5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 5, 0.0, 5.0, 1.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 6, 1.0, 20.0, 5.0, 330, 100);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 7, 5.0, 100.0, 20.0, 330, 140);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 8, 10, 80, 45, 330, 180);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 9, 0.5, 10.0, 2.0, 330, 220);
                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 10, true, 330, 260);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", -250, -60);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "mesh", -250, -20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "curve", -250, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", -250, 60);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", -250, 100);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "mesh", -250, 140);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 6, "mesh", -250, 180);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "T", "Modified terrain mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("Road Table", "R", "Asphalt surface mesh", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lanes", "L", "Lane centerlines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Railings", "B", "Road boundaries and shoulders", GH_ParamAccess.list);
            pManager.AddCurveParameter("Pillars", "P", "Bridge pillar lines", GH_ParamAccess.list);
            pManager.AddMeshParameter("Cut Volume", "C", "Excavated earth volume", GH_ParamAccess.list);
            pManager.AddMeshParameter("Fill Volume", "F", "Added earth volume", GH_ParamAccess.list);
        }

                protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            List<Curve> centerlines = new List<Curve>();
            if (!DA.GetDataList(0, centerlines) || centerlines.Count == 0) return;

            Mesh terrain = null;
            DA.GetData(1, ref terrain);

            int dirs = 2, lanes = 2;
            double laneW = 3.5, shoulderW = 1.5, threshold = 5.0, pillarSep = 20.0, angle = 45.0, subDist = 2.0;
            bool colorize = true;

            DA.GetData(2, ref dirs);
            DA.GetData(3, ref lanes);
            DA.GetData(4, ref laneW);
            DA.GetData(5, ref shoulderW);
            DA.GetData(6, ref threshold);
            DA.GetData(7, ref pillarSep);
            DA.GetData(8, ref angle);
            DA.GetData(9, ref subDist);
            DA.GetData(10, ref colorize);

            double totalLanes = dirs * lanes;
            double roadHalfWidth = (totalLanes * laneW) / 2.0;
            double totalHalfWidth = roadHalfWidth + shoulderW;
            double angleRad = angle * Math.PI / 180.0;
            if (angleRad < 0.01) angleRad = 0.01;
            if (angleRad > 1.5) angleRad = 1.5;

            double totalCutM3 = 0.0;
            double totalFillM3 = 0.0;
            double buffer = subDist * 1.5; 

            List<RoadData> roads = new List<RoadData>();

            for (int k = 0; k < centerlines.Count; k++)
            {
                Curve crv = centerlines[k];
                if (crv == null) continue;

                Curve nCrv = crv.ToNurbsCurve();
                RoadData rd = new RoadData();
                rd.IsClosed = nCrv.IsClosed;
                
                for (int i = 0; i < totalLanes; i++) rd.allLanes.Add(new List<Point3d>());

                double length = nCrv.GetLength();
                int divs = Math.Max(2, (int)(length / subDist));
                
                // NATIVE ARC-LENGTH DIVISION: No Chord-Jumping across hairpins, and no domain-interpolation fallback twists
                double[] tParams = nCrv.DivideByCount(divs, false); // false = strictly arc-length
                
                if (tParams == null || tParams.Length < 2) {
                    // Absolute fallback: Rebuild the curve to ensure perfectly uniform parameterization and try again
                    nCrv = nCrv.Rebuild(Math.Max(10, divs), 3, true);
                    tParams = nCrv.DivideByCount(divs, false);
                }
                
                if (tParams == null || tParams.Length < 2) {
                    // Final safety net
                    tParams = new double[divs + 1];
                    for (int i = 0; i <= divs; i++) {
                        tParams[i] = nCrv.Domain.T0 + (nCrv.Domain.T1 - nCrv.Domain.T0) * ((double)i / divs);
                    }
                }

                double lastPillarDist = 0;
                Vector3d prevTangent = Vector3d.Unset;

                for (int i = 0; i < tParams.Length; i++)
                {
                    if (rd.IsClosed && i == tParams.Length - 1 && rd.leftPts.Count > 0)
                    {
                        rd.leftPts.Add(rd.leftPts[0]);
                        rd.rightPts.Add(rd.rightPts[0]);
                        for (int j = 0; j < totalLanes; j++) rd.allLanes[j].Add(rd.allLanes[j][0]);
                        if (rd.roadProfiles.Count > 0) {
                            rd.roadProfiles.Add(rd.roadProfiles[0]);
                            rd.terrProfiles.Add(rd.terrProfiles[0]);
                        }
                        continue;
                    }

                    double t = tParams[i];
                    Point3d pt = nCrv.PointAt(t);
                    Vector3d tangent = nCrv.TangentAt(t);
                    tangent.Z = 0; 
                    if (!tangent.Unitize()) {
                        if (prevTangent != Vector3d.Unset) tangent = prevTangent;
                        else tangent = Vector3d.XAxis;
                    } else {
                        // CRITICAL FIX: If the NURBS segment was joined backwards, the tangent will flip 180 degrees.
                        // We strictly un-flip it to preserve continuous sweeping orientation!
                        if (prevTangent != Vector3d.Unset && tangent * prevTangent < 0.0) {
                            tangent = -tangent; 
                        }
                        prevTangent = tangent;
                    }
                    
                    Vector3d normal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                    normal.Unitize();

                    Point3d left = pt + normal * totalHalfWidth;
                    Point3d right = pt - normal * totalHalfWidth;
                    
                    rd.leftPts.Add(left);
                    rd.rightPts.Add(right);
                    rd.asphaltCenters.Add(new Tuple<Point3d, int>(new Point3d(pt.X, pt.Y, 0), i));

                    double startOffset = -roadHalfWidth + (laneW / 2.0);
                    for (int j = 0; j < totalLanes; j++)
                    {
                        double offset = startOffset + j * laneW;
                        rd.allLanes[j].Add(pt - normal * offset); 
                    }

                    if (terrain != null)
                    {
                        double zTerrain = pt.Z, zLeftT = left.Z, zRightT = right.Z;
                        
                        Ray3d rC = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tC = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rC);
                        bool onTerrain = (tC >= 0.0);
                        if (onTerrain) zTerrain = rC.PointAt(tC).Z;

                        if (onTerrain)
                        {
                            Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                            if (tL >= 0.0) zLeftT = rL.PointAt(tL).Z;
                            else zLeftT = zTerrain; // Fallback to center terrain height to prevent 0-width embankments if raycast misses a tiny hole

                            Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                            if (tR >= 0.0) zRightT = rR.PointAt(tR).Z;
                            else zRightT = zTerrain;

                            double deltaZ = pt.Z - zTerrain;

                            if (deltaZ > threshold)
                            {
                                double currDist = length * ((double)i / tParams.Length);
                                if (currDist - lastPillarDist >= pillarSep)
                                {
                                    rd.pillars.Add(new LineCurve(pt, new Point3d(pt.X, pt.Y, zTerrain)));
                                    lastPillarDist = currDist;
                                }
                                rd.roadProfiles.Add(new Point3d[] { left, left, pt, right, right });
                                rd.terrProfiles.Add(new Point3d[] { left, left, pt, right, right });
                            }
                            else
                            {
                                Point3d leftBlend = left;
                                if (zLeftT > left.Z + 0.1) {
                                    Vector3d dir = normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir));
                                    leftBlend = hit >= 0 ? left + dir * hit : new Point3d(left.X, left.Y, zLeftT);
                                } else if (zLeftT < left.Z - 0.1) {
                                    Vector3d dir = normal * Math.Cos(angleRad) - Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir));
                                    leftBlend = hit >= 0 ? left + dir * hit : new Point3d(left.X, left.Y, zLeftT);
                                }

                                Point3d rightBlend = right;
                                if (zRightT > right.Z + 0.1) {
                                    Vector3d dir = -normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir));
                                    rightBlend = hit >= 0 ? right + dir * hit : new Point3d(right.X, right.Y, zRightT);
                                } else if (zRightT < right.Z - 0.1) {
                                    Vector3d dir = -normal * Math.Cos(angleRad) - Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir));
                                    rightBlend = hit >= 0 ? right + dir * hit : new Point3d(right.X, right.Y, zRightT);
                                }

                                rd.extraPoints.Add(new Tuple<Point3d, int>(pt, i));
                                rd.extraPoints.Add(new Tuple<Point3d, int>(left, i));
                                rd.extraPoints.Add(new Tuple<Point3d, int>(right, i));
                                rd.extraPoints.Add(new Tuple<Point3d, int>(leftBlend, i));
                                rd.extraPoints.Add(new Tuple<Point3d, int>(rightBlend, i));

                                double exclL = new Point3d(leftBlend.X, leftBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                                double exclR = new Point3d(rightBlend.X, rightBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                                rd.daylightFootprints.Add(new Tuple<Point3d, double>(new Point3d(pt.X, pt.Y, 0), Math.Max(exclL, exclR) + buffer));

                                Point3d leftT = new Point3d(left.X, left.Y, zLeftT);
                                Point3d rightT = new Point3d(right.X, right.Y, zRightT);
                                Point3d ptT = new Point3d(pt.X, pt.Y, zTerrain);

                                rd.roadProfiles.Add(new Point3d[] { leftBlend, left, pt, right, rightBlend });
                                rd.terrProfiles.Add(new Point3d[] { leftBlend, leftT, ptT, rightT, rightBlend });
                            }
                        }
                    }
                }
                roads.Add(rd);
            }

            List<Curve> laneCurves = new List<Curve>();
            List<Curve> pillarsOut = new List<Curve>();
            List<Curve> railingCurves = new List<Curve>();
            List<Mesh> roadMeshes = new List<Mesh>();
            List<Mesh> cutVols = new List<Mesh>();
            List<Mesh> fillVols = new List<Mesh>();
            
            List<Point3d> allCleanExtraPoints = new List<Point3d>();
            List<Tuple<Point3d, double>> allDaylightFootprints = new List<Tuple<Point3d, double>>();
            
            double localIndexThreshold = 30.0 / subDist; 

            for (int k = 0; k < roads.Count; k++)
            {
                RoadData rd = roads[k];
                allDaylightFootprints.AddRange(rd.daylightFootprints);

                foreach (var ep in rd.extraPoints)
                {
                    Point3d p = ep.Item1;
                    int current_i = ep.Item2;
                    Point3d p2D = new Point3d(p.X, p.Y, 0);
                    bool culled = false;
                    for (int j = 0; j < roads.Count; j++) {
                        foreach (var center in roads[j].asphaltCenters) {
                            if (j == k && Math.Abs(center.Item2 - current_i) < localIndexThreshold) continue; 
                            
                            if (p2D.DistanceTo(center.Item1) < totalHalfWidth + buffer) {
                                culled = true; break;
                            }
                        }
                        if (culled) break;
                    }
                    if (!culled) allCleanExtraPoints.Add(p);
                }

                Mesh roadMesh = new Mesh();
                for (int i = 0; i < rd.leftPts.Count; i++) {
                    roadMesh.Vertices.Add(rd.leftPts[i]);
                    roadMesh.Vertices.Add(rd.rightPts[i]);
                }
                for (int i = 0; i < rd.leftPts.Count - 1; i++) {
                    int v0 = i * 2, v1 = i * 2 + 1, v2 = (i + 1) * 2, v3 = (i + 1) * 2 + 1;
                    roadMesh.Faces.AddFace(v0, v1, v3);
                    roadMesh.Faces.AddFace(v0, v3, v2);
                }
                roadMesh.Faces.CullDegenerateFaces();
                roadMesh.Vertices.CullUnused();
                roadMesh.Compact();
                roadMesh.Normals.ComputeNormals();
                if (roadMesh.IsValid && roadMesh.Faces.Count > 0) roadMeshes.Add(roadMesh);

                railingCurves.Add(new PolylineCurve(rd.leftPts));
                railingCurves.Add(new PolylineCurve(rd.rightPts));
                foreach(var lanePts in rd.allLanes) laneCurves.Add(new PolylineCurve(lanePts));
                pillarsOut.AddRange(rd.pillars);

                Mesh cutMesh = new Mesh();
                Mesh fillMesh = new Mesh();
                
                for (int i = 0; i < rd.roadProfiles.Count - 1; i++)
                {
                    Point3d[] rp1 = rd.roadProfiles[i];
                    Point3d[] rp2 = rd.roadProfiles[i + 1];
                    Point3d[] tp1 = rd.terrProfiles[i];
                    Point3d[] tp2 = rd.terrProfiles[i + 1];

                    if (rp1[2].DistanceTo(rp2[2]) > subDist * 3.5) continue; 

                    bool isCut = rp1[2].Z < tp1[2].Z; 
                    Mesh target = isCut ? cutMesh : fillMesh;
                    System.Drawing.Color c = isCut ? System.Drawing.Color.Red : System.Drawing.Color.Blue;
                    
                    int bIdx = target.Vertices.Count;
                    Point3d[] top1 = isCut ? tp1 : rp1;
                    Point3d[] top2 = isCut ? tp2 : rp2;
                    Point3d[] bot1 = isCut ? rp1 : tp1;
                    Point3d[] bot2 = isCut ? rp2 : tp2;

                    for(int j=0; j<5; j++) target.Vertices.Add(top1[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(top2[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(bot1[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(bot2[j]);

                    if (colorize) for (int j = 0; j < 20; j++) target.VertexColors.Add(c);

                    for (int j = 0; j < 4; j++) {
                        if (top1[j].DistanceTo(top1[j+1]) > 0.001) {
                            int A = bIdx + j, B = bIdx + j + 1, C = bIdx + j + 6, D = bIdx + j + 5;
                            target.Faces.AddFace(A, B, C); target.Faces.AddFace(A, C, D);
                        }
                    }
                    int bOff = bIdx + 10;
                    for (int j = 0; j < 4; j++) {
                        if (bot1[j].DistanceTo(bot1[j+1]) > 0.001) {
                            int A = bOff + j, B = bOff + j + 5, C = bOff + j + 6, D = bOff + j + 1;
                            target.Faces.AddFace(A, B, C); target.Faces.AddFace(A, C, D);
                        }
                    }
                    
                    if (i == 0 && (!rd.IsClosed)) {
                        for (int j = 0; j < 4; j++) {
                           if (top1[j].DistanceTo(bot1[j]) > 0.001 || top1[j+1].DistanceTo(bot1[j+1]) > 0.001) {
                               int A = bIdx + j, B = bOff + j, C = bOff + j + 1, D = bIdx + j + 1;
                               target.Faces.AddFace(A, B, C); target.Faces.AddFace(A, C, D);
                           }
                        }
                    }
                    if (i == rd.roadProfiles.Count - 2 && (!rd.IsClosed)) {
                        for (int j = 0; j < 4; j++) {
                           if (top2[j].DistanceTo(bot2[j]) > 0.001 || top2[j+1].DistanceTo(bot2[j+1]) > 0.001) {
                               int A = bIdx + j + 5, B = bIdx + j + 6, C = bOff + j + 6, D = bOff + j + 5;
                               target.Faces.AddFace(A, B, C); target.Faces.AddFace(A, C, D);
                           }
                        }
                    }

                    for (int j = 0; j < 4; j++) {
                        double vol1 = TriVolume(top1[j], top1[j+1], top2[j], bot1[j], bot1[j+1], bot2[j]);
                        double vol2 = TriVolume(top1[j+1], top2[j+1], top2[j], bot1[j+1], bot2[j+1], bot2[j]);
                        if (isCut) totalCutM3 += vol1 + vol2;
                        else totalFillM3 += vol1 + vol2;
                    }
                }

                if (cutMesh.Faces.Count > 0) { 
                    cutMesh.Faces.CullDegenerateFaces();
                    cutMesh.Vertices.CullUnused();
                    cutMesh.Compact();
                    cutMesh.Weld(3.14159);
                    cutMesh.Normals.ComputeNormals(); 
                    if (cutMesh.IsValid) cutVols.Add(cutMesh); 
                }
                if (fillMesh.Faces.Count > 0) { 
                    fillMesh.Faces.CullDegenerateFaces();
                    fillMesh.Vertices.CullUnused();
                    fillMesh.Compact();
                    fillMesh.Weld(3.14159);
                    fillMesh.Normals.ComputeNormals(); 
                    if (fillMesh.IsValid) fillVols.Add(fillMesh); 
                }
            }

            Mesh modTerrain = null;
            if (terrain != null && allCleanExtraPoints.Count > 0)
            {
                modTerrain = terrain.DuplicateMesh();
                
                Rhino.Geometry.PointCloud pc = new Rhino.Geometry.PointCloud();
                List<Point3d> cleanOrig = new List<Point3d>();
                foreach (var op in terrain.Vertices.ToPoint3dArray())
                {
                    bool tooClose = false;
                    Point3d op2D = new Point3d(op.X, op.Y, 0);
                    foreach (var f in allDaylightFootprints)
                    {
                        if (op2D.DistanceTo(f.Item1) < f.Item2) { tooClose = true; break; }
                    }
                    if (!tooClose) {
                        pc.Add(op);
                        cleanOrig.Add(op);
                    }
                }
                
                foreach(var ep in allCleanExtraPoints) {
                    pc.Add(ep);
                    cleanOrig.Add(ep);
                }

                var nodes = new Node2List();
                var faces_placeholder = new List<Grasshopper.Kernel.Geometry.Delaunay.Face>();
                foreach (var p in cleanOrig) nodes.Append(new Node2(p.X, p.Y));
                
                Mesh newTerrain = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 1e-6, ref faces_placeholder);
                
                for (int i = 0; i < newTerrain.Vertices.Count; i++) {
                    newTerrain.Vertices[i] = new Rhino.Geometry.Point3f((float)cleanOrig[i].X, (float)cleanOrig[i].Y, (float)cleanOrig[i].Z);
                }
                
                var facesToDelete = new List<int>();
                for (int i = 0; i < newTerrain.Faces.Count; i++)
                {
                    var f = newTerrain.Faces[i];
                    var pA = cleanOrig[f.A];
                    var pB = cleanOrig[f.B];
                    var pC = cleanOrig[f.C];
                    if (pA.DistanceTo(pB) > 150 || pB.DistanceTo(pC) > 150 || pC.DistanceTo(pA) > 150)
                        facesToDelete.Add(i);
                }
                newTerrain.Faces.DeleteFaces(facesToDelete);
                newTerrain.Faces.CullDegenerateFaces();
                newTerrain.Vertices.CullUnused();
                newTerrain.Compact();
                newTerrain.Weld(3.14159);
                newTerrain.Normals.ComputeNormals();
                if (newTerrain.IsValid) modTerrain = newTerrain;
                else modTerrain = terrain; 
            }
            else { modTerrain = terrain; }

            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, laneCurves);
            DA.SetDataList(3, railingCurves);
            DA.SetDataList(4, pillarsOut);
            DA.SetDataList(5, cutVols);
            DA.SetDataList(6, fillVols);
            
            stopwatch.Stop();
            Message = $"Road Generator\nTime: {stopwatch.ElapsedMilliseconds} ms\n---\nLanes: {totalLanes}\nWidth: {totalHalfWidth*2:F1}m\n---\nCut: {totalCutM3:N0} m3\nFill: {totalFillM3:N0} m3";
        }

        private class RoadData
        {
            public List<Point3d> leftPts = new List<Point3d>();
            public List<Point3d> rightPts = new List<Point3d>();
            public List<List<Point3d>> allLanes = new List<List<Point3d>>();
            public List<Point3d[]> roadProfiles = new List<Point3d[]>();
            public List<Point3d[]> terrProfiles = new List<Point3d[]>();
            public List<Tuple<Point3d, int>> extraPoints = new List<Tuple<Point3d, int>>();
            public List<Tuple<Point3d, int>> asphaltCenters = new List<Tuple<Point3d, int>>();
            public List<Tuple<Point3d, double>> daylightFootprints = new List<Tuple<Point3d, double>>();
            public List<LineCurve> pillars = new List<LineCurve>();
            public bool IsClosed = false;
        }

        private double TriVolume(Point3d t1, Point3d t2, Point3d t3, Point3d b1, Point3d b2, Point3d b3)
        {
            double area2D = 0.5 * Math.Abs(t1.X*(t2.Y - t3.Y) + t2.X*(t3.Y - t1.Y) + t3.X*(t1.Y - t2.Y));
            double avgDz = ((t1.Z - b1.Z) + (t2.Z - b2.Z) + (t3.Z - b3.Z)) / 3.0;
            if (avgDz < 0) avgDz = 0;
            return area2D * avgDz;
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("E5A7B8C9-1234-4ABC-9DEF-0123456789AB"); }
        }
    }
}
