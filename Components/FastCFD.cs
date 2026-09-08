using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class FastCFD : GH_Component
    {
        public FastCFD()
          : base("Fast CFD", "FastCFD",
              "A 2.5D Terrain-Following Grid Fluid Solver for fast urban wind analysis.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TM", "Input Terrain Mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("ContextMeshes", "CM", "Buildings and context as closed meshes", GH_ParamAccess.list);
            pManager.AddVectorParameter("WindVector", "WV", "Wind direction and speed (m/s)", GH_ParamAccess.item, new Vector3d(5, 5, 0));
            pManager.AddNumberParameter("CellSize", "CS", "Resolution of the grid in meters (e.g., 2.0). Smaller is more accurate but slower.", GH_ParamAccess.item, 4.0);
            pManager.AddIntegerParameter("Iterations", "I", "Simulation steps", GH_ParamAccess.item, 50);
            
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("WindMesh", "WM", "Heatmap of wind speeds mapped to the terrain", GH_ParamAccess.item);
            pManager.AddVectorParameter("WindVectors", "WV", "Wind velocity vectors for visualization", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "Pt", "Grid points corresponding to the vectors", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Simulation data and timing", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh terrainMesh = null;
            if (!DA.GetData(0, ref terrainMesh)) return;

            List<Mesh> contextMeshes = new List<Mesh>();
            DA.GetDataList(1, contextMeshes);

            Vector3d windVector = new Vector3d(5, 5, 0);
            if (!DA.GetData(2, ref windVector)) return;

            double cellSize = 4.0;
            if (!DA.GetData(3, ref cellSize)) return;
            if (cellSize < 0.5) cellSize = 0.5; // Safety limit

            int iterations = 50;
            if (!DA.GetData(4, ref iterations)) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. Establish Grid Bounding Box
            BoundingBox bbox = terrainMesh.GetBoundingBox(true);
            int cols = (int)Math.Ceiling((bbox.Max.X - bbox.Min.X) / cellSize);
            int rows = (int)Math.Ceiling((bbox.Max.Y - bbox.Min.Y) / cellSize);

            // Hard limit to prevent memory overflow during prototype phase
            if (cols * rows > 40000)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Grid is too dense ({cols}x{rows}). Increase CellSize.");
                return;
            }

            int N = cols;
            int M = rows;
            int totalCells = (N + 2) * (M + 2);

            // 2. Combine context meshes for fast raycasting
            Mesh combinedContext = new Mesh();
            foreach (var m in contextMeshes) { if (m != null) combinedContext.Append(m); }

            // 3. Build Draped Grid & Detect Obstacles
            Point3d[] gridPoints = new Point3d[totalCells];
            bool[] obstacles = new bool[totalCells];

            double startX = bbox.Min.X + cellSize / 2.0;
            double startY = bbox.Min.Y + cellSize / 2.0;

            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    double x = startX + (i - 1) * cellSize;
                    double y = startY + (j - 1) * cellSize;

                    // Raycast down to find terrain
                    Ray3d rayDown = new Ray3d(new Point3d(x, y, bbox.Max.Z + 100), new Vector3d(0, 0, -1));
                    double terrainT = Rhino.Geometry.Intersect.Intersection.MeshRay(terrainMesh, rayDown);
                    
                    if (terrainT >= 0.0)
                    {
                        Point3d terrainPt = rayDown.PointAt(terrainT);
                        Point3d drapedPt = new Point3d(terrainPt.X, terrainPt.Y, terrainPt.Z + 1.5);
                        gridPoints[idx] = drapedPt;

                        // Check obstacle: Does the ray hit a building higher than the draped point?
                        if (combinedContext.IsValid && combinedContext.Faces.Count > 0)
                        {
                            double contextT = Rhino.Geometry.Intersect.Intersection.MeshRay(combinedContext, rayDown);
                            if (contextT >= 0.0 && contextT < terrainT)
                            {
                                Point3d contextPt = rayDown.PointAt(contextT);
                                if (contextPt.Z > drapedPt.Z)
                                {
                                    obstacles[idx] = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        gridPoints[idx] = new Point3d(x, y, bbox.Min.Z);
                        obstacles[idx] = true; // Off terrain = boundary obstacle
                    }
                }
            }

            // 4. Stable Fluids Solver
            float[] u = new float[totalCells];
            float[] v = new float[totalCells];
            float[] u0 = new float[totalCells];
            float[] v0 = new float[totalCells];

            float dt = 0.1f;
            
            // Normalize initial wind injection
            float windU = (float)windVector.X;
            float windV = (float)windVector.Y;

            for (int iter = 0; iter < iterations; iter++)
            {
                // Inject wind at boundaries (if wind is coming from -X, inject at i=1)
                for (int j = 1; j <= M; j++)
                {
                    if (windU > 0) { u[IX(1, j, N)] = windU; v[IX(1, j, N)] = windV; obstacles[IX(1, j, N)] = false; }
                    if (windU < 0) { u[IX(N, j, N)] = windU; v[IX(N, j, N)] = windV; obstacles[IX(N, j, N)] = false; }
                }
                for (int i = 1; i <= N; i++)
                {
                    if (windV > 0) { u[IX(i, 1, N)] = windU; v[IX(i, 1, N)] = windV; obstacles[IX(i, 1, N)] = false; }
                    if (windV < 0) { u[IX(i, M, N)] = windU; v[IX(i, M, N)] = windV; obstacles[IX(i, M, N)] = false; }
                }

                // Core Fluid Steps (Advection and Projection)
                // Swap pointers
                float[] tmp = u; u = u0; u0 = tmp;
                tmp = v; v = v0; v0 = tmp;

                Advect(N, M, obstacles, u, u0, u0, v0, dt);
                Advect(N, M, obstacles, v, v0, u0, v0, dt);

                Project(N, M, obstacles, u, v, u0, v0);
            }

            // 5. Output Mapping
            Mesh outMesh = new Mesh();
            List<Vector3d> outVectors = new List<Vector3d>();
            List<Point3d> outPoints = new List<Point3d>();
            
            double maxSpeed = 0.01;
            double[] speeds = new double[totalCells];
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obstacles[idx]) continue;
                    
                    double speed = Math.Sqrt(u[idx] * u[idx] + v[idx] * v[idx]);
                    speeds[idx] = speed;
                    if (speed > maxSpeed) maxSpeed = speed;

                    outPoints.Add(gridPoints[idx]);
                    outVectors.Add(new Vector3d(u[idx], v[idx], 0));
                }
            }

            // Build Quad Mesh
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    outMesh.Vertices.Add(gridPoints[idx]);
                    
                    double speed = speeds[idx];
                    double normalized = Math.Min(speed / (windVector.Length * 1.5 + 0.1), 1.0); // Visual scale
                    
                    // Simple blue to red gradient
                    int r = (int)(normalized * 255);
                    int b = (int)((1.0 - normalized) * 255);
                    outMesh.VertexColors.Add(Color.FromArgb(255, r, 0, b));
                }
            }
            for (int i = 0; i < N - 1; i++)
            {
                for (int j = 0; j < M - 1; j++)
                {
                    int v00 = i + j * N;
                    int v10 = (i + 1) + j * N;
                    int v11 = (i + 1) + (j + 1) * N;
                    int v01 = i + (j + 1) * N;
                    outMesh.Faces.AddFace(v00, v10, v11, v01);
                }
            }

            DA.SetData(0, outMesh);
            DA.SetDataList(1, outVectors);
            DA.SetDataList(2, outPoints);

            sw.Stop();
            string info = $"FAST CFD 2.5D\nGrid: {cols} x {rows} ({totalCells} cells)\nTime: {sw.ElapsedMilliseconds} ms";
            DA.SetData(3, info);
        }

        // Fluid Dynamics Helpers
        private int IX(int i, int j, int N) { return i + (N + 2) * j; }

        private void Advect(int N, int M, bool[] obs, float[] d, float[] d0, float[] u, float[] v, float dt)
        {
            float dt0 = dt * N; // scaled for grid
            for (int i = 1; i <= N; i++) {
                for (int j = 1; j <= M; j++) {
                    if (obs[IX(i, j, N)]) continue;
                    float x = i - dt0 * u[IX(i, j, N)];
                    float y = j - dt0 * v[IX(i, j, N)];
                    if (x < 0.5f) x = 0.5f; if (x > N + 0.5f) x = N + 0.5f;
                    int i0 = (int)x; int i1 = i0 + 1;
                    if (y < 0.5f) y = 0.5f; if (y > M + 0.5f) y = M + 0.5f;
                    int j0 = (int)y; int j1 = j0 + 1;
                    float s1 = x - i0; float s0 = 1.0f - s1;
                    float t1 = y - j0; float t0 = 1.0f - t1;
                    d[IX(i, j, N)] = s0 * (t0 * d0[IX(i0, j0, N)] + t1 * d0[IX(i0, j1, N)]) +
                                     s1 * (t0 * d0[IX(i1, j0, N)] + t1 * d0[IX(i1, j1, N)]);
                }
            }
            SetBnd(N, M, obs, d);
        }

        private void Project(int N, int M, bool[] obs, float[] u, float[] v, float[] p, float[] div)
        {
            for (int i = 1; i <= N; i++) {
                for (int j = 1; j <= M; j++) {
                    if (obs[IX(i, j, N)]) continue;
                    div[IX(i, j, N)] = -0.5f * (u[IX(i + 1, j, N)] - u[IX(i - 1, j, N)] +
                                                v[IX(i, j + 1, N)] - v[IX(i, j - 1, N)]) / N;
                    p[IX(i, j, N)] = 0;
                }
            }
            SetBnd(N, M, obs, div);
            SetBnd(N, M, obs, p);

            for (int k = 0; k < 15; k++) {
                for (int i = 1; i <= N; i++) {
                    for (int j = 1; j <= M; j++) {
                        if (obs[IX(i, j, N)]) continue;
                        p[IX(i, j, N)] = (div[IX(i, j, N)] + p[IX(i - 1, j, N)] + p[IX(i + 1, j, N)] +
                                          p[IX(i, j - 1, N)] + p[IX(i, j + 1, N)]) / 4;
                    }
                }
                SetBnd(N, M, obs, p);
            }

            for (int i = 1; i <= N; i++) {
                for (int j = 1; j <= M; j++) {
                    if (obs[IX(i, j, N)]) continue;
                    u[IX(i, j, N)] -= 0.5f * N * (p[IX(i + 1, j, N)] - p[IX(i - 1, j, N)]);
                    v[IX(i, j, N)] -= 0.5f * N * (p[IX(i, j + 1, N)] - p[IX(i, j - 1, N)]);
                }
            }
            SetBnd(N, M, obs, u);
            SetBnd(N, M, obs, v);
        }

        private void SetBnd(int N, int M, bool[] obs, float[] x)
        {
            // Simple obstacle boundary enforcing
            for (int i = 1; i <= N; i++) {
                for (int j = 1; j <= M; j++) {
                    if (obs[IX(i, j, N)]) {
                        x[IX(i, j, N)] = 0.0f; // Velocity/Pressure is zero inside obstacles
                    }
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("11223344-5566-7788-9900-AABBCCDDEEFF");
    }
}
