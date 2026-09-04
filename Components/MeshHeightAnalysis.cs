using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
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

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 20.0, 5, 330, -140);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 0.5, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, false, 210, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 5, false, 210, -20);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, true, 210, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -341, 180, 22);
                Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 1, System.Drawing.Color.Blue, 10.0, 350, -285);
                Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 3, System.Drawing.Color.Blue, 5.0, 350, -240);
                Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 5, System.Drawing.Color.Red, 10.0, 350, -195);
                Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 7, System.Drawing.Color.Red, 5.0, 350, -150);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 9, "mesh", 220, -105);
            }
        }

        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

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
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            bool result = base.Read(reader);
            if (result)
            {
                // Force sync outputs 10 onwards to clean up legacy section outputs
                while (Params.Output.Count > 10)
                {
                    Params.UnregisterOutputParameter(Params.Output[Params.Output.Count - 1], true);
                }
                
                // Re-add the Color Legend parameter at index 10
                var legendParam = new Grasshopper.Kernel.Parameters.Param_GenericObject();
                legendParam.Name = "Color Legend";
                legendParam.NickName = "Color Legend";
                legendParam.Description = "JSON Legend Data";
                legendParam.Access = GH_ParamAccess.item;
                Params.RegisterOutputParam(legendParam);
            }
            return result;
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
            pManager.AddGenericParameter("Color Legend", "Color Legend", "JSON Legend Data", GH_ParamAccess.item);
                    pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var t_start = System.Diagnostics.Stopwatch.StartNew();
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
                                            DA.SetData(11, "MESH HEIGHT ANALYSIS\n" + "\n" + "HOW IT WORKS:\n" + "Analyzes mesh elevations to generate detailed HUD metrics (average, min, max heights) and identifies localized peaks and valleys.\n\n" + "INTERPRETATION & IMPORTANCE:\n" + "Provides quantitative tabular data summarizing the site's verticality. Knowing the highest peaks and lowest basins is critical for locating water towers, telecom equipment, or drainage ponds.");
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



                    for (int vIdx = 0; vIdx < topology.Count; vIdx++)
                    {
                        Point3d pt = new Point3d(topology[vIdx]);



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
                }
            }

            string instructions = "Analyzes mesh extremes and generates topo heatmaps.";
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

            if (enableHeatmap && totalVerticesCount > 0)
            {
                var jColors = new JArray();
                var cList = customColorList.Count > 0 ? customColorList : new List<Color> { Color.Blue, Color.Cyan, Color.Lime, Color.Yellow, Color.Red };
                foreach (var c in cList) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var legendObj = new JObject
                {
                    ["Type"] = "Blocks",
                    ["Title"] = "Mesh Terrain Elevation",
                    ["Colors"] = jColors,
                    ["Labels"] = new JArray($"{globalTerrainZMin:F1}m", $"{globalTerrainZMax:F1}m"),
                    ["SubLabels"] = new JArray($"Relief: {(globalTerrainZMax - globalTerrainZMin):F1}m")
                };
                DA.SetData(10, legendObj.ToString());
            }

            double terrainRelief = totalVerticesCount > 0 ? Math.Round(globalTerrainZMax - globalTerrainZMin, 2) : 0.0;
            double meanElevation = totalVerticesCount > 0 ? Math.Round(totalZSum / totalVerticesCount, 2) : 0.0;
            
            Message = "TERRAIN ANALYZER\n";
            Message += $"Time: {t_start.ElapsedMilliseconds:F2} ms\n";
            Message += "---\n";
            Message += $"Area: {Math.Round(totalTerrainArea, 2)}\n";
            Message += $"Relief (ΔZ): {terrainRelief}\n";
            Message += $"Avg Elev: {meanElevation}\n";
            Message += $"Max Height: {Math.Round(globalTerrainZMax, 2)}\n";
            Message += $"Min Height: {Math.Round(globalTerrainZMin, 2)}\n";
            Message += $"Peaks: {totalPeaksFound} | Valleys: {totalValleysFound}";
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
