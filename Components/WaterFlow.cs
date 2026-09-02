using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Enzyme.Terrain
{
    public class WaterFlow : GH_Component
    {
        public WaterFlow()
          : base("Auto-Grid Raindrop Flow Engine", "WaterFlow",
              "Generates a parametric grid, projects it to the terrain, and simulates downhill flow paths.",
              Enzyme.Utils.TabInfo.TabName, "Terrain")
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 10.0, 5.0, 330, -40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 5.0, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 10.0, 10, 330, 40);
                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 0, System.Drawing.Color.DeepSkyBlue, 0.06, 300, -30);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "point", 300, 50);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TM", "The unified topological surface.", GH_ParamAccess.item);
            pManager.AddNumberParameter("GridSpacing", "GS", "The XY distance between starting raindrops.", GH_ParamAccess.item);
            pManager.AddNumberParameter("StepSize", "SS", "Distance the water travels per tick.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("MaxSteps", "MS", "Safety limit for the simulation loop.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("FlowPaths", "FP", "The generated water movement paths.", GH_ParamAccess.tree);
            pManager.AddPointParameter("DropPoints", "DP", "The valid starting grid points on the mesh.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Mesh terrainMesh = null;
            double gridSpacing = 0.0;
            double stepSize = 0.0;
            int maxSteps = 0;

            if (!DA.GetData(0, ref terrainMesh)) return;
            if (!DA.GetData(1, ref gridSpacing)) return;
            if (!DA.GetData(2, ref stepSize)) return;
            if (!DA.GetData(3, ref maxSteps)) return;

            terrainMesh.FaceNormals.ComputeFaceNormals();

            BoundingBox bbox = terrainMesh.GetBoundingBox(true);
            double minX = bbox.Min.X;
            double maxX = bbox.Max.X;
            double minY = bbox.Min.Y;
            double maxY = bbox.Max.Y;
            double maxZ = bbox.Max.Z + 1.0;

            List<Point3d> startGridPoints = new List<Point3d>();
            for (double currentX = minX; currentX <= maxX; currentX += gridSpacing)
            {
                for (double currentY = minY; currentY <= maxY; currentY += gridSpacing)
                {
                    startGridPoints.Add(new Point3d(currentX, currentY, maxZ));
                }
            }

            int gridPointsGenerated = startGridPoints.Count;
            Vector3d gravity = new Vector3d(0, 0, -1);
            Point3d[] projectedPts = Intersection.ProjectPointsToMeshes(new Mesh[] { terrainMesh }, startGridPoints, gravity, 0.001);

            double zTolerance = 0.001;
            DataTree<PolylineCurve> flowPaths = new DataTree<PolylineCurve>();
            DataTree<Point3d> dropPoints = new DataTree<Point3d>();
            int pathCount = 0;
            int stalledCount = 0;

            if (projectedPts != null)
            {
                for (int i = 0; i < projectedPts.Length; i++)
                {
                    Point3d pt = projectedPts[i];
                    GH_Path pathIndex = new GH_Path(i);

                    dropPoints.Add(pt, pathIndex);

                    List<Point3d> polylineVertices = new List<Point3d>();
                    MeshPoint meshPt = terrainMesh.ClosestMeshPoint(pt, 0.0);

                    if (meshPt == null)
                    {
                        stalledCount++;
                        continue;
                    }

                    Point3d currentLocation = meshPt.Point;
                    polylineVertices.Add(currentLocation);

                    for (int step = 0; step < maxSteps; step++)
                    {
                        Vector3f faceNormalf = terrainMesh.FaceNormals[meshPt.FaceIndex];
                        Vector3d faceNormal = new Vector3d(faceNormalf.X, faceNormalf.Y, faceNormalf.Z);

                        if (Math.Abs(faceNormal.Z) >= 0.9999)
                        {
                            break;
                        }

                        Vector3d strike = Vector3d.CrossProduct(faceNormal, gravity);
                        Vector3d downhill = Vector3d.CrossProduct(strike, faceNormal);

                        if (!downhill.Unitize())
                        {
                            break;
                        }

                        Point3d nextLocation = currentLocation + (downhill * stepSize);
                        MeshPoint nextMeshPt = terrainMesh.ClosestMeshPoint(nextLocation, 0.0);

                        if (nextMeshPt == null)
                        {
                            break;
                        }

                        Point3d projectedPoint = nextMeshPt.Point;

                        if (projectedPoint.Z >= currentLocation.Z - zTolerance)
                        {
                            break;
                        }

                        polylineVertices.Add(projectedPoint);
                        currentLocation = projectedPoint;
                        meshPt = nextMeshPt;
                    }

                    if (polylineVertices.Count > 1)
                    {
                        flowPaths.Add(new PolylineCurve(polylineVertices), pathIndex);
                        pathCount++;
                    }
                    else
                    {
                        stalledCount++;
                    }
                }
            }

            DA.SetDataTree(0, flowPaths);
            DA.SetDataTree(1, dropPoints);

            stopwatch.Stop();
            double durationMs = stopwatch.Elapsed.TotalMilliseconds;

            this.Message = $"{this.NickName}\nTime: {durationMs:F1} ms\n---\nGrid Seeds: {gridPointsGenerated}\nTotal Paths: {pathCount}\n● Active: {pathCount} | ○ Stalled: {stalledCount}";
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("WaterFlow.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("1a3d9061-0428-444f-b673-a4425bf5a1e2"); }
        }
    }
}
