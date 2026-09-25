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

        private void AutoWireDefaults(GH_Document document)
        {
            Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 1, 2, 2, 316, -70);
            Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 1, 6, 2, 316, -50);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 4, 1.0, 10.0, 3.5, 316, -30);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 5, 0.0, 5.0, 1.5, 316, -10);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 6, 1.0, 20.0, 5.0, 316, 10);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 7, 5.0, 100.0, 20.0, 316, 30);
            Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 8, 10, 80, 45, 316, 50);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 9, 0.5, 10.0, 2.0, 316, 70);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 10, true, 316, 89);

            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", -145, -80);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "mesh", -145, -60);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "curve", -145, -40);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", -145, -20);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", -145, 0);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "mesh", -145, 20);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 6, "mesh", -145, 40);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 7, "curve", -145, 60);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 8, "curve", -145, 80);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                AutoWireDefaults(document);
            }
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Auto-Fill Defaults", (s, e) =>
            {
                if (OnPingDocument() != null)
                {
                    AutoWireDefaults(OnPingDocument());
                }
            });
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
            pManager.AddCurveParameter("Contours", "Ct", "Normal contours (1m)", GH_ParamAccess.list);
            pManager.AddCurveParameter("Main Contours", "MC", "Main contours (5m)", GH_ParamAccess.list);
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
                
                double[] tParams = nCrv.DivideByCount(divs, true); 
                if (tParams == null || tParams.Length < 2) tParams = nCrv.DivideByCount(divs, false);
                
                if (tParams == null || tParams.Length < 2) {
                    tParams = new double[divs + 1];
                    tParams[0] = nCrv.Domain.T0;
                    tParams[divs] = nCrv.Domain.T1;
                    for (int i = 1; i < divs; i++) {
                        if (nCrv.LengthParameter((length * i) / divs, out double t) && t > tParams[i-1]) {
                            tParams[i] = t;
                        } else {
                            tParams[i] = tParams[i-1] + 1e-5;
                        }
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

                    double zTerrain = pt.Z, zLeftT = left.Z, zRightT = right.Z;
                    bool onTerrain = false;
                    
                    if (terrain != null)
                    {
                        Ray3d rC = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tC = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rC);
                        if (tC >= 0.0) { zTerrain = rC.PointAt(tC).Z; onTerrain = true; }

                        Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                        if (tL >= 0.0) { zLeftT = rL.PointAt(tL).Z; onTerrain = true; }
                        else zLeftT = zTerrain;

                        Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                        if (tR >= 0.0) { zRightT = rR.PointAt(tR).Z; onTerrain = true; }
                        else zRightT = zTerrain;
                    }

                    // STRICT BOUNDARY FILTER: Only modify terrain if the road segment is actually over the original mesh!
                    if (onTerrain)
                    {
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
                            if (Math.Abs(zLeftT - left.Z) > 0.1) {
                                Vector3d dir = normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad) * (zLeftT > left.Z ? 1 : -1);
                                double hit = terrain != null ? Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir)) : -1;
                                if (hit >= 0) {
                                    Point3d hitPt = left + dir * hit;
                                    double dist2D = new Point3d(hitPt.X, hitPt.Y, 0).DistanceTo(new Point3d(left.X, left.Y, 0));
                                    leftBlend = new Point3d(left.X + normal.X * dist2D, left.Y + normal.Y * dist2D, hitPt.Z);
                                } else {
                                    leftBlend = new Point3d(left.X + normal.X * 2.0, left.Y + normal.Y * 2.0, zLeftT);
                                }
                            } else {
                                leftBlend = new Point3d(left.X + normal.X * 2.0, left.Y + normal.Y * 2.0, zLeftT);
                            }

                            Point3d rightBlend = right;
                            if (Math.Abs(zRightT - right.Z) > 0.1) {
                                Vector3d dir = -normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad) * (zRightT > right.Z ? 1 : -1);
                                double hit = terrain != null ? Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir)) : -1;
                                if (hit >= 0) {
                                    Point3d hitPt = right + dir * hit;
                                    double dist2D = new Point3d(hitPt.X, hitPt.Y, 0).DistanceTo(new Point3d(right.X, right.Y, 0));
                                    rightBlend = new Point3d(right.X - normal.X * dist2D, right.Y - normal.Y * dist2D, hitPt.Z);
                                } else {
                                    rightBlend = new Point3d(right.X - normal.X * 2.0, right.Y - normal.Y * 2.0, zRightT);
                                }
                            } else {
                                rightBlend = new Point3d(right.X - normal.X * 2.0, right.Y - normal.Y * 2.0, zRightT);
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

                VolumeBuilder cutBuilder = new VolumeBuilder();
                VolumeBuilder fillBuilder = new VolumeBuilder();
                
                for (int i = 0; i < rd.roadProfiles.Count - 1; i++)
                {
                    Point3d[] rp1 = rd.roadProfiles[i];
                    Point3d[] rp2 = rd.roadProfiles[i + 1];
                    Point3d[] tp1 = rd.terrProfiles[i];
                    Point3d[] tp2 = rd.terrProfiles[i + 1];

                    if (rp1[2].DistanceTo(rp2[2]) > subDist * 3.5) continue; 

                    for (int j = 0; j < 4; j++)
                    {
                        ProcessVolumeTriangle(rp1[j], rp1[j+1], rp2[j], tp1[j], tp1[j+1], tp2[j], cutBuilder, fillBuilder);
                        ProcessVolumeTriangle(rp1[j+1], rp2[j+1], rp2[j], tp1[j+1], tp2[j+1], tp2[j], cutBuilder, fillBuilder);
                    }
                }

                totalCutM3 += cutBuilder.TotalVolume;
                totalFillM3 += fillBuilder.TotalVolume;

                if (cutBuilder.Vertices.Count > 0) {
                    Mesh m = cutBuilder.ToMesh(colorize ? System.Drawing.Color.Red : System.Drawing.Color.Transparent);
                    if (!colorize) m.VertexColors.Clear();
                    if (m.IsValid && m.Faces.Count > 0) cutVols.Add(m);
                }
                if (fillBuilder.Vertices.Count > 0) {
                    Mesh m = fillBuilder.ToMesh(colorize ? System.Drawing.Color.Blue : System.Drawing.Color.Transparent);
                    if (!colorize) m.VertexColors.Clear();
                    if (m.IsValid && m.Faces.Count > 0) fillVols.Add(m);
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
            Message = $"Road Generator\nTime: {stopwatch.Elapsed.TotalMilliseconds:F2} ms\n---\nLanes: {totalLanes}\nWidth: {totalHalfWidth*2:F1}m\n---\nCut: {totalCutM3:N0} m3\nFill: {totalFillM3:N0} m3";
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

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("RoadGenerator.png");

        public override Guid ComponentGuid
        {
            get { return new Guid("E5A7B8C9-1234-4ABC-9DEF-0123456789AB"); }
        }
    
        private void ProcessVolumeTriangle(Point3d r0, Point3d r1, Point3d r2, Point3d t0, Point3d t1, Point3d t2, VolumeBuilder cutBuilder, VolumeBuilder fillBuilder)
        {
            var pts = new[] {
                new { R = r0, T = t0, D = r0.Z - t0.Z },
                new { R = r1, T = t1, D = r1.Z - t1.Z },
                new { R = r2, T = t2, D = r2.Z - t2.Z }
            };
            Array.Sort(pts, (a, b) => a.D.CompareTo(b.D));
            
            var A = pts[0];
            var B = pts[1];
            var C = pts[2];

            if (C.D <= 1e-4) {
                cutBuilder.AddPrism(A.R, B.R, C.R, A.T, B.T, C.T, true);
                return;
            }
            if (A.D >= -1e-4) {
                fillBuilder.AddPrism(A.R, B.R, C.R, A.T, B.T, C.T, false);
                return;
            }

            if (B.D <= 0) {
                double tAC = A.D / (A.D - C.D);
                double tBC = B.D / (B.D - C.D);
                Point3d zAC = A.R + (C.R - A.R) * tAC; zAC.Z = A.R.Z + (C.R.Z - A.R.Z) * tAC;
                Point3d zBC = B.R + (C.R - B.R) * tBC; zBC.Z = B.R.Z + (C.R.Z - B.R.Z) * tBC;

                cutBuilder.AddPrism(A.R, B.R, zBC, A.T, B.T, zBC, true);
                cutBuilder.AddPrism(A.R, zBC, zAC, A.T, zBC, zAC, true);
                fillBuilder.AddPrism(zAC, zBC, C.R, zAC, zBC, C.T, false);
            } else {
                double tAB = A.D / (A.D - B.D);
                double tAC = A.D / (A.D - C.D);
                Point3d zAB = A.R + (B.R - A.R) * tAB; zAB.Z = A.R.Z + (B.R.Z - A.R.Z) * tAB;
                Point3d zAC = A.R + (C.R - A.R) * tAC; zAC.Z = A.R.Z + (C.R.Z - A.R.Z) * tAC;

                cutBuilder.AddPrism(A.R, zAB, zAC, A.T, zAB, zAC, true);
                fillBuilder.AddPrism(zAB, B.R, C.R, zAB, B.T, C.T, false);
                fillBuilder.AddPrism(zAB, C.R, zAC, zAB, C.T, zAC, false);
            }
        }

        private class VolumeBuilder {
            public List<Point3d> Vertices = new List<Point3d>();
            public Dictionary<Point3d, int> VertDict = new Dictionary<Point3d, int>();
            public Dictionary<string, int[]> FaceCounts = new Dictionary<string, int[]>();
            public double TotalVolume = 0.0;

            public int GetOrAddVert(Point3d p) {
                Point3d key = new Point3d(Math.Round(p.X, 3), Math.Round(p.Y, 3), Math.Round(p.Z, 3));
                if (VertDict.TryGetValue(key, out int idx)) return idx;
                idx = Vertices.Count;
                Vertices.Add(p);
                VertDict[key] = idx;
                return idx;
            }

            public void AddFace(Point3d pA, Point3d pB, Point3d pC) {
                int a = GetOrAddVert(pA);
                int b = GetOrAddVert(pB);
                int c = GetOrAddVert(pC);
                if (a == b || b == c || c == a) return; 

                int[] arr = new int[] { a, b, c };
                Array.Sort(arr);
                string key = arr[0] + "_" + arr[1] + "_" + arr[2];
                
                if (FaceCounts.ContainsKey(key)) {
                    FaceCounts.Remove(key);
                } else {
                    FaceCounts[key] = new int[] { a, b, c };
                }
            }

            public void AddQuad(Point3d pA, Point3d pB, Point3d pC, Point3d pD) {
                AddFace(pA, pB, pC);
                AddFace(pA, pC, pD);
            }

            public void AddPrism(Point3d rA, Point3d rB, Point3d rC, Point3d tA, Point3d tB, Point3d tC, bool isCut) {
                double cross = (rB.X - rA.X) * (rC.Y - rA.Y) - (rB.Y - rA.Y) * (rC.X - rA.X);
                if (cross < 0) {
                    Point3d tmpR = rB; rB = rC; rC = tmpR;
                    Point3d tmpT = tB; tB = tC; tC = tmpT;
                    cross = -cross;
                }
                double area2D = 0.5 * cross;
                if (area2D < 1e-5) return;
                
                double avgDz = 0;
                if (isCut) {
                    avgDz = ((tA.Z - rA.Z) + (tB.Z - rB.Z) + (tC.Z - rC.Z)) / 3.0;
                } else {
                    avgDz = ((rA.Z - tA.Z) + (rB.Z - tB.Z) + (rC.Z - tC.Z)) / 3.0;
                }
                if (avgDz > 0) TotalVolume += area2D * avgDz;

                if (isCut) {
                    AddFace(tA, tB, tC);
                    AddFace(rA, rC, rB);
                    AddQuad(rA, rB, tB, tA);
                    AddQuad(rB, rC, tC, tB);
                    AddQuad(rC, rA, tA, tC);
                } else {
                    AddFace(rA, rB, rC);
                    AddFace(tA, tC, tB);
                    AddQuad(tA, tB, rB, rA);
                    AddQuad(tB, tC, rC, rB);
                    AddQuad(tC, tA, rA, rC);
                }
            }

            public Mesh ToMesh(System.Drawing.Color color) {
                Mesh m = new Mesh();
                foreach (var v in Vertices) m.Vertices.Add(v);
                foreach (var kvp in FaceCounts) {
                    m.Faces.AddFace(kvp.Value[0], kvp.Value[1], kvp.Value[2]);
                }
                m.Normals.ComputeNormals();
                if (m.SolidOrientation() == -1) m.Flip(true, true, true);
                for(int i=0; i<m.Vertices.Count; i++) m.VertexColors.Add(color);
                return m;
            }
        }

    }
}