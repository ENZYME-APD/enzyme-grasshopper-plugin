using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using Rhino.Geometry.Intersect;

namespace Enzyme.Terrain
{
    public class MeshHeightAnalysis : GH_Component
    {
        public MeshHeightAnalysis()
          : base("Mesh Terrain Analyzer", "Terrain",
              "Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels.",
              "Enzyme", "Terrain")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "M", "The meshes to analyze.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("SearchRings", "R", "Topological radius in rings.", GH_ParamAccess.item, 5);
            pManager.AddNumberParameter("ProminenceLimit", "P", "Minimum Z-delta to be considered a peak/valley.", GH_ParamAccess.item, 0.5);
            pManager.AddColourParameter("CustomColors", "C", "Custom colormap list.", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("CullGlobals", "CG", "Toggle to remove the absolute highest/lowest points.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("AvoidBoundaries", "AB", "Toggle to ignore naked edge vertices.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("EnableHeatmap", "EH", "Toggle to compute and output the vertex heatmap mesh.", GH_ParamAccess.item, true);
            pManager.AddPlaneParameter("RotationPlane", "RP", "Orientation plane for the bounding box sectioning.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("SectionsX", "SX", "Number of sections running parallel to the X-axis.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("SectionsY", "SY", "Number of sections running parallel to the Y-axis.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("LayoutFlat", "LF", "Toggle to generate 2D XY print layouts next to the mesh.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions", "I", "Component documentation and usage manual.", GH_ParamAccess.item);
            pManager.AddPointParameter("LocalPeaks", "LP", "Output points for local highs.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("PeakElevations", "PE", "Z-values for local highs.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMaxPoint", "GMP", "Absolute highest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMaxElevation", "GME", "Absolute highest Z-value.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LocalValleys", "LV", "Output points for local lows.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("ValleyElevations", "VE", "Z-values for local lows.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMinPoint", "GMI", "Absolute lowest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMinElevation", "GMIE", "Absolute lowest Z-value.", GH_ParamAccess.tree);
            pManager.AddMeshParameter("HeatmapMeshes", "HM", "The vertex-colored duplicate mesh.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("SectionOutlinesX", "SOX", "3D Polylines running parallel to the X-axis.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("SectionOutlinesY", "SOY", "3D Polylines running parallel to the Y-axis.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsX", "FSX", "2D X-Sections stacked downwards (-Y direction).", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsY", "FSY", "2D Y-Sections stacked leftwards (-X direction).", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelText3D", "LT3D", "Text strings for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPoints3D", "LP3D", "Points for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelTextFlat", "LTF", "Text strings for the flattened section layout.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPointsFlat", "LPF", "Points for the flattened section layout.", GH_ParamAccess.tree);
            pManager.AddTextParameter("SectionMetadata", "SM", "Dictionary keys containing spatial transform & ID data.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Mesh> targetMeshes = new GH_Structure<GH_Mesh>();
            if (!DA.GetDataTree(0, out targetMeshes)) return;

            int rings = 5;
            DA.GetData(1, ref rings);

            double prominence = 0.5;
            DA.GetData(2, ref prominence);

            List<Color> customColorList = new List<Color>();
            DA.GetDataList(3, customColorList);

            bool cullGlobals = false;
            DA.GetData(4, ref cullGlobals);

            bool avoidBounds = false;
            DA.GetData(5, ref avoidBounds);

            bool enableHeatmap = true;
            DA.GetData(6, ref enableHeatmap);

            Plane secPlane = Plane.WorldXY;
            DA.GetData(7, ref secPlane);

            int secCountX = 0;
            DA.GetData(8, ref secCountX);

            int secCountY = 0;
            DA.GetData(9, ref secCountY);

            bool layoutFlat = false;
            DA.GetData(10, ref layoutFlat);

            var localPeaks = new GH_Structure<GH_Point>();
            var peakElevations = new GH_Structure<GH_Number>();
            var globalMaxPoint = new GH_Structure<GH_Point>();
            var globalMaxElevation = new GH_Structure<GH_Number>();
            var localValleys = new GH_Structure<GH_Point>();
            var valleyElevations = new GH_Structure<GH_Number>();
            var globalMinPoint = new GH_Structure<GH_Point>();
            var globalMinElevation = new GH_Structure<GH_Number>();
            var heatmapMeshes = new GH_Structure<GH_Mesh>();
            var sectionOutlinesX = new GH_Structure<GH_Curve>();
            var sectionOutlinesY = new GH_Structure<GH_Curve>();
            var flatSectionsX = new GH_Structure<GH_Curve>();
            var flatSectionsY = new GH_Structure<GH_Curve>();
            var labelText3D = new GH_Structure<GH_String>();
            var labelPoints3D = new GH_Structure<GH_Point>();
            var labelTextFlat = new GH_Structure<GH_String>();
            var labelPointsFlat = new GH_Structure<GH_Point>();
            var sectionMetadata = new GH_Structure<GH_String>();

            int totalPeaksFound = 0;
            int totalValleysFound = 0;
            double globalTerrainZMin = double.MaxValue;
            double globalTerrainZMax = double.MinValue;
            double totalZSum = 0.0;
            int totalVerticesCount = 0;
            double totalTerrainArea = 0.0;
            int totalSectionsX = 0;
            int totalSectionsY = 0;

            BoundingBox globalBB = BoundingBox.Empty;
            foreach (var path in targetMeshes.Paths)
            {
                foreach (var obj in targetMeshes.get_Branch(path))
                {
                    var ghMesh = obj as GH_Mesh;
                    if (ghMesh != null && ghMesh.Value != null && ghMesh.Value.IsValid)
                    {
                        globalBB.Union(ghMesh.Value.GetBoundingBox(true));
                    }
                }
            }

            double padding = globalBB.IsValid ? globalBB.Diagonal.Length * 0.05 : 10.0;
            double cursorYXSecs = globalBB.IsValid ? globalBB.Min.Y - padding : -padding;
            double cursorXYSecs = globalBB.IsValid ? globalBB.Min.X - padding : -padding;

            for (int pathIdx = 0; pathIdx < targetMeshes.Paths.Count; pathIdx++)
            {
                var currentPath = targetMeshes.Paths[pathIdx];
                var branchMeshes = targetMeshes.get_Branch(currentPath);

                localPeaks.EnsurePath(currentPath);
                peakElevations.EnsurePath(currentPath);
                globalMaxPoint.EnsurePath(currentPath);
                globalMaxElevation.EnsurePath(currentPath);
                localValleys.EnsurePath(currentPath);
                valleyElevations.EnsurePath(currentPath);
                globalMinPoint.EnsurePath(currentPath);
                globalMinElevation.EnsurePath(currentPath);
                heatmapMeshes.EnsurePath(currentPath);
                sectionOutlinesX.EnsurePath(currentPath);
                sectionOutlinesY.EnsurePath(currentPath);
                flatSectionsX.EnsurePath(currentPath);
                flatSectionsY.EnsurePath(currentPath);
                labelText3D.EnsurePath(currentPath);
                labelPoints3D.EnsurePath(currentPath);
                labelTextFlat.EnsurePath(currentPath);
                labelPointsFlat.EnsurePath(currentPath);
                sectionMetadata.EnsurePath(currentPath);

                foreach (var obj in branchMeshes)
                {
                    var ghMesh = obj as GH_Mesh;
                    if (ghMesh == null || ghMesh.Value == null || !ghMesh.Value.IsValid) continue;
                    Mesh mesh = ghMesh.Value;
                    var topology = mesh.TopologyVertices;
                    var vertices = mesh.Vertices;
                    bool[] isNakedEdge = mesh.GetNakedEdgePointStatus();

                    var amp = AreaMassProperties.Compute(mesh);
                    if (amp != null) totalTerrainArea += amp.Area;

                    if (topology.Count == 0) continue;

                    List<double> zValues = new List<double>(topology.Count);
                    for (int i = 0; i < topology.Count; i++)
                    {
                        zValues.Add(topology[i].Z);
                    }

                    double zMin = zValues.Min();
                    double zMax = zValues.Max();

                    globalTerrainZMin = Math.Min(globalTerrainZMin, zMin);
                    globalTerrainZMax = Math.Max(globalTerrainZMax, zMax);
                    totalZSum += zValues.Sum();
                    totalVerticesCount += zValues.Count;

                    int globalMinIdx = zValues.IndexOf(zMin);
                    int globalMaxIdx = zValues.IndexOf(zMax);

                    var foundPeaks = new List<Tuple<int, double, Point3d>>();
                    var foundValleys = new List<Tuple<int, double, Point3d>>();

                    double bMinX = double.MaxValue, bMaxX = double.MinValue;
                    double bMinY = double.MaxValue, bMaxY = double.MinValue;

                    for (int vIdx = 0; vIdx < topology.Count; vIdx++)
                    {
                        Point3d pt = new Point3d(topology[vIdx]);

                        if (secCountX > 0 || secCountY > 0)
                        {
                            double u, v;
                            if (secPlane.ClosestParameter(pt, out u, out v))
                            {
                                if (u < bMinX) bMinX = u;
                                if (u > bMaxX) bMaxX = u;
                                if (v < bMinY) bMinY = v;
                                if (v > bMaxY) bMaxY = v;
                            }
                        }

                        if (avoidBounds && isNakedEdge[vIdx]) continue;

                        double currentZ = zValues[vIdx];
                        int[] immediateNeighbors = topology.ConnectedTopologyVertices(vIdx);

                        bool isLocalMax = true;
                        bool isLocalMin = true;

                        foreach (int nIdx in immediateNeighbors)
                        {
                            double nZ = zValues[nIdx];
                            if (nZ > currentZ + 0.0001) isLocalMax = false;
                            if (nZ < currentZ - 0.0001) isLocalMin = false;
                        }

                        if (!isLocalMax && !isLocalMin) continue;

                        var fullNeighbors = GetTopoNeighbors(topology, vIdx, rings);
                        if (fullNeighbors.Count == 0) continue;

                        double maxNeighborZ = double.MinValue;
                        double minNeighborZ = double.MaxValue;

                        foreach (int nIdx in fullNeighbors)
                        {
                            double nZ = zValues[nIdx];
                            if (nZ > maxNeighborZ) maxNeighborZ = nZ;
                            if (nZ < minNeighborZ) minNeighborZ = nZ;

                            if (isLocalMax && nZ > currentZ + 0.0001) isLocalMax = false;
                            if (isLocalMin && nZ < currentZ - 0.0001) isLocalMin = false;
                        }

                        if (isLocalMax && (currentZ - minNeighborZ) >= prominence)
                            foundPeaks.Add(new Tuple<int, double, Point3d>(vIdx, currentZ, pt));
                        else if (isLocalMin && (maxNeighborZ - currentZ) >= prominence)
                            foundValleys.Add(new Tuple<int, double, Point3d>(vIdx, currentZ, pt));
                    }

                    var peakIndices = new HashSet<int>(foundPeaks.Select(p => p.Item1));
                    var valleyIndices = new HashSet<int>(foundValleys.Select(v => v.Item1));

                    if (!avoidBounds || !isNakedEdge[globalMinIdx])
                    {
                        if (!valleyIndices.Contains(globalMinIdx))
                            foundValleys.Add(new Tuple<int, double, Point3d>(globalMinIdx, zMin, new Point3d(topology[globalMinIdx])));
                    }

                    if (!avoidBounds || !isNakedEdge[globalMaxIdx])
                    {
                        if (!peakIndices.Contains(globalMaxIdx))
                            foundPeaks.Add(new Tuple<int, double, Point3d>(globalMaxIdx, zMax, new Point3d(topology[globalMaxIdx])));
                    }

                    foundPeaks = foundPeaks.OrderByDescending(x => x.Item2).ToList();
                    foundValleys = foundValleys.OrderBy(x => x.Item2).ToList();

                    if (cullGlobals)
                    {
                        foundPeaks.RemoveAll(p => p.Item1 == globalMaxIdx);
                        foundValleys.RemoveAll(v => v.Item1 == globalMinIdx);
                    }

                    foreach (var data in foundPeaks)
                    {
                        localPeaks.Append(new GH_Point(data.Item3), currentPath);
                        peakElevations.Append(new GH_Number(Math.Round(data.Item2, 2)), currentPath);
                        totalPeaksFound++;
                    }

                    foreach (var data in foundValleys)
                    {
                        localValleys.Append(new GH_Point(data.Item3), currentPath);
                        valleyElevations.Append(new GH_Number(Math.Round(data.Item2, 2)), currentPath);
                        totalValleysFound++;
                    }

                    globalMaxPoint.Append(new GH_Point(new Point3d(topology[globalMaxIdx])), currentPath);
                    globalMaxElevation.Append(new GH_Number(Math.Round(zMax, 2)), currentPath);
                    globalMinPoint.Append(new GH_Point(new Point3d(topology[globalMinIdx])), currentPath);
                    globalMinElevation.Append(new GH_Number(Math.Round(zMin, 2)), currentPath);

                    if (enableHeatmap)
                    {
                        Mesh heatmapDup = mesh.DuplicateMesh();
                        heatmapDup.VertexColors.Clear();
                        for (int i = 0; i < vertices.Count; i++)
                        {
                            double zVal = heatmapDup.Vertices[i].Z;
                            Color c = ComputeHeatmapColor(zVal, zMin, zMax, customColorList);
                            heatmapDup.VertexColors.Add(c);
                        }
                        heatmapMeshes.Append(new GH_Mesh(heatmapDup), currentPath);
                    }

                    // X Sections
                    if (secCountX > 0 && (bMaxY - bMinY) > 1e-5)
                    {
                        List<double> yVals = new List<double>();
                        if (secCountX == 1) yVals.Add((bMinY + bMaxY) * 0.5);
                        else
                        {
                            for (int i = 0; i < secCountX; i++)
                                yVals.Add(bMinY + i * ((bMaxY - bMinY) / (secCountX - 1)));
                        }

                        for (int i = 0; i < yVals.Count; i++)
                        {
                            string secId = $"SecX_{i + 1:D2}";
                            Point3d origin = secPlane.PointAt(0, yVals[i], 0);
                            Plane cutPlaneXDir = new Plane(origin, secPlane.XAxis, secPlane.ZAxis);
                            Polyline[] polys = Intersection.MeshPlane(mesh, cutPlaneXDir);

                            if (polys != null && polys.Length > 0)
                            {
                                List<PolylineCurve> validCrvs = new List<PolylineCurve>();
                                foreach (var p in polys)
                                {
                                    if (p.IsValid && p.Count > 1)
                                    {
                                        var crv = new PolylineCurve(p);
                                        Vector3d vec = crv.PointAtEnd - crv.PointAtStart;
                                        if (vec * secPlane.XAxis < 0) crv.Reverse();
                                        validCrvs.Add(crv);
                                    }
                                }

                                if (validCrvs.Count > 0)
                                {
                                    validCrvs.Sort((c1, c2) =>
                                    {
                                        double u1, v1; secPlane.ClosestParameter(c1.PointAtStart, out u1, out v1);
                                        double u2, v2; secPlane.ClosestParameter(c2.PointAtStart, out u2, out v2);
                                        return u1.CompareTo(u2);
                                    });

                                    BoundingBox bbFlat = BoundingBox.Empty;
                                    List<Curve> flatCrvs = new List<Curve>();
                                    var xformToWorld = Transform.PlaneToPlane(cutPlaneXDir, Plane.WorldXY);

                                    foreach (var crv in validCrvs)
                                    {
                                        sectionOutlinesX.Append(new GH_Curve(crv), currentPath);
                                        totalSectionsX++;

                                        if (layoutFlat)
                                        {
                                            Curve flatCrv = crv.DuplicateCurve();
                                            flatCrv.Transform(xformToWorld);
                                            bbFlat.Union(flatCrv.GetBoundingBox(true));
                                            flatCrvs.Add(flatCrv);
                                        }
                                    }

                                    var firstCrv = validCrvs[0];
                                    var lastCrv = validCrvs[validCrvs.Count - 1];
                                    Point3d ptStart3D = firstCrv.PointAtStart - cutPlaneXDir.XAxis * 2.0;
                                    Point3d ptEnd3D = lastCrv.PointAtEnd + cutPlaneXDir.XAxis * 2.0;

                                    labelText3D.Append(new GH_String(secId), currentPath);
                                    labelText3D.Append(new GH_String(secId), currentPath);
                                    labelPoints3D.Append(new GH_Point(ptStart3D), currentPath);
                                    labelPoints3D.Append(new GH_Point(ptEnd3D), currentPath);

                                    if (layoutFlat)
                                    {
                                        var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorYXSecs - bbFlat.Max.Y, 0));
                                        foreach (var flatCrv in flatCrvs)
                                        {
                                            flatCrv.Transform(xformMove);
                                            flatSectionsX.Append(new GH_Curve(flatCrv), currentPath);
                                        }

                                        Point3d ptStartFlat = new Point3d(ptStart3D);
                                        Point3d ptEndFlat = new Point3d(ptEnd3D);
                                        ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove);
                                        ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove);

                                        labelTextFlat.Append(new GH_String(secId), currentPath);
                                        labelTextFlat.Append(new GH_String(secId), currentPath);
                                        labelPointsFlat.Append(new GH_Point(ptStartFlat), currentPath);
                                        labelPointsFlat.Append(new GH_Point(ptEndFlat), currentPath);

                                        string meta = $"{{\"id\": \"{secId}\", \"plane_origin\": \"{origin}\", \"direction\": \"X_Section\"}}";
                                        sectionMetadata.Append(new GH_String(meta), currentPath);

                                        cursorYXSecs -= ((bbFlat.Max.Y - bbFlat.Min.Y) + padding);
                                    }
                                }
                            }
                        }
                    }

                    // Y Sections
                    if (secCountY > 0 && (bMaxX - bMinX) > 1e-5)
                    {
                        Plane targetPlaneY = Plane.WorldXY;
                        targetPlaneY.Rotate(Math.PI / 2, Vector3d.ZAxis);

                        List<double> xVals = new List<double>();
                        if (secCountY == 1) xVals.Add((bMinX + bMaxX) * 0.5);
                        else
                        {
                            for (int i = 0; i < secCountY; i++)
                                xVals.Add(bMinX + i * ((bMaxX - bMinX) / (secCountY - 1)));
                        }

                        for (int i = 0; i < xVals.Count; i++)
                        {
                            string secId = $"SecY_{i + 1:D2}";
                            Point3d origin = secPlane.PointAt(xVals[i], 0, 0);
                            Plane cutPlaneYDir = new Plane(origin, secPlane.YAxis, secPlane.ZAxis);
                            Polyline[] polys = Intersection.MeshPlane(mesh, cutPlaneYDir);

                            if (polys != null && polys.Length > 0)
                            {
                                List<PolylineCurve> validCrvs = new List<PolylineCurve>();
                                foreach (var p in polys)
                                {
                                    if (p.IsValid && p.Count > 1)
                                    {
                                        var crv = new PolylineCurve(p);
                                        Vector3d vec = crv.PointAtEnd - crv.PointAtStart;
                                        if (vec * secPlane.YAxis < 0) crv.Reverse();
                                        validCrvs.Add(crv);
                                    }
                                }

                                if (validCrvs.Count > 0)
                                {
                                    validCrvs.Sort((c1, c2) =>
                                    {
                                        double u1, v1; secPlane.ClosestParameter(c1.PointAtStart, out u1, out v1);
                                        double u2, v2; secPlane.ClosestParameter(c2.PointAtStart, out u2, out v2);
                                        return v1.CompareTo(v2);
                                    });

                                    BoundingBox bbFlat = BoundingBox.Empty;
                                    List<Curve> flatCrvs = new List<Curve>();
                                    var xformToWorld = Transform.PlaneToPlane(cutPlaneYDir, targetPlaneY);

                                    foreach (var crv in validCrvs)
                                    {
                                        sectionOutlinesY.Append(new GH_Curve(crv), currentPath);
                                        totalSectionsY++;

                                        if (layoutFlat)
                                        {
                                            Curve flatCrv = crv.DuplicateCurve();
                                            flatCrv.Transform(xformToWorld);
                                            bbFlat.Union(flatCrv.GetBoundingBox(true));
                                            flatCrvs.Add(flatCrv);
                                        }
                                    }

                                    var firstCrv = validCrvs[0];
                                    var lastCrv = validCrvs[validCrvs.Count - 1];
                                    Point3d ptStart3D = firstCrv.PointAtStart - cutPlaneYDir.XAxis * 2.0;
                                    Point3d ptEnd3D = lastCrv.PointAtEnd + cutPlaneYDir.XAxis * 2.0;

                                    labelText3D.Append(new GH_String(secId), currentPath);
                                    labelText3D.Append(new GH_String(secId), currentPath);
                                    labelPoints3D.Append(new GH_Point(ptStart3D), currentPath);
                                    labelPoints3D.Append(new GH_Point(ptEnd3D), currentPath);

                                    if (layoutFlat)
                                    {
                                        var xformMove = Transform.Translation(new Vector3d(cursorXYSecs - bbFlat.Max.X, globalBB.Min.Y - bbFlat.Min.Y, 0));
                                        foreach (var flatCrv in flatCrvs)
                                        {
                                            flatCrv.Transform(xformMove);
                                            flatSectionsY.Append(new GH_Curve(flatCrv), currentPath);
                                        }

                                        Point3d ptStartFlat = new Point3d(ptStart3D);
                                        Point3d ptEndFlat = new Point3d(ptEnd3D);
                                        ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove);
                                        ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove);

                                        labelTextFlat.Append(new GH_String(secId), currentPath);
                                        labelTextFlat.Append(new GH_String(secId), currentPath);
                                        labelPointsFlat.Append(new GH_Point(ptStartFlat), currentPath);
                                        labelPointsFlat.Append(new GH_Point(ptEndFlat), currentPath);

                                        string meta = $"{{\"id\": \"{secId}\", \"plane_origin\": \"{origin}\", \"direction\": \"Y_Section\"}}";
                                        sectionMetadata.Append(new GH_String(meta), currentPath);

                                        cursorXYSecs -= ((bbFlat.Max.X - bbFlat.Min.X) + padding);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string instructions = "Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels.";
            DA.SetData(0, instructions);
            DA.SetDataTree(1, localPeaks);
            DA.SetDataTree(2, peakElevations);
            DA.SetDataTree(3, globalMaxPoint);
            DA.SetDataTree(4, globalMaxElevation);
            DA.SetDataTree(5, localValleys);
            DA.SetDataTree(6, valleyElevations);
            DA.SetDataTree(7, globalMinPoint);
            DA.SetDataTree(8, globalMinElevation);
            DA.SetDataTree(9, heatmapMeshes);
            DA.SetDataTree(10, sectionOutlinesX);
            DA.SetDataTree(11, sectionOutlinesY);
            DA.SetDataTree(12, flatSectionsX);
            DA.SetDataTree(13, flatSectionsY);
            DA.SetDataTree(14, labelText3D);
            DA.SetDataTree(15, labelPoints3D);
            DA.SetDataTree(16, labelTextFlat);
            DA.SetDataTree(17, labelPointsFlat);
            DA.SetDataTree(18, sectionMetadata);

            double terrainRelief = totalVerticesCount > 0 ? Math.Round(globalTerrainZMax - globalTerrainZMin, 2) : 0.0;
            double meanElevation = totalVerticesCount > 0 ? Math.Round(totalZSum / totalVerticesCount, 2) : 0.0;
            string layoutStatus = layoutFlat ? "ON (Bi-Directional Unroll)" : "OFF";
            
            Message = $"TERRAIN ANALYZER\n---\nArea: {Math.Round(totalTerrainArea, 2)}\nRelief (ΔZ): {terrainRelief}\nAvg Elev: {meanElevation}\n● Peaks: {totalPeaksFound} | ○ Valleys: {totalValleysFound}\n≡ Sections X: {totalSectionsX} | Y: {totalSectionsY}\n[] XY Layout: {layoutStatus}";
        }

        private HashSet<int> GetTopoNeighbors(MeshTopologyVertexList topology, int startIdx, int steps)
        {
            var visited = new HashSet<int> { startIdx };
            var currentLayer = new HashSet<int> { startIdx };

            for (int i = 0; i < steps; i++)
            {
                var nextLayer = new HashSet<int>();
                foreach (int idx in currentLayer)
                {
                    int[] neighbors = topology.ConnectedTopologyVertices(idx);
                    foreach (int nIdx in neighbors)
                    {
                        if (visited.Add(nIdx))
                        {
                            nextLayer.Add(nIdx);
                        }
                    }
                }
                if (nextLayer.Count == 0) break;
                currentLayer = nextLayer;
            }
            visited.Remove(startIdx);
            return visited;
        }

        private Color ComputeHeatmapColor(double val, double minVal, double maxVal, List<Color> colorList)
        {
            if (Math.Abs(maxVal - minVal) < 1e-9) return Color.Gray;
            double param = Math.Max(0.0, Math.Min(1.0, (val - minVal) / (maxVal - minVal)));

            if (colorList != null && colorList.Count > 1)
            {
                double idxF = param * (colorList.Count - 1);
                int idxLow = (int)idxF;
                int idxHigh = Math.Min(idxLow + 1, colorList.Count - 1);
                double t = idxF - idxLow;
                Color c1 = colorList[idxLow];
                Color c2 = colorList[idxHigh];
                return Color.FromArgb(
                    (int)(c1.R + (c2.R - c1.R) * t),
                    (int)(c1.G + (c2.G - c1.G) * t),
                    (int)(c1.B + (c2.B - c1.B) * t)
                );
            }

            if (param < 0.5) return Color.FromArgb((int)(param * 2 * 255), 255, (int)((1 - param * 2) * 255));
            return Color.FromArgb(255, (int)((1 - (param - 0.5) * 2) * 255), 0);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("MeshHeightAnalisys.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("8F1604B0-C27B-4966-9FC9-5DE911C3E20F"); }
        }
    }
}
