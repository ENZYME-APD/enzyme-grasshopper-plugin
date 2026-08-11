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
              "Explodes and splits room boundaries at intersections, classifies edge topology, and generates unified floor slabs.",
              "Enzyme", "Topology")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("RoomCurves", "RC", "Room boundaries as curves", GH_ParamAccess.tree);
            pManager[0].Optional = true;
            pManager.AddNumberParameter("ToleranceValue", "T", "Tolerance value for intersections", GH_ParamAccess.item, 0.001);
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions_Out", "IO", "Canvas instructions", GH_ParamAccess.item);
            pManager.AddCurveParameter("ExternalEdges", "EE", "External boundary edges", GH_ParamAccess.tree);
            pManager.AddCurveParameter("InternalPartitions", "IP", "Internal partition edges", GH_ParamAccess.tree);
            pManager.AddTextParameter("MetadataOut", "MO", "Metadata for edges", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FloorSlabs", "FS", "Unified floor slabs", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string instructionsOut = @"CANVAS INSTRUCTIONS:
1. Set Input 1 Name: 'RoomCurves' | Access: Tree | Type: Curve
2. Set Input 2 Name: 'ToleranceValue' | Access: Item | Type: float
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

            int countExternal = 0;
            int countInternal = 0;
            int countSlabs = 0;

            if (roomCurvesTree != null && !roomCurvesTree.IsEmpty)
            {
                var xformFlat = Transform.PlanarProjection(Plane.WorldXY);

                for (int i = 0; i < roomCurvesTree.Branches.Count; i++)
                {
                    GH_Path path = roomCurvesTree.Paths[i];
                    List<GH_Curve> branchCurves = roomCurvesTree.Branches[i];
                    
                    externalEdgesTree.EnsurePath(path);
                    internalPartitionsTree.EnsurePath(path);
                    metadataOutTree.EnsurePath(path);
                    floorSlabsTree.EnsurePath(path);

                    var validCurves = branchCurves
                        .Where(c => c != null && c.Value != null && c.Value.IsValid)
                        .Select(c => c.Value)
                        .ToList();

                    if (validCurves.Count == 0)
                        continue;

                    var unionedSlabs = Curve.CreateBooleanUnion(validCurves, toleranceValue);
                    if (unionedSlabs != null)
                    {
                        foreach (var slab in unionedSlabs)
                        {
                            var flatSlab = slab.DuplicateCurve();
                            flatSlab.Transform(xformFlat);
                            floorSlabsTree.Append(new GH_Curve(flatSlab), path);
                            countSlabs++;
                        }
                    }

                    List<Curve> initialSegments = new List<Curve>();
                    foreach (var crv in validCurves)
                    {
                        var segments = crv.DuplicateSegments();
                        if (segments == null || segments.Length == 0)
                            initialSegments.Add(crv);
                        else
                            initialSegments.AddRange(segments);
                    }

                    var splitParameters = new Dictionary<int, List<double>>();
                    for (int idx = 0; idx < initialSegments.Count; idx++)
                    {
                        splitParameters[idx] = new List<double> { initialSegments[idx].Domain.Min, initialSegments[idx].Domain.Max };
                    }

                    for (int idx = 0; idx < initialSegments.Count; idx++)
                    {
                        Curve segI = initialSegments[idx];
                        for (int jdx = idx + 1; jdx < initialSegments.Count; jdx++)
                        {
                            Curve segJ = initialSegments[jdx];

                            var events = Rhino.Geometry.Intersect.Intersection.CurveCurve(segI, segJ, toleranceValue, toleranceValue);
                            if (events != null && events.Count > 0)
                            {
                                foreach (var ev in events)
                                {
                                    if (ev.IsPoint)
                                    {
                                        double tI, tJ;
                                        bool successI = segI.ClosestPoint(ev.PointA, out tI);
                                        bool successJ = segJ.ClosestPoint(ev.PointB, out tJ);

                                        if (successI)
                                        {
                                            if (tI > segI.Domain.Min + toleranceValue && tI < segI.Domain.Max - toleranceValue)
                                                splitParameters[idx].Add(tI);
                                        }
                                        if (successJ)
                                        {
                                            if (tJ > segJ.Domain.Min + toleranceValue && tJ < segJ.Domain.Max - toleranceValue)
                                                splitParameters[jdx].Add(tJ);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    List<Curve> refinedSegments = new List<Curve>();
                    for (int idx = 0; idx < initialSegments.Count; idx++)
                    {
                        var seg = initialSegments[idx];
                        var paramsList = splitParameters[idx].Distinct().OrderBy(p => p).ToList();
                        
                        for (int pIdx = 0; pIdx < paramsList.Count - 1; pIdx++)
                        {
                            var subCrv = seg.Trim(paramsList[pIdx], paramsList[pIdx + 1]);
                            if (subCrv != null && subCrv.GetLength() > toleranceValue)
                            {
                                refinedSegments.Add(subCrv);
                            }
                        }
                    }

                    HashSet<int> matchedIndices = new HashSet<int>();

                    for (int idx = 0; idx < refinedSegments.Count; idx++)
                    {
                        if (matchedIndices.Contains(idx)) continue;

                        Curve segI = refinedSegments[idx];
                        double midTI = segI.Domain.Min + (segI.Domain.Max - segI.Domain.Min) * 0.5;
                        Point3d ptI = segI.PointAt(midTI);

                        bool matchFound = false;
                        for (int jdx = idx + 1; jdx < refinedSegments.Count; jdx++)
                        {
                            if (matchedIndices.Contains(jdx)) continue;

                            Curve segJ = refinedSegments[jdx];
                            double tVal;
                            bool success = segJ.ClosestPoint(ptI, out tVal);

                            if (success)
                            {
                                Point3d closestPt = segJ.PointAt(tVal);
                                if (ptI.DistanceTo(closestPt) <= toleranceValue)
                                {
                                    var flatSegI = segI.DuplicateCurve();
                                    flatSegI.Transform(xformFlat);

                                    string metaI = string.Format(CultureInfo.InvariantCulture,
                                        "{{\"origin\": {{\"x\": {0}, \"y\": {1}, \"z\": {2}}}, \"true_z\": {2}, \"type\": \"internal\"}}",
                                        ptI.X, ptI.Y, ptI.Z);

                                    double midTJ = segJ.Domain.Min + (segJ.Domain.Max - segJ.Domain.Min) * 0.5;
                                    Point3d ptJ = segJ.PointAt(midTJ);

                                    var flatSegJ = segJ.DuplicateCurve();
                                    flatSegJ.Transform(xformFlat);

                                    string metaJ = string.Format(CultureInfo.InvariantCulture,
                                        "{{\"origin\": {{\"x\": {0}, \"y\": {1}, \"z\": {2}}}, \"true_z\": {2}, \"type\": \"internal\"}}",
                                        ptJ.X, ptJ.Y, ptJ.Z);

                                    internalPartitionsTree.Append(new GH_Curve(flatSegI), path);
                                    metadataOutTree.Append(new GH_String(metaI), path);
                                    internalPartitionsTree.Append(new GH_Curve(flatSegJ), path);
                                    metadataOutTree.Append(new GH_String(metaJ), path);

                                    matchedIndices.Add(idx);
                                    matchedIndices.Add(jdx);
                                    matchFound = true;
                                    countInternal += 2;
                                    break;
                                }
                            }
                        }

                        if (!matchFound)
                        {
                            var flatSegI = segI.DuplicateCurve();
                            flatSegI.Transform(xformFlat);

                            string metaI = string.Format(CultureInfo.InvariantCulture,
                                "{{\"origin\": {{\"x\": {0}, \"y\": {1}, \"z\": {2}}}, \"true_z\": {2}, \"type\": \"external\"}}",
                                ptI.X, ptI.Y, ptI.Z);

                            externalEdgesTree.Append(new GH_Curve(flatSegI), path);
                            metadataOutTree.Append(new GH_String(metaI), path);
                            countExternal++;
                        }
                    }
                }
            }

            DA.SetDataTree(1, externalEdgesTree);
            DA.SetDataTree(2, internalPartitionsTree);
            DA.SetDataTree(3, metadataOutTree);
            DA.SetDataTree(4, floorSlabsTree);

            stopwatch.Stop();
            double durationMs = stopwatch.Elapsed.TotalMilliseconds;

            this.Message = string.Format(CultureInfo.InvariantCulture,
                "TSEC\nTime: {0:F2} ms\n---\n● Int: {1} | ○ Ext: {2}\n■ Slabs: {3}",
                durationMs, countInternal, countExternal, countSlabs);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("TopologySplitEdgeClassifier.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("db487f5d-7521-432d-8b09-fb4d872b22bb"); }
        }
    }
}
