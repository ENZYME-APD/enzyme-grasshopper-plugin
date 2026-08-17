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
          : base("Robust R-Tree Edge Topology", "RRET",
              "Uses R-Tree clustering and rigorous topological checks to bypass tolerance ambiguities and extract junction metadata.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("TestPoints", "TP", "The points to test against boundaries", GH_ParamAccess.tree);
            pManager.AddCurveParameter("BoundaryEdges", "BE", "The geometry boundaries or edges", GH_ParamAccess.list);
            pManager.AddNumberParameter("SearchRadius", "SR", "Broad R-Tree clustering radius", GH_ParamAccess.item, 0.1);
            pManager.AddNumberParameter("StrictTolerance", "ST", "Final topological distance check", GH_ParamAccess.item, 0.001);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions_Out", "IO", "Canvas setup instructions to enforce correct data binding", GH_ParamAccess.item);
            pManager.AddPointParameter("FilteredPoints", "FP", "Points that passed the topology check (flattened to Z=0)", GH_ParamAccess.tree);
            pManager.AddTextParameter("MetadataOut", "MO", "JSON serialized spatial parameters and curve index connections", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            string instructionsOut = @"CANVAS INSTRUCTIONS:
1. Set Input 1 Name: 'TestPoints' | Access: Tree | Type: Point3d
2. Set Input 2 Name: 'BoundaryEdges' | Access: List | Type: Curve
3. Set Input 3 Name: 'SearchRadius' | Access: Item | Type: double
4. Set Input 4 Name: 'StrictTolerance' | Access: Item | Type: double
5. Set Output 1 Name: 'Instructions_Out'
6. Set Output 2 Name: 'FilteredPoints'
7. Set Output 3 Name: 'MetadataOut'";

            DA.SetData(0, instructionsOut);

            GH_Structure<GH_Point> testPointsTree;
            if (!DA.GetDataTree(0, out testPointsTree))
                testPointsTree = new GH_Structure<GH_Point>();

            List<Curve> boundaryEdges = new List<Curve>();
            DA.GetDataList(1, boundaryEdges);

            double searchRadius = 0.1;
            DA.GetData(2, ref searchRadius);
            
            double strictTolerance = 0.001;
            DA.GetData(3, ref strictTolerance);

            var filteredPointsTree = new GH_Structure<GH_Point>();
            var metadataOutTree = new GH_Structure<GH_String>();
            
            int passedCount = 0;
            int failedCount = 0;

            if (testPointsTree != null && boundaryEdges != null && boundaryEdges.Count > 0)
            {
                double radius = searchRadius <= 0.0 ? 0.1 : searchRadius;
                double tol = strictTolerance <= 0.0 ? 0.001 : strictTolerance;

                RTree rtree = new RTree();
                for (int i = 0; i < boundaryEdges.Count; i++)
                {
                    if (boundaryEdges[i] != null && boundaryEdges[i].IsValid)
                    {
                        BoundingBox bbox = boundaryEdges[i].GetBoundingBox(true);
                        bbox.Inflate(radius);
                        rtree.Insert(bbox, i);
                    }
                }

                for (int p = 0; p < testPointsTree.Branches.Count; p++)
                {
                    GH_Path path = testPointsTree.Paths[p];
                    
                    filteredPointsTree.EnsurePath(path);
                    metadataOutTree.EnsurePath(path);
                    
                    List<GH_Point> branch = testPointsTree.Branches[p];
                    foreach (GH_Point ghPt in branch)
                    {
                        if (ghPt == null) continue;
                        Point3d pt = ghPt.Value;
                        List<int> connectedIndices = new List<int>();
                        
                        Sphere searchSphere = new Sphere(pt, radius);
                        
                        rtree.Search(searchSphere.BoundingBox, (sender, args) => {
                            double t;
                            if (boundaryEdges[args.Id].ClosestPoint(pt, out t, radius))
                            {
                                Point3d closestPt = boundaryEdges[args.Id].PointAt(t);
                                if (pt.DistanceTo(closestPt) <= tol)
                                {
                                    connectedIndices.Add(args.Id);
                                }
                            }
                        });
                        
                        if (connectedIndices.Count > 0)
                        {
                            Point3d flatPt = new Point3d(pt.X, pt.Y, 0.0);
                            filteredPointsTree.Append(new GH_Point(flatPt), path);
                            
                            string indicesJson = "[" + string.Join(",", connectedIndices) + "]";
                            string meta = string.Format(CultureInfo.InvariantCulture,
                                "{{\"origin\": {{\"x\": {0}, \"y\": {1}, \"z\": {2}}}, \"true_z\": {2}, \"connected_curves\": {3}}}",
                                pt.X, pt.Y, pt.Z, indicesJson);
                                
                            metadataOutTree.Append(new GH_String(meta), path);
                            
                            passedCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                }
            }

            DA.SetDataTree(1, filteredPointsTree);
            DA.SetDataTree(2, metadataOutTree);

            sw.Stop();
            this.Message = $"RRET\nTime: {sw.ElapsedMilliseconds} ms\n---\n● Passed: {passedCount} | ○ Failed: {failedCount}";
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("TopologySplitEdgeClassifier.png");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public override Guid ComponentGuid
        {
            get { return new Guid("db487f5d-7521-432d-8b09-fb4d872b22bb"); }
        }
    }
}
