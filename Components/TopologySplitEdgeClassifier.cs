using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class TopologySplitEdgeClassifier : GH_Component
    {
        public TopologySplitEdgeClassifier()
          : base("Topology Split Edge Classifier", "TSEC",
              "Clusters disjoint zones via R-Tree adjacency, splits boundaries at intersections, and classifies edge topology.",
              Enzyme.Utils.TabInfo.TabName, "Utilities")
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 2.0, 0.001, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -101, 180, 22);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "curve", 220, -45);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "curve", 220, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 3, 220, 34, 180, 22);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", 220, 90);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("RoomCurves", "RC", "The primary tree of input room boundary curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("ToleranceValue", "T", "The strict proximity tolerance to merge shared walls", GH_ParamAccess.item, 0.001);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions_Out", "IO", "Canvas setup instructions to enforce correct data binding", GH_ParamAccess.item);
            pManager.AddCurveParameter("ExternalEdges", "EE", "The flattened (Z=0) external boundary curves", GH_ParamAccess.tree);
            pManager.AddCurveParameter("InternalPartitions", "IP", "The flattened (Z=0) internal shared partition curves", GH_ParamAccess.tree);
            pManager.AddTextParameter("MetadataOut", "MO", "JSON serialized spatial parameters for orientation reconstruction", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FloorSlabs", "FS", "The flattened (Z=0) combined boolean union of the rooms", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            string instructionsOut = @"CANVAS INSTRUCTIONS:
1. Set Input 1 Name: 'RoomCurves' | Access: Tree | Type: Curve
2. Set Input 2 Name: 'ToleranceValue' | Access: Item | Type: double
3. Set Output 1 Name: 'Instructions_Out'
4. Set Output 2 Name: 'ExternalEdges'
5. Set Output 3 Name: 'InternalPartitions'
6. Set Output 4 Name: 'MetadataOut'
7. Set Output 5 Name: 'FloorSlabs'";

            DA.SetData(0, instructionsOut);

            GH_Structure<GH_Curve> roomCurvesTree;
            if (!DA.GetDataTree(0, out roomCurvesTree))
                roomCurvesTree = new GH_Structure<GH_Curve>();

            double toleranceValue = 0.001;
            DA.GetData(1, ref toleranceValue);

            var externalEdgesTree = new GH_Structure<GH_Curve>();
            var internalPartitionsTree = new GH_Structure<GH_Curve>();
            var metadataOutTree = new GH_Structure<GH_String>();
            var floorSlabsTree = new GH_Structure<GH_Curve>();

            int countZones = 0;
            int countClusters = 0;
            int countExternal = 0;
            int countInternal = 0;
            int countSlabs = 0;

            if (roomCurvesTree != null)
            {
                // Apply safe fallback values
                double tol = toleranceValue <= 0.0 ? 0.001 : toleranceValue;
                // Broad preliminary R-Tree search radius (min 0.1m)
                double rtreeRadius = tol > 0.05 ? tol * 2.0 : 0.1; 
                
                Transform xformFlat = Transform.PlanarProjection(Plane.WorldXY);

                for (int p = 0; p < roomCurvesTree.Branches.Count; p++)
                {
                    GH_Path path = roomCurvesTree.Paths[p];
                    List<GH_Curve> branchGHCurves = roomCurvesTree.Branches[p];
                    
                    List<Curve> validCurves = branchGHCurves
                        .Where(c => c != null && c.Value != null && c.Value.IsValid)
                        .Select(c => c.Value)
                        .ToList();
                    
                    if (validCurves.Count == 0)
                    {
                        externalEdgesTree.EnsurePath(path);
                        internalPartitionsTree.EnsurePath(path);
                        metadataOutTree.EnsurePath(path);
                        floorSlabsTree.EnsurePath(path);
                        continue;
                    }
                    
                    countZones += validCurves.Count;
                    int n = validCurves.Count;

                    // ======================================================================
                    // 5. ZONE CLUSTERING: R-TREE + UNION-FIND GRAPH
                    // ======================================================================
                    int[] parent = new int[n];
                    for (int i = 0; i < n; i++) parent[i] = i;
                    
                    // Local function for Union-Find Root
                    int Find(int node) 
                    {
                        if (parent[node] == node) return node;
                        parent[node] = Find(parent[node]);
                        return parent[node];
                    }
                    
                    // Local function to merge branches
                    void Union(int a, int b) 
                    {
                        int rootA = Find(a);
                        int rootB = Find(b);
                        if (rootA != rootB) parent[rootA] = rootB;
                    }

                    // Initialize RTree and 5-Point Proximity Envelopes
                    RTree rtree = new RTree();
                    BoundingBox[] bboxes = new BoundingBox[n];
                    List<Point3d>[] testPoints = new List<Point3d>[n];

                    for (int i = 0; i < n; i++)
                    {
                        bboxes[i] = validCurves[i].GetBoundingBox(true);
                        bboxes[i].Inflate(rtreeRadius); 
                        rtree.Insert(bboxes[i], i);
                        
                        testPoints[i] = new List<Point3d>();
                        Curve[] segs = validCurves[i].DuplicateSegments();
                        if (segs == null || segs.Length == 0) segs = new Curve[] { validCurves[i] };
                        
                        foreach (Curve s in segs)
                        {
                            for (int k = 0; k <= 4; k++)
                            {
                                double t = s.Domain.Min + (s.Domain.Max - s.Domain.Min) * (k / 4.0);
                                testPoints[i].Add(s.PointAt(t));
                            }
                        }
                    }

                    // Query RTree for Adjacency
                    for (int i = 0; i < n; i++)
                    {
                        int currentIdx = i;
                        rtree.Search(bboxes[i], (sender, args) => 
                        {
                            int j = args.Id;
                            if (j <= currentIdx) return; // Prevent duplicate checks
                            
                            Curve ci = validCurves[currentIdx];
                            Curve cj = validCurves[j];
                            
                            // Strict Intersect Check
                            var events = Rhino.Geometry.Intersect.Intersection.CurveCurve(ci, cj, tol, tol);
                            if (events != null && events.Count > 0)
                            {
                                Union(currentIdx, j);
                                return;
                            }
                            
                            // Strict 5-Point Envelope Check (current -> candidate)
                            bool touch = false;
                            foreach (Point3d pt in testPoints[currentIdx])
                            {
                                if (cj.ClosestPoint(pt, out double t, rtreeRadius) && pt.DistanceTo(cj.PointAt(t)) <= tol)
                                {
                                    touch = true; break;
                                }
                            }
                            if (touch) { Union(currentIdx, j); return; }
                            
                            // Strict 5-Point Envelope Check (candidate -> current)
                            foreach (Point3d pt in testPoints[j])
                            {
                                if (ci.ClosestPoint(pt, out double t, rtreeRadius) && pt.DistanceTo(ci.PointAt(t)) <= tol)
                                {
                                    touch = true; break;
                                }
                            }
                            if (touch) { Union(currentIdx, j); return; }
                        });
                    }

                    // Bundle mapped clusters
                    Dictionary<int, List<Curve>> clusters = new Dictionary<int, List<Curve>>();
                    for (int i = 0; i < n; i++)
                    {
                        int root = Find(i);
                        if (!clusters.ContainsKey(root)) clusters[root] = new List<Curve>();
                        clusters[root].Add(validCurves[i]);
                    }

                    // ======================================================================
                    // 6. TOPOLOGY PROCESSING PER ZONE CLUSTER
                    // ======================================================================
                    int cIdx = 0;
                    foreach (List<Curve> clusterCurves in clusters.Values)
                    {
                        countClusters++;
                        GH_Path clusterPath = path.AppendElement(cIdx++);
                        
                        externalEdgesTree.EnsurePath(clusterPath);
                        internalPartitionsTree.EnsurePath(clusterPath);
                        metadataOutTree.EnsurePath(clusterPath);
                        floorSlabsTree.EnsurePath(clusterPath);

                        // -- FLOOR SLAB EXTRACTION --
                        Curve[] unionedSlabs = Curve.CreateBooleanUnion(clusterCurves, tol);
                        if (unionedSlabs != null)
                        {
                            foreach (Curve slab in unionedSlabs)
                            {
                                Curve flatSlab = slab.DuplicateCurve();
                                flatSlab.Transform(xformFlat);
                                floorSlabsTree.Append(new GH_Curve(flatSlab), clusterPath);
                                countSlabs++;
                            }
                        }

                        // -- EXPLODE TO SEGMENTS --
                        List<Curve> initialSegments = new List<Curve>();
                        foreach (Curve crv in clusterCurves)
                        {
                            Curve[] segs = crv.DuplicateSegments();
                            if (segs == null || segs.Length == 0) initialSegments.Add(crv);
                            else initialSegments.AddRange(segs);
                        }

                        // -- INTERSECTION SPLITTING ALGORITHM --
                        List<double>[] splitParams = new List<double>[initialSegments.Count];
                        for (int k = 0; k < initialSegments.Count; k++)
                        {
                            splitParams[k] = new List<double>() { initialSegments[k].Domain.Min, initialSegments[k].Domain.Max };
                        }

                        for (int i = 0; i < initialSegments.Count; i++)
                        {
                            for (int j = i + 1; j < initialSegments.Count; j++)
                            {
                                var events = Rhino.Geometry.Intersect.Intersection.CurveCurve(initialSegments[i], initialSegments[j], tol, tol);
                                if (events != null)
                                {
                                    for (int e = 0; e < events.Count; e++)
                                    {
                                        if (events[e].IsPoint)
                                        {
                                            if (initialSegments[i].ClosestPoint(events[e].PointA, out double ti))
                                            {
                                                if (ti > initialSegments[i].Domain.Min + tol && ti < initialSegments[i].Domain.Max - tol)
                                                    splitParams[i].Add(ti);
                                            }
                                            if (initialSegments[j].ClosestPoint(events[e].PointB, out double tj))
                                            {
                                                if (tj > initialSegments[j].Domain.Min + tol && tj < initialSegments[j].Domain.Max - tol)
                                                    splitParams[j].Add(tj);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        List<Curve> refinedSegments = new List<Curve>();
                        for (int i = 0; i < initialSegments.Count; i++)
                        {
                            var parameters = splitParams[i].Distinct().ToList();
                            parameters.Sort();
                            for (int paramIdx = 0; paramIdx < parameters.Count - 1; paramIdx++)
                            {
                                Curve subCrv = initialSegments[i].Trim(parameters[paramIdx], parameters[paramIdx + 1]);
                                if (subCrv != null && subCrv.GetLength() > tol)
                                {
                                    refinedSegments.Add(subCrv);
                                }
                            }
                        }

                        // -- MIDPOINT PROXIMITY TOPOLOGY MATCHING --
                        HashSet<int> matchedIndices = new HashSet<int>();
                        for (int i = 0; i < refinedSegments.Count; i++)
                        {
                            if (matchedIndices.Contains(i)) continue;

                            Curve segI = refinedSegments[i];
                            double midTI = segI.Domain.Min + (segI.Domain.Max - segI.Domain.Min) * 0.5;
                            Point3d ptI = segI.PointAt(midTI);

                            bool matchFound = false;
                            for (int j = i + 1; j < refinedSegments.Count; j++)
                            {
                                if (matchedIndices.Contains(j)) continue;

                                Curve segJ = refinedSegments[j];
                                if (segJ.ClosestPoint(ptI, out double tVal) && ptI.DistanceTo(segJ.PointAt(tVal)) <= tol)
                                {
                                    // Internal Edge Logic
                                    Curve flatI = segI.DuplicateCurve(); flatI.Transform(xformFlat);
                                    string metaI = string.Format(CultureInfo.InvariantCulture, "{{\"origin\": {{\"x\": {0}, \"y\": {1}, \"z\": {2}}}, \"true_z\": {2}, \"type\": \"internal\"}}", ptI.X, ptI.Y, ptI.Z);

                                    double midTJ = segJ.Domain.Min + (segJ.Domain.Max - segJ.Domain.Min) * 0.5;
                                    Point3d ptJ = segJ.PointAt(midTJ);
                                    Curve flatJ = segJ.DuplicateCurve(); flatJ.Transform(xformFlat);
                                    string metaJ = string.Format(CultureInfo.InvariantCulture, "{{\"origin\": {{\"x\": {0}, \"y\": {1}, \"z\": {2}}}, \"true_z\": {2}, \"type\": \"internal\"}}", ptJ.X, ptJ.Y, ptJ.Z);

                                    internalPartitionsTree.Append(new GH_Curve(flatI), clusterPath); metadataOutTree.Append(new GH_String(metaI), clusterPath);
                                    internalPartitionsTree.Append(new GH_Curve(flatJ), clusterPath); metadataOutTree.Append(new GH_String(metaJ), clusterPath);

                                    matchedIndices.Add(i);
                                    matchedIndices.Add(j);
                                    matchFound = true;
                                    countInternal += 2;
                                    break;
                                }
                            }

                            if (!matchFound)
                            {
                                // External Edge Logic
                                Curve flatI = segI.DuplicateCurve(); flatI.Transform(xformFlat);
                                string metaI = string.Format(CultureInfo.InvariantCulture, "{{\"origin\": {{\"x\": {0}, \"y\": {1}, \"z\": {2}}}, \"true_z\": {2}, \"type\": \"external\"}}", ptI.X, ptI.Y, ptI.Z);

                                externalEdgesTree.Append(new GH_Curve(flatI), clusterPath);
                                metadataOutTree.Append(new GH_String(metaI), clusterPath);
                                countExternal++;
                            }
                        }
                    }
                }
            }

            DA.SetDataTree(1, externalEdgesTree);
            DA.SetDataTree(2, internalPartitionsTree);
            DA.SetDataTree(3, metadataOutTree);
            DA.SetDataTree(4, floorSlabsTree);

            sw.Stop();
            
            this.Message = $"TSEC\nTimer: {sw.ElapsedMilliseconds} ms\nZones: {countZones}\nClusters: {countClusters}\nSlabs: {countSlabs}\nInternal walls: {countInternal}\nExternal walls: {countExternal}";
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("TopologySplitEdgeClassifier.png");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public override Guid ComponentGuid
        {
            get { return new Guid("db487f5d-7521-432d-8b09-fb4d872b22bb"); }
        }
    }
}
