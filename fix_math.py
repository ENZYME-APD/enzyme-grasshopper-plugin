import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# We will completely rewrite the SolveInstance method using regex to ensure we don't miss anything.
# Let's extract everything from "protected override void SolveInstance(IGH_DataAccess DA)" down to "public override Guid ComponentGuid"

start_idx = content.find("protected override void SolveInstance(IGH_DataAccess DA)")
end_idx = content.find("public override Guid ComponentGuid")

if start_idx == -1 or end_idx == -1:
    print("Error finding boundaries")
    exit(1)

new_solve = """protected override void SolveInstance(IGH_DataAccess DA)
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
            DA.GetData(10, ref colorize); // We removed fillet in previous step, so colorize is at 10. Wait, input index is 10.

            double totalLanes = dirs * lanes;
            double roadHalfWidth = (totalLanes * laneW) / 2.0;
            double totalHalfWidth = roadHalfWidth + shoulderW;
            double angleRad = angle * Math.PI / 180.0;
            if (angleRad < 0.01) angleRad = 0.01;
            if (angleRad > 1.5) angleRad = 1.5;

            List<Curve> laneCurves = new List<Curve>();
            List<Curve> pillars = new List<Curve>();
            List<Curve> railingCurves = new List<Curve>();
            List<Mesh> roadMeshes = new List<Mesh>();
            List<Mesh> cutVols = new List<Mesh>();
            List<Mesh> fillVols = new List<Mesh>();
            
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
                List<Point3d[]> roadProfiles = new List<Point3d[]>();
                List<Point3d[]> terrProfiles = new List<Point3d[]>();
                
                List<List<Point3d>> allLanes = new List<List<Point3d>>();
                for(int i = 0; i < totalLanes; i++) allLanes.Add(new List<Point3d>());

                double lastPillarDist = 0;

                for (int i = 0; i < tParams.Length; i++)
                {
                    // Closed curve seam seal
                    if (crv.IsClosed && i == tParams.Length - 1 && roadProfiles.Count > 0)
                    {
                        leftPts.Add(leftPts[0]);
                        rightPts.Add(rightPts[0]);
                        for(int j = 0; j < totalLanes; j++) allLanes[j].Add(allLanes[j][0]);
                        roadProfiles.Add(roadProfiles[0]);
                        terrProfiles.Add(terrProfiles[0]);
                        continue;
                    }

                    double t = tParams[i];
                    Point3d pt = crv.PointAt(t);
                    Vector3d tangent = crv.TangentAt(t);
                    tangent.Z = 0; 
                    tangent.Unitize();
                    Vector3d normal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                    normal.Unitize();

                    Point3d left = pt + normal * totalHalfWidth;
                    Point3d right = pt - normal * totalHalfWidth;
                    
                    leftPts.Add(left);
                    rightPts.Add(right);

                    double startOffset = -roadHalfWidth + (laneW / 2.0);
                    for(int j=0; j<totalLanes; j++)
                    {
                        double offset = startOffset + j * laneW;
                        allLanes[j].Add(pt - normal * offset); 
                    }

                    if (terrain != null)
                    {
                        double zTerrain = pt.Z, zLeftT = left.Z, zRightT = right.Z;
                        
                        Ray3d rC = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tC = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rC);
                        if (tC >= 0.0) zTerrain = rC.PointAt(tC).Z;

                        Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                        if (tL >= 0.0) zLeftT = rL.PointAt(tL).Z;

                        Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                        if (tR >= 0.0) zRightT = rR.PointAt(tR).Z;

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
                            roadProfiles.Add(new Point3d[] { left, left, pt, right, right });
                            terrProfiles.Add(new Point3d[] { left, left, pt, right, right });
                        }
                        else
                        {
                            // DAYLIGHTING CALCULATION (CUT/FILL)
                            Point3d leftBlend = left;
                            if (zLeftT > left.Z + 0.1) {
                                // Cut: Ray UP and OUT
                                Vector3d dir = normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad);
                                double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir));
                                leftBlend = hit >= 0 ? left + dir * hit : left + dir * 20.0;
                            } else if (zLeftT < left.Z - 0.1) {
                                // Fill: Ray DOWN and OUT
                                Vector3d dir = normal * Math.Cos(angleRad) - Vector3d.ZAxis * Math.Sin(angleRad);
                                double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir));
                                leftBlend = hit >= 0 ? left + dir * hit : left + dir * 20.0;
                            }

                            Point3d rightBlend = right;
                            if (zRightT > right.Z + 0.1) {
                                Vector3d dir = -normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad);
                                double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir));
                                rightBlend = hit >= 0 ? right + dir * hit : right + dir * 20.0;
                            } else if (zRightT < right.Z - 0.1) {
                                Vector3d dir = -normal * Math.Cos(angleRad) - Vector3d.ZAxis * Math.Sin(angleRad);
                                double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir));
                                rightBlend = hit >= 0 ? right + dir * hit : right + dir * 20.0;
                            }

                            extraPoints.Add(pt);
                            extraPoints.Add(left);
                            extraPoints.Add(right);
                            extraPoints.Add(leftBlend);
                            extraPoints.Add(rightBlend);

                            double exclL = new Point3d(leftBlend.X, leftBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                            double exclR = new Point3d(rightBlend.X, rightBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                            exclNodes.Add(new ExclusionNode { Pt2D = new Point3d(pt.X, pt.Y, 0), Radius = Math.Max(exclL, exclR) + 0.5 });

                            Point3d leftT = new Point3d(left.X, left.Y, zLeftT);
                            Point3d rightT = new Point3d(right.X, right.Y, zRightT);
                            Point3d ptT = new Point3d(pt.X, pt.Y, zTerrain);

                            roadProfiles.Add(new Point3d[] { leftBlend, left, pt, right, rightBlend });
                            terrProfiles.Add(new Point3d[] { leftBlend, leftT, ptT, rightT, rightBlend });
                        }
                    }
                }

                // Build Road Mesh
                Mesh roadMesh = new Mesh();
                for (int i = 0; i < leftPts.Count; i++) {
                    roadMesh.Vertices.Add(leftPts[i]);
                    roadMesh.Vertices.Add(rightPts[i]);
                }
                for (int i = 0; i < leftPts.Count - 1; i++) {
                    int v0 = i * 2, v1 = i * 2 + 1, v2 = (i + 1) * 2, v3 = (i + 1) * 2 + 1;
                    roadMesh.Faces.AddFace(v0, v1, v3, v2);
                }
                roadMesh.Normals.ComputeNormals();
                roadMeshes.Add(roadMesh);

                railingCurves.Add(new PolylineCurve(leftPts));
                railingCurves.Add(new PolylineCurve(rightPts));
                foreach(var lanePts in allLanes) laneCurves.Add(new PolylineCurve(lanePts));

                // Build Cut and Fill Solid Meshes (Closed Watertight Segments)
                Mesh cutMesh = new Mesh();
                Mesh fillMesh = new Mesh();
                
                for (int i = 0; i < roadProfiles.Count - 1; i++)
                {
                    Point3d[] rp1 = roadProfiles[i];
                    Point3d[] rp2 = roadProfiles[i + 1];
                    Point3d[] tp1 = terrProfiles[i];
                    Point3d[] tp2 = terrProfiles[i + 1];

                    // Simple Cut/Fill classification per segment based on centerline
                    bool isCut = rp1[2].Z < tp1[2].Z; 
                    Mesh target = isCut ? cutMesh : fillMesh;
                    System.Drawing.Color c = isCut ? System.Drawing.Color.Red : System.Drawing.Color.Blue;
                    
                    int bIdx = target.Vertices.Count;
                    // Top Surface (If Cut, Top is Terrain. If Fill, Top is Road)
                    Point3d[] top1 = isCut ? tp1 : rp1;
                    Point3d[] top2 = isCut ? tp2 : rp2;
                    // Bottom Surface
                    Point3d[] bot1 = isCut ? rp1 : tp1;
                    Point3d[] bot2 = isCut ? rp2 : tp2;

                    for(int j=0; j<5; j++) target.Vertices.Add(top1[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(top2[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(bot1[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(bot2[j]);

                    if (colorize) for (int j = 0; j < 20; j++) target.VertexColors.Add(c);

                    // Top Faces (Upwards normal)
                    for (int j = 0; j < 4; j++) target.Faces.AddFace(bIdx + j, bIdx + j + 1, bIdx + j + 6, bIdx + j + 5);
                    // Bottom Faces (Downwards normal)
                    int bOff = bIdx + 10;
                    for (int j = 0; j < 4; j++) target.Faces.AddFace(bOff + j, bOff + j + 5, bOff + j + 6, bOff + j + 1);
                    // Start Cap
                    for (int j = 0; j < 4; j++) target.Faces.AddFace(bIdx + j, bOff + j, bOff + j + 1, bIdx + j + 1);
                    // End Cap
                    for (int j = 0; j < 4; j++) target.Faces.AddFace(bIdx + j + 5, bIdx + j + 6, bOff + j + 6, bOff + j + 5);
                }

                if (cutMesh.Faces.Count > 0) { cutMesh.Normals.ComputeNormals(); cutVols.Add(cutMesh); }
                if (fillMesh.Faces.Count > 0) { fillMesh.Normals.ComputeNormals(); fillVols.Add(fillMesh); }
            }

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
                            if (op2D.DistanceTo(node.Pt2D) < node.Radius) { tooClose = true; break; }
                        }
                    }
                    if (!tooClose) pts.Add(op);
                }
                pts.AddRange(extraPoints);

                var nodes = new Node2List();
                var faces_placeholder = new List<Grasshopper.Kernel.Geometry.Delaunay.Face>();
                foreach (var p in pts) nodes.Append(new Node2(p.X, p.Y));
                Mesh newTerrain = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 1e-6, ref faces_placeholder);
                
                for (int i = 0; i < newTerrain.Vertices.Count; i++) {
                    newTerrain.Vertices[i] = new Rhino.Geometry.Point3f((float)pts[i].X, (float)pts[i].Y, (float)pts[i].Z);
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
            else { modTerrain = terrain; }

            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, laneCurves);
            DA.SetDataList(3, railingCurves);
            DA.SetDataList(4, pillars);
            DA.SetDataList(5, cutVols);
            DA.SetDataList(6, fillVols);
            
            stopwatch.Stop();
            Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m\\nTime: {stopwatch.ElapsedMilliseconds} ms";
        }
"""

content = content[:start_idx] + new_solve + content[end_idx:]

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
