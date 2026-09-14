using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class SunHoursAnalysis : GH_Component
    {
        public SunHoursAnalysis()
          : base("Sun Hours Analysis", "SunHours",
              "Lightning-fast multi-threaded raycaster to calculate solar exposure and sun hours.",
              "Enzyme", "Analysis")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Test Points", "Pts", "(Optional) Specific points to test (e.g. facade windows). Overrides Base Mesh.", GH_ParamAccess.list);
            pManager.AddMeshParameter("Base Mesh", "Base", "(Optional) Terrain or surface to automatically generate a grid on.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Grid Size", "Grid", "Size of the auto-generated grid on the Base Mesh", GH_ParamAccess.item, 2.0);
            pManager.AddMeshParameter("Context", "Context", "Environment geometry (buildings, terrain) as shadow casters", GH_ParamAccess.list);
            pManager.AddVectorParameter("Sun Vectors", "Vectors", "Solar vectors from the Heliodon (pointing TO the sun)", GH_ParamAccess.list);
            
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Sun Hits", "Hits", "Total number of sun rays that reached the point (Sun Hours)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Exposure %", "Exposure", "Percentage of time in the sun (0.0 to 1.0)", GH_ParamAccess.list);
            pManager.AddPointParameter("Points Out", "Pts", "Passthrough of the test points for easy gradient mapping", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> testPoints = new List<Point3d>();
            Mesh baseMesh = null;
            double gridSize = 2.0;
            List<Mesh> context = new List<Mesh>();
            List<Vector3d> vectors = new List<Vector3d>();

            DA.GetDataList(0, testPoints);
            DA.GetData(1, ref baseMesh);
            DA.GetData(2, ref gridSize);
            DA.GetDataList(3, context); // Optional
            if (!DA.GetDataList(4, vectors)) return;

            if (vectors.Count == 0) return;

            // Generate grid if no points were provided but a mesh was
            if ((testPoints == null || testPoints.Count == 0) && baseMesh != null && baseMesh.IsValid)
            {
                testPoints = new List<Point3d>();
                BoundingBox bbox = baseMesh.GetBoundingBox(true);
                double rayStartZ = bbox.Max.Z + 100.0;
                
                if (gridSize < 0.1) gridSize = 0.1;
                
                int cols = (int)Math.Ceiling((bbox.Max.X - bbox.Min.X) / gridSize);
                int rows = (int)Math.Ceiling((bbox.Max.Y - bbox.Min.Y) / gridSize);
                
                double startX = bbox.Min.X + (gridSize / 2.0);
                double startY = bbox.Min.Y + (gridSize / 2.0);
                
                for (int i = 0; i <= cols; i++)
                {
                    for (int j = 0; j <= rows; j++)
                    {
                        double x = startX + i * gridSize;
                        double y = startY + j * gridSize;
                        
                        Ray3d rayDown = new Ray3d(new Point3d(x, y, rayStartZ), new Vector3d(0, 0, -1));
                        double t = Rhino.Geometry.Intersect.Intersection.MeshRay(baseMesh, rayDown);
                        if (t >= 0.0)
                        {
                            testPoints.Add(rayDown.PointAt(t));
                        }
                    }
                }
            }

            if (testPoints == null || testPoints.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide either Test Points or a Base Mesh to generate points.");
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();

            Mesh[] contextMeshes = context.ToArray();
            int[] sunHits = new int[testPoints.Count];
            double[] exposures = new double[testPoints.Count];
            int totalRays = vectors.Count;

            // Multi-threaded raycasting
            Parallel.For(0, testPoints.Count, i =>
            {
                Point3d pt = testPoints[i];
                int hits = 0;

                foreach (Vector3d vec in vectors)
                {
                    // Shift the point slightly along the vector to prevent self-intersection 
                    // if the test point is exactly ON the context mesh.
                    Ray3d ray = new Ray3d(pt + (vec * 0.01), vec);
                    bool isShaded = false;

                    for (int m = 0; m < contextMeshes.Length; m++)
                    {
                        if (contextMeshes[m] == null) continue;
                        
                        // Check intersection. MeshRay returns the parameter along the ray, or negative if missed.
                        if (Rhino.Geometry.Intersect.Intersection.MeshRay(contextMeshes[m], ray) >= 0.0)
                        {
                            isShaded = true;
                            break; // Immediately stop checking other meshes for this ray
                        }
                    }

                    if (!isShaded)
                    {
                        hits++;
                    }
                }

                sunHits[i] = hits;
                exposures[i] = (double)hits / totalRays;
            });

            sw.Stop();

            DA.SetDataList(0, sunHits);
            DA.SetDataList(1, exposures);
            DA.SetDataList(2, testPoints);

            Message = $"Rays: {testPoints.Count * totalRays:N0}\\nTime: {sw.ElapsedMilliseconds} ms";
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("SunHoursAnalysis.png"); // Fallback

        public override Guid ComponentGuid => new Guid("C4F812D3-A42E-4D7F-8C9B-9A3E4F5D6C11");
    }
}
