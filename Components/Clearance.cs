using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Geometry;
using Grasshopper.Kernel.Geometry.Delaunay;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class Clearance : GH_Component
    {
        public Clearance()
          : base("Masterplan Clearance Engine", "Clearance",
              "Calculates topological (Delaunay) or proximity-based clearances between tower outlines with dynamic categorization.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Outlines", "O", "Outlines", GH_ParamAccess.list);
            pManager.AddNumberParameter("SearchRadius", "SR", "Search Radius", GH_ParamAccess.item, 200.0);
            pManager.AddIntegerParameter("Method", "M", "Method (0 = Delaunay, 1 = Proximity)", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("L1", "L1", "Limit 1", GH_ParamAccess.item, 50.0);
            pManager.AddNumberParameter("L2", "L2", "Limit 2", GH_ParamAccess.item, 100.0);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Distances", "D", "Distances", GH_ParamAccess.tree);
            pManager.AddLineParameter("Lines", "L", "Lines", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("Categories", "C", "Categories", GH_ParamAccess.tree);
            pManager.AddPlaneParameter("LabelPlanes", "P", "Label Planes", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> outlines = new List<Curve>();
            if (!DA.GetDataList(0, outlines)) return;

            double searchRadius = 200.0;
            DA.GetData(1, ref searchRadius);

            int method = 0;
            DA.GetData(2, ref method);

            double l1 = 50.0;
            DA.GetData(3, ref l1);

            double l2 = 100.0;
            DA.GetData(4, ref l2);

            var startTime = DateTime.Now;

            GH_Structure<GH_Number> distTree = new GH_Structure<GH_Number>();
            GH_Structure<GH_Line> lineTree = new GH_Structure<GH_Line>();
            GH_Structure<GH_Integer> catTree = new GH_Structure<GH_Integer>();
            GH_Structure<GH_Plane> planeTree = new GH_Structure<GH_Plane>();

            List<Node2> nodesRaw = new List<Node2>();
            List<int> validIndices = new List<int>();
            List<Point3d> centroids3d = new List<Point3d>();

            for (int i = 0; i < outlines.Count; i++)
            {
                Curve crv = outlines[i];
                if (crv == null)
                {
                    centroids3d.Add(Point3d.Unset);
                    continue;
                }

                BoundingBox bbox = crv.GetBoundingBox(true);
                if (bbox.IsValid)
                {
                    Point3d center = bbox.Center;
                    nodesRaw.Add(new Node2(center.X, center.Y));
                    centroids3d.Add(center);
                    validIndices.Add(i);
                }
                else
                {
                    centroids3d.Add(Point3d.Unset);
                }
            }

            Action<int, Point3d, Point3d, double> addDataToTrees = (idxA, pA, pB, distance) =>
            {
                GH_Path path = new GH_Path(idxA);
                Line ln = new Line(pA, pB);
                
                lineTree.Append(new GH_Line(ln), path);
                distTree.Append(new GH_Number(distance), path);
                
                if (distance < l1) catTree.Append(new GH_Integer(0), path);
                else if (distance <= l2) catTree.Append(new GH_Integer(1), path);
                else catTree.Append(new GH_Integer(2), path);
                
                Point3d mid = ln.PointAt(0.5);
                Vector3d vec = ln.Direction;
                if (vec.Length > 0.001)
                {
                    double angle = Math.Atan2(vec.Y, vec.X);
                    if (angle > Math.PI / 2 || angle < -Math.PI / 2) angle += Math.PI;
                    Rhino.Geometry.Plane pln = Rhino.Geometry.Plane.WorldXY;
                    pln.Origin = mid;
                    pln.Rotate(angle, Vector3d.ZAxis, mid);
                    planeTree.Append(new GH_Plane(pln), path);
                }
            };

            string methodName = "";

            if (method == 0)
            {
                methodName = "Delaunay Method";
                if (nodesRaw.Count >= 3)
                {
                    Node2List nodesList = new Node2List();
                    foreach (var node in nodesRaw) nodesList.Append(node);
                    
                    List<Grasshopper.Kernel.Geometry.Delaunay.Face> dummyFaces = null;
                    Mesh delaunayMesh = Solver.Solve_Mesh(nodesList, 0.1, ref dummyFaces);
                    
                    if (delaunayMesh != null)
                    {
                        var topEdges = delaunayMesh.TopologyEdges;
                        for (int i = 0; i < topEdges.Count; i++)
                        {
                            var pair = topEdges.GetTopologyVertices(i);
                            int idxAValid = pair.I;
                            int idxBValid = pair.J;
                            
                            int idxA = validIndices[idxAValid];
                            int idxB = validIndices[idxBValid];

                            bool resCp = outlines[idxA].ClosestPoints(outlines[idxB], out Point3d cpA, out Point3d cpB);
                            if (resCp)
                            {
                                double d = cpA.DistanceTo(cpB);
                                if (d <= searchRadius)
                                {
                                    addDataToTrees(idxA, cpA, cpB, d);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                methodName = "Proximity Method";
                RTree rtree = new RTree();
                for (int i = 0; i < centroids3d.Count; i++)
                {
                    if (centroids3d[i] != Point3d.Unset)
                    {
                        rtree.Insert(centroids3d[i], i);
                    }
                }

                for (int i = 0; i < outlines.Count; i++)
                {
                    if (centroids3d[i] == Point3d.Unset) continue;
                    List<int> potentialIds = new List<int>();

                    BoundingBox bbox = outlines[i].GetBoundingBox(true);
                    bbox.Inflate(searchRadius);
                    rtree.Search(bbox, (sender, e) =>
                    {
                        if (e.Id > i) potentialIds.Add(e.Id);
                    });

                    foreach (int j in potentialIds)
                    {
                        Point3d mid = (centroids3d[i] + centroids3d[j]) / 2.0;
                        double limit = centroids3d[i].DistanceTo(centroids3d[j]) * 0.5;

                        bool isObscured = false;
                        for (int k = 0; k < outlines.Count; k++)
                        {
                            if (k == i || k == j || centroids3d[k] == Point3d.Unset) continue;
                            if (centroids3d[k].DistanceTo(mid) < (limit * 0.85))
                            {
                                isObscured = true;
                                break;
                            }
                        }

                        if (!isObscured)
                        {
                            bool resCp = outlines[i].ClosestPoints(outlines[j], out Point3d cpA, out Point3d cpB);
                            if (resCp)
                            {
                                double d = cpA.DistanceTo(cpB);
                                if (d <= searchRadius)
                                {
                                    addDataToTrees(i, cpA, cpB, d);
                                }
                            }
                        }
                    }
                }
            }

            var ms = (DateTime.Now - startTime).TotalMilliseconds;
            string dynamicLegend = $"0-{l1:0} / {l1:0}-{l2:0} / >{l2:0}";
            this.Message = $"{methodName}\n{dynamicLegend}\n{ms:0.1}ms";

            DA.SetDataTree(0, distTree);
            DA.SetDataTree(1, lineTree);
            DA.SetDataTree(2, catTree);
            DA.SetDataTree(3, planeTree);
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Clearance.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("1f6b8b08-3a9a-4c28-bb84-255d64acb33d"); }
        }
    }
}
