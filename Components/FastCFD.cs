using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class FastCFD : EnzymeComponent
    {
        public FastCFD()
          : base("Fast CFD", "FastCFD",
              "A multi-threaded 2.5D Terrain-Following Grid Fluid Solver for fast urban wind and pedestrian comfort analysis.",
              "Enzyme", "Site Analysis")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TerrainMesh", "Input Terrain Mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("ContextMeshes", "ContextMeshes", "Buildings and context as closed meshes", GH_ParamAccess.list);
            pManager.AddVectorParameter("WindVector", "WindVector", "Prevailing wind direction vector", GH_ParamAccess.item, new Vector3d(1, 1, 0));
            pManager.AddNumberParameter("WindSpeed", "WindSpeed", "Baseline wind velocity (m/s) at reference height (default 10m meteorological wind)", GH_ParamAccess.item, 10.0);
            pManager.AddNumberParameter("CellSize", "CellSize", "Resolution of the grid in meters.", GH_ParamAccess.item, 4.0);
            pManager.AddIntegerParameter("Iterations", "Iterations", "Simulation steps (time steps). Automatically adapts to domain scale if lower than minimum flush.", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("AnalysisHeight", "AnalysisHeight", "Drape offset above terrain for pedestrian evaluation (m)", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("Viscosity", "Viscosity", "Kinematic viscosity (diffusion/turbulence in m²/s, default 0.1)", GH_ParamAccess.item, 0.1);
            pManager.AddNumberParameter("Friction", "Friction", "Surface drag coefficient (0.0 to 1.0). Accepts item or list.", GH_ParamAccess.list, 0.1);
            pManager.AddNumberParameter("ComfortThreshold", "ComfortThreshold", "User threshold for pedestrian comfort (m/s)", GH_ParamAccess.item, 5.0);
            pManager.AddCurveParameter("BoundaryMask", "BoundaryMask", "Optional closed curve to crop the simulation domain and lock grid coordinates.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "Colors", "Custom color spectrum override (min to max)", GH_ParamAccess.list);
            pManager.AddNumberParameter("WindRefHeight", "RefHeight", "Meteorological measurement height for input WindSpeed (m). Default = 10.0m (scales to pedestrian height via ABL power law).", GH_ParamAccess.item, 10.0);
            
            pManager[1].Optional = true;
            pManager[8].Optional = true;
            pManager[10].Optional = true;
            pManager[11].Optional = true;
            pManager[12].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("WindMesh", "WindMesh", "Heatmap of wind speeds mapped to the terrain", GH_ParamAccess.item);
            pManager.AddPointParameter("Points", "Points", "Grid points corresponding to the vectors", GH_ParamAccess.list);
            pManager.AddVectorParameter("WindVectors", "WindVectors", "Wind velocity vectors for visualization (m/s)", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "The color assigned to each point/vector", GH_ParamAccess.list);
            pManager.AddNumberParameter("Speeds", "Speeds", "Wind speed magnitude (m/s) at each point", GH_ParamAccess.list);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON string for Dashboard and Legend", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Simulation data, Lawson comfort criteria, and timing", GH_ParamAccess.item);
        }

        protected override void Solve(IGH_DataAccess DA, System.Diagnostics.Stopwatch hudSw)
        {
            Mesh terrainMesh = null;
            if (!DA.GetData(0, ref terrainMesh)) return;

            List<Mesh> contextMeshes = new List<Mesh>();
            DA.GetDataList(1, contextMeshes);

            Vector3d windDir = new Vector3d(1, 1, 0);
            if (!DA.GetData(2, ref windDir)) return;

            double windSpeed = 10.0;
            if (!DA.GetData(3, ref windSpeed)) return;

            double cellSize = 4.0;
            if (!DA.GetData(4, ref cellSize)) return;
            if (cellSize < 0.5) cellSize = 0.5;

            int iterations = 50;
            if (!DA.GetData(5, ref iterations)) return;
            if (iterations < 5) iterations = 5;

            double analysisHeight = 1.5;
            if (!DA.GetData(6, ref analysisHeight)) return;
            if (analysisHeight < 0.1) analysisHeight = 0.1;

            double viscosity = 0.1;
            if (!DA.GetData(7, ref viscosity)) return;
            if (viscosity < 0.0) viscosity = 0.0;

            List<double> frictionList = new List<double>();
            DA.GetDataList(8, frictionList);

            double comfortThreshold = 5.0;
            if (!DA.GetData(9, ref comfortThreshold)) return;

            Curve maskCurve = null;
            DA.GetData(10, ref maskCurve);

            List<Color> customColors = new List<Color>();
            DA.GetDataList(11, customColors);

            double refHeight = 10.0;
            DA.GetData(12, ref refHeight);
            if (refHeight < 1.0) refHeight = 1.0;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Atmospheric Boundary Layer (ABL) height scaling from weather station to pedestrian level
            double heightScale = (analysisHeight < refHeight)
                ? Math.Pow(analysisHeight / refHeight, 0.25)
                : 1.0;
            double pedestrianSpeed = windSpeed * heightScale;

            if (!windDir.Unitize()) windDir = new Vector3d(1, 0, 0);
            Vector3d windVector = windDir * pedestrianSpeed;

            BoundingBox bbox;
            if (maskCurve != null && maskCurve.IsValid) {
                bbox = maskCurve.GetBoundingBox(true);
            } else {
                bbox = terrainMesh.GetBoundingBox(true);
            }

            int cols = (int)Math.Ceiling((bbox.Max.X - bbox.Min.X) / cellSize);
            int rows = (int)Math.Ceiling((bbox.Max.Y - bbox.Min.Y) / cellSize);

            if (cols * rows > 100000)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Grid is too dense ({cols}x{rows}). Increase CellSize or use a smaller BoundaryMask.");
                return;
            }

            int N = cols;
            int M = rows;
            int totalCells = (N + 2) * (M + 2);

            // Pre-filter valid context meshes & cache bounding boxes once
            List<Mesh> validContextMeshes = new List<Mesh>();
            List<BoundingBox> contextBBoxes = new List<BoundingBox>();
            double maxContextZ = bbox.Max.Z;

            if (contextMeshes != null && contextMeshes.Count > 0)
            {
                foreach (var m in contextMeshes)
                {
                    if (m != null && m.IsValid && m.Faces.Count > 0)
                    {
                        validContextMeshes.Add(m);
                        BoundingBox b = m.GetBoundingBox(false);
                        contextBBoxes.Add(b);
                        if (b.Max.Z > maxContextZ) maxContextZ = b.Max.Z;
                    }
                }
            }

            double rayStartZ = maxContextZ + 100.0;

            Point3d[] gridPoints = new Point3d[totalCells];
            bool[] obstacles = new bool[totalCells];
            float[] frictionMap = new float[totalCells];
            bool[] insideMask = new bool[totalCells];
            float[] terrainSlopeFactor = new float[totalCells];

            double startX = bbox.Min.X + cellSize / 2.0;
            double startY = bbox.Min.Y + cellSize / 2.0;

            // Multi-threaded grid initialization with cached context lookups and terrain slope analysis
            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    double x = startX + (i - 1) * cellSize;
                    double y = startY + (j - 1) * cellSize;

                    // Thread-safe friction lookup
                    double fVal = 0.1;
                    if (frictionList.Count > 0)
                    {
                        int fIdx = (i - 1) * M + (j - 1);
                        fVal = fIdx < frictionList.Count ? frictionList[fIdx] : frictionList.Last();
                    }
                    frictionMap[idx] = (float)Math.Max(0.0, Math.Min(1.0, fVal));

                    Ray3d rayDown = new Ray3d(new Point3d(x, y, rayStartZ), new Vector3d(0, 0, -1));
                    double terrainT = Rhino.Geometry.Intersect.Intersection.MeshRay(terrainMesh, rayDown);

                    if (terrainT >= 0.0)
                    {
                        Point3d terrainPt = rayDown.PointAt(terrainT);
                        Point3d drapedPt = new Point3d(terrainPt.X, terrainPt.Y, terrainPt.Z + analysisHeight);
                        gridPoints[idx] = drapedPt;

                        // Fast BoundingBox pre-check prior to point-in-mesh
                        for (int k = 0; k < validContextMeshes.Count; k++)
                        {
                            if (contextBBoxes[k].Contains(drapedPt))
                            {
                                if (validContextMeshes[k].IsPointInside(drapedPt, 0.01, true))
                                {
                                    obstacles[idx] = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        gridPoints[idx] = new Point3d(x, y, bbox.Min.Z);
                        // Ray missed terrain: outside terrain bounds, not a solid building obstacle
                        obstacles[idx] = false;
                    }

                    if (maskCurve != null && maskCurve.IsValid)
                    {
                        var ptContainment = maskCurve.Contains(new Point3d(x, y, 0), Plane.WorldXY, 0.01);
                        insideMask[idx] = (ptContainment == PointContainment.Inside || ptContainment == PointContainment.Coincident);
                    }
                    else
                    {
                        insideMask[idx] = true;
                    }
                }
            });

            // Compute local topographic slope in wind direction (speedup over ridges, shelter in dips)
            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obstacles[idx]) { terrainSlopeFactor[idx] = 1.0f; continue; }

                    int iNext = Math.Min(N, i + 1);
                    int iPrev = Math.Max(1, i - 1);
                    int jNext = Math.Min(M, j + 1);
                    int jPrev = Math.Max(1, j - 1);

                    double dz_dx = (gridPoints[IX(iNext, j, N)].Z - gridPoints[IX(iPrev, j, N)].Z) / ((iNext - iPrev) * cellSize);
                    double dz_dy = (gridPoints[IX(i, jNext, N)].Z - gridPoints[IX(i, jPrev, N)].Z) / ((jNext - jPrev) * cellSize);

                    // Slope component aligned with wind direction
                    double slopeAlongWind = dz_dx * windDir.X + dz_dy * windDir.Y;
                    // Jackson & Hunt hill speedup factor: ~1 + 0.6 * slope (clamped between 0.7x and 1.4x)
                    double factor = 1.0 + 0.5 * slopeAlongWind;
                    terrainSlopeFactor[idx] = (float)Math.Max(0.7, Math.Min(1.4, factor));
                }
            });

            float[] u = new float[totalCells];
            float[] v = new float[totalCells];
            float[] u0 = new float[totalCells];
            float[] v0 = new float[totalCells];
            float[] p = new float[totalCells]; // Persistent pressure buffer for warm starts

            double wSpeed = pedestrianSpeed + 0.001;
            
            // CONVERT TO CELLS/SEC TO STABILIZE GRID-SCALE PHYSICS
            float windU_cells = (float)(windVector.X / cellSize);
            float windV_cells = (float)(windVector.Y / cellSize);
            float wSpeed_cells = (float)(wSpeed / cellSize);

            // Courant number CFL = 0.5 for stable semi-Lagrangian advection
            float dt = 0.5f / wSpeed_cells;
            if (dt > 0.5f) dt = 0.5f;

            // Physical time step in seconds
            float dt_sec = dt * (float)(cellSize / wSpeed);

            // Initialize interior velocities with terrain slope modulation
            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (!obstacles[idx])
                    {
                        u[idx] = windU_cells * terrainSlopeFactor[idx];
                        v[idx] = windV_cells * terrainSlopeFactor[idx];
                    }
                }
            });

            // Minimum time steps required for flow to traverse the domain at least 1.5 times
            int minDomainFlushes = (int)Math.Ceiling(1.5 * Math.Max(N, M) / 0.5);
            int totalSteps = Math.Max(iterations, Math.Min(250, minDomainFlushes));

            // Dynamic Poisson relaxation count per time step
            int poissonPasses = Math.Max(12, Math.Min(40, Math.Max(N, M) / 3));

            int executedSteps = 0;
            for (int iter = 0; iter < totalSteps; iter++)
            {
                executedSteps++;

                // Apply domain boundaries (Inflow, Outflow, Free-slip, preserving building obstacles)
                SetDomainBoundaries(N, M, obstacles, u, v, p, windU_cells, windV_cells);

                float[] tmp = u; u = u0; u0 = tmp;
                tmp = v; v = v0; v0 = tmp;

                AdvectParallel(N, M, obstacles, u, u0, u0, v0, dt);
                AdvectParallel(N, M, obstacles, v, v0, u0, v0, dt);

                // Grid-independent kinematic diffusion
                if (viscosity > 0)
                {
                    tmp = u; u = u0; u0 = tmp;
                    tmp = v; v = v0; v0 = tmp;
                    // Dimensionless diffusion: a = dt_sec * nu / (cellSize^2)
                    float diffCoeff = (float)(viscosity * dt_sec / (cellSize * cellSize));
                    DiffuseParallel(N, M, obstacles, u, u0, diffCoeff);
                    DiffuseParallel(N, M, obstacles, v, v0, diffCoeff);
                }

                // Incompressibility projection with Red-Black SOR and warm-started pressure
                ProjectRedBlackSOR(N, M, obstacles, u, v, p, poissonPasses, windU_cells, windV_cells);

                // Ground friction damping (exponential decay per physical second)
                Parallel.For(1, N + 1, i =>
                {
                    for (int j = 1; j <= M; j++)
                    {
                        int idx = IX(i, j, N);
                        if (obstacles[idx]) continue;
                        float dragCoeff = frictionMap[idx] * (1.0f / (float)Math.Max(0.5, analysisHeight));
                        float decay = (float)Math.Exp(-dragCoeff * dt_sec);
                        u[idx] *= decay;
                        v[idx] *= decay;
                    }
                });

                // Convergence check every 10 steps after domain flush
                if (iter >= minDomainFlushes && iter % 10 == 0)
                {
                    float maxDelta = 0.0f;
                    for (int chk = 1; chk <= N; chk += 2)
                    {
                        for (int jhk = 1; jhk <= M; jhk += 2)
                        {
                            int idx = IX(chk, jhk, N);
                            if (obstacles[idx]) continue;
                            float du = (u[idx] - u0[idx]) * (float)cellSize;
                            float dv = (v[idx] - v0[idx]) * (float)cellSize;
                            float delta = (float)Math.Sqrt(du * du + dv * dv);
                            if (delta > maxDelta) maxDelta = delta;
                        }
                    }
                    if (maxDelta < 0.005f) // Converged to < 5 mm/s change
                    {
                        break;
                    }
                }
            }

            Mesh outMesh = new Mesh();
            List<Vector3d> outVectors = new List<Vector3d>();
            List<Point3d> outPoints = new List<Point3d>();
            List<double> outSpeeds = new List<double>();
            List<Color> outColors = new List<Color>();
            
            double maxSpeed = 0.0;
            double minSpeed = double.MaxValue;
            double sumSpeed = 0.0;
            int validCells = 0;
            int comfortCells = 0;

            // Full LDDC Lawson Pedestrian Comfort criteria counts
            int sittingCount = 0;         // < 4.0 m/s
            int standingCount = 0;        // 4.0 - 6.0 m/s
            int strollingCount = 0;       // 6.0 - 8.0 m/s
            int businessWalkCount = 0;    // 8.0 - 10.0 m/s
            int uncomfortCount = 0;       // > 10.0 m/s
            double maxTurbulenceIntensity = 0.0;

            double[] speeds = new double[totalCells];
            double[] turbulenceIntensity = new double[totalCells];

            // Compute velocities, local gradients, and Lawson comfort categories
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    
                    // Revert cells/sec back to physical m/s
                    double u_ms = u[idx] * cellSize;
                    double v_ms = v[idx] * cellSize;
                    double speed = Math.Sqrt(u_ms * u_ms + v_ms * v_ms);
                    speeds[idx] = speed;

                    if (obstacles[idx] || !insideMask[idx]) continue;

                    // Local shear strain rate from velocity gradient tensor
                    double du_dx = (u[IX(i + 1, j, N)] - u[IX(i - 1, j, N)]) * 0.5;
                    double du_dy = (u[IX(i, j + 1, N)] - u[IX(i, j - 1, N)]) * 0.5;
                    double dv_dx = (v[IX(i + 1, j, N)] - v[IX(i - 1, j, N)]) * 0.5;
                    double dv_dy = (v[IX(i, j + 1, N)] - v[IX(i, j - 1, N)]) * 0.5;

                    double shear = Math.Sqrt(2 * du_dx * du_dx + 2 * dv_dy * dv_dy + Math.Pow(du_dy + dv_dx, 2));
                    // Smagorinsky sub-grid turbulent kinetic energy estimate
                    double tke = Math.Pow(0.12 * cellSize * shear, 2);
                    double sigmaU = Math.Sqrt(2.0 / 3.0 * tke);
                    double ti = (speed > 0.1) ? Math.Min(1.0, sigmaU / speed) : 0.0;
                    turbulenceIntensity[idx] = ti;
                    if (ti > maxTurbulenceIntensity) maxTurbulenceIntensity = ti;

                    // Effective gust wind speed for pedestrian comfort
                    double gustSpeed = speed * (1.0 + 1.2 * ti);

                    if (gustSpeed < 4.0) sittingCount++;
                    else if (gustSpeed < 6.0) standingCount++;
                    else if (gustSpeed < 8.0) strollingCount++;
                    else if (gustSpeed < 10.0) businessWalkCount++;
                    else uncomfortCount++;

                    if (speed > maxSpeed) maxSpeed = speed;
                    if (speed < minSpeed) minSpeed = speed;
                    sumSpeed += speed;
                    validCells++;
                    if (speed <= comfortThreshold) comfortCells++;

                    double normalized = Math.Min(speed / (wSpeed * 1.5), 1.0);
                    Color ptColor = GetColor(normalized, customColors);

                    outPoints.Add(gridPoints[idx]);
                    outVectors.Add(new Vector3d(u_ms, v_ms, 0));
                    outColors.Add(ptColor);
                    outSpeeds.Add(speed);
                }
            }

            double avgSpeed = validCells > 0 ? sumSpeed / validCells : 0;
            if (minSpeed == double.MaxValue) minSpeed = 0;
            double pctComfort = validCells > 0 ? ((double)comfortCells / validCells) * 100.0 : 0;
            double pctSitting = validCells > 0 ? ((double)sittingCount / validCells) * 100.0 : 0;
            double pctStanding = validCells > 0 ? ((double)standingCount / validCells) * 100.0 : 0;
            double pctStrolling = validCells > 0 ? ((double)strollingCount / validCells) * 100.0 : 0;
            double pctBusinessWalk = validCells > 0 ? ((double)businessWalkCount / validCells) * 100.0 : 0;
            double pctUncomfort = validCells > 0 ? ((double)uncomfortCount / validCells) * 100.0 : 0;

            int[] vMap = new int[totalCells];
            int vCounter = 0;
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    outMesh.Vertices.Add(gridPoints[idx]);
                    vMap[idx] = vCounter++;
                    
                    if (obstacles[idx]) {
                        outMesh.VertexColors.Add(Color.Gray);
                    } else {
                        double speed = speeds[idx];
                        double normalized = Math.Min(speed / (wSpeed * 1.5), 1.0);
                        outMesh.VertexColors.Add(GetColor(normalized, customColors));
                    }
                }
            }

            for (int i = 1; i < N; i++)
            {
                for (int j = 1; j < M; j++)
                {
                    int idx00 = IX(i, j, N);
                    int idx10 = IX(i + 1, j, N);
                    int idx11 = IX(i + 1, j + 1, N);
                    int idx01 = IX(i, j + 1, N);

                    if (insideMask[idx00] || insideMask[idx10] || insideMask[idx11] || insideMask[idx01]) {
                        outMesh.Faces.AddFace(vMap[idx00], vMap[idx10], vMap[idx11], vMap[idx01]);
                    }
                }
            }
            outMesh.Compact(); 

            DA.SetData(0, outMesh);
            DA.SetDataList(1, outPoints);
            DA.SetDataList(2, outVectors);
            DA.SetDataList(3, outColors);
            DA.SetDataList(4, outSpeeds);

            List<Color> legendColors = customColors.Count > 0 ? customColors : new List<Color> { Color.FromArgb(255, 0, 0, 255), Color.FromArgb(255, 255, 0, 0) };
            string colorJsonArray = "[" + string.Join(",", legendColors.Select(c => $"{{\"R\":{c.R},\"G\":{c.G},\"B\":{c.B}}}")) + "]";
            string labelsJson = $@"[""0.0"", ""{(wSpeed * 1.5):F1}""]";
            
            string jsonStr = $@"{{
  ""Type"": ""Gradient"",
  ""Title"": ""WIND SPEED (m/s)"",
  ""Colors"": {colorJsonArray},
  ""Labels"": {labelsJson},
  ""Metrics"": [
    {{ ""Name"": ""Avg Speed"", ""Value"": ""{avgSpeed:F1} m/s"" }},
    {{ ""Name"": ""Comfort ({comfortThreshold:F1} m/s)"", ""Value"": ""{pctComfort:F1}%"" }},
    {{ ""Name"": ""Sitting (<4 m/s)"", ""Value"": ""{pctSitting:F1}%"" }},
    {{ ""Name"": ""Standing (<6 m/s)"", ""Value"": ""{pctStanding:F1}%"" }},
    {{ ""Name"": ""Strolling (<8 m/s)"", ""Value"": ""{pctStrolling:F1}%"" }},
    {{ ""Name"": ""Business (<10 m/s)"", ""Value"": ""{pctBusinessWalk:F1}%"" }},
    {{ ""Name"": ""Uncomfortable"", ""Value"": ""{pctUncomfort:F1}%"" }}
  ]
}}";
            DA.SetData(5, jsonStr);

            string infoStr = 
                "FAST CFD (2.5D MULTI-THREADED EULERIAN SOLVER)\n" +
                "============================================\n\n" +
                "SIMULATION SUMMARY:\n" +
                $"- Domain Grid: {cols} x {rows} ({validCells:N0} active cells)\n" +
                $"- Spatial Resolution: {cellSize:F1} m | Analysis Drape Height: {analysisHeight:F1} m\n" +
                $"- Inlet Wind: {windSpeed:F1} m/s @ {refHeight:F1}m reference height -> {pedestrianSpeed:F1} m/s @ {analysisHeight:F1}m pedestrian level\n" +
                $"- Steps Executed: {executedSteps} time steps (dt = {dt_sec:F3}s) | Poisson SOR Passes: {poissonPasses} per step\n" +
                $"- Velocity Field: Avg = {avgSpeed:F1} m/s | Min = {minSpeed:F1} m/s | Max = {maxSpeed:F1} m/s\n\n" +
                "LAWSON-STYLE PEDESTRIAN COMFORT CLASSES (SINGLE WIND SCENARIO):\n" +
                $"- Sitting / Outdoor Dining (<4.0 m/s): {pctSitting:F1}%\n" +
                $"- Standing / Pedestrian Waiting (<6.0 m/s): {pctStanding:F1}%\n" +
                $"- Strolling / Leisure Walking (<8.0 m/s): {pctStrolling:F1}%\n" +
                $"- Business Walking / Transit (<10.0 m/s): {pctBusinessWalk:F1}%\n" +
                $"- Uncomfortable / High Gusts (>10.0 m/s): {pctUncomfort:F1}%\n" +
                $"- Custom Threshold Comfort (<= {comfortThreshold:F1} m/s): {pctComfort:F1}%\n" +
                $"- Peak Turbulence Intensity (TI): {maxTurbulenceIntensity:P1}\n\n" +
                "NUMERICAL & PHYSICAL SPECIFICATIONS:\n" +
                "1. Multi-Core Solver: Thread-safe Parallel.For advection, diffusion, and Red-Black Successive Over-Relaxation (SOR) with warm-start pressure projection.\n" +
                "2. Non-Reflecting Domain Boundaries: Open Neumann outflow and Dirichlet pressure anchors at domain exit; windward boundaries enforce clean inflow without erasing edge buildings.\n" +
                "3. Zero-Gradient Neumann Obstacles: Solid facades enforce dp/dn = 0, preventing artificial vacuum suction holes and accurately developing stagnation zones and deflecting wakes.\n" +
                "4. Topographic Modulation: Integrates wind-aligned terrain slope acceleration (speed-up over ridges, deceleration in depressions).\n" +
                "5. Grid-Independent Physics: Kinematic viscosity and friction decay are non-dimensionalised against physical cell size and time step.\n\n" +
                "LIMITATIONS & METHODOLOGY NOTE:\n" +
                "- 2.5D Assumption: Simulates horizontal Venturi tunneling, gap flows, and recirculating wake eddies over 3D terrain, but does not capture 3D vertical skyscraper downdrafts.\n" +
                "- Single Scenario Evaluation: True regulatory Lawson criteria evaluate multi-directional statistical exceedance over a full annual wind climate; this component computes the deterministic wind field for the specified wind vector.";
            
            DA.SetData(6, infoStr);

            sw.Stop();
            Hud.Done(this, sw.Elapsed.TotalMilliseconds, $"Grid: {cols}x{rows}\nAvg: {avgSpeed:F1} | Max: {maxSpeed:F1}\nComfort: {pctComfort:F1}%");
        }

        private Color GetColor(double normalized, List<Color> palette)
        {
            if (palette == null || palette.Count == 0)
            {
                int r = (int)(normalized * 255);
                int b = (int)((1.0 - normalized) * 255);
                return Color.FromArgb(255, r, 0, b);
            }
            if (palette.Count == 1) return palette[0];
            
            double scaled = Math.Max(0.0, Math.Min(1.0, normalized)) * (palette.Count - 1);
            int idx1 = (int)Math.Floor(scaled);
            int idx2 = (int)Math.Ceiling(scaled);
            if (idx1 == idx2) return palette[idx1];
            if (idx2 >= palette.Count) return palette.Last();
            
            double t = scaled - idx1;
            Color c1 = palette[idx1];
            Color c2 = palette[idx2];
            
            int R = (int)(c1.R + (c2.R - c1.R) * t);
            int G = (int)(c1.G + (c2.G - c1.G) * t);
            int B = (int)(c1.B + (c2.B - c1.B) * t);
            
            return Color.FromArgb(255, R, G, B);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendItem(menu, "Generate Default Sliders", Menu_GenerateSliders);
        }

        private void Menu_GenerateSliders(object sender, EventArgs e)
        {
            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            float pivotX = this.Attributes.Pivot.X - 250;
            float pivotY = this.Attributes.Pivot.Y - 100;

            CreateSlider(doc, "WindSpeed", 1.0, 50.0, 10.0, 3, pivotX, pivotY);
            CreateSlider(doc, "CellSize", 1.0, 20.0, 4.0, 4, pivotX, pivotY + 30);
            CreateSlider(doc, "Iterations", 10, 500, 50, 5, pivotX, pivotY + 60);
            CreateSlider(doc, "AnalysisHeight", 0.0, 50.0, 1.5, 6, pivotX, pivotY + 90);
            CreateSlider(doc, "Viscosity", 0.0, 1.0, 0.1, 7, pivotX, pivotY + 120);
            CreateSlider(doc, "Friction", 0.0, 1.0, 0.1, 8, pivotX, pivotY + 150);
            CreateSlider(doc, "ComfortThreshold", 1.0, 20.0, 5.0, 9, pivotX, pivotY + 180);
            CreateSlider(doc, "WindRefHeight", 1.0, 50.0, 10.0, 12, pivotX, pivotY + 210);

            doc.NewSolution(false);
        }

        private void CreateSlider(GH_Document doc, string name, double min, double max, double val, int index, float x, float y)
        {
            if (this.Params.Input[index].SourceCount > 0) return;

            GH_NumberSlider slider = new GH_NumberSlider();
            slider.CreateAttributes();
            slider.Attributes.Pivot = new PointF(x, y);
            
            if (val == (int)val && max > 1.0)
                slider.Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Integer;
            else
                slider.Slider.Type = Grasshopper.GUI.Base.GH_SliderAccuracy.Float;

            slider.Slider.Minimum = (decimal)min;
            slider.Slider.Maximum = (decimal)max;
            slider.Slider.DecimalPlaces = 1;
            slider.Slider.Value = (decimal)val;
            slider.NickName = name;

            doc.AddObject(slider, false);
            this.Params.Input[index].AddSource(slider);
        }

        private int IX(int i, int j, int N) { return i + (N + 2) * j; }

        // OPEN OUTFLOW / INFLOW DOMAIN BOUNDARIES
        private void SetDomainBoundaries(int N, int M, bool[] obs, float[] u, float[] v, float[] p, float windU, float windV)
        {
            // Left & Right boundaries
            for (int j = 1; j <= M; j++)
            {
                int left0 = IX(0, j, N);
                int left1 = IX(1, j, N);
                int rightN = IX(N, j, N);
                int rightN1 = IX(N + 1, j, N);

                if (windU >= 0)
                {
                    // Left is Inflow
                    u[left0] = windU; v[left0] = windV;
                    if (!obs[left1]) { u[left1] = windU; v[left1] = windV; }
                    // Right is Outflow (Neumann for velocity, Dirichlet p=0 anchor)
                    u[rightN1] = u[rightN]; v[rightN1] = v[rightN];
                    p[rightN1] = 0.0f;
                }
                else
                {
                    // Left is Outflow
                    u[left0] = u[left1]; v[left0] = v[left1];
                    p[left0] = 0.0f;
                    // Right is Inflow
                    u[rightN1] = windU; v[rightN1] = windV;
                    if (!obs[rightN]) { u[rightN] = windU; v[rightN] = windV; }
                }
            }

            // Bottom & Top boundaries
            for (int i = 1; i <= N; i++)
            {
                int bot0 = IX(i, 0, N);
                int bot1 = IX(i, 1, N);
                int topM = IX(i, M, N);
                int topM1 = IX(i, M + 1, N);

                if (windV >= 0)
                {
                    // Bottom is Inflow
                    u[bot0] = windU; v[bot0] = windV;
                    if (!obs[bot1]) { u[bot1] = windU; v[bot1] = windV; }
                    // Top is Outflow
                    u[topM1] = u[topM]; v[topM1] = v[topM];
                    p[topM1] = 0.0f;
                }
                else
                {
                    // Bottom is Outflow
                    u[bot0] = u[bot1]; v[bot0] = v[bot1];
                    p[bot0] = 0.0f;
                    // Top is Inflow
                    u[topM1] = windU; v[topM1] = windV;
                    if (!obs[topM]) { u[topM] = windU; v[topM] = windV; }
                }
            }

            // Ghost corner cells
            u[IX(0, 0, N)] = 0.5f * (u[IX(1, 0, N)] + u[IX(0, 1, N)]);
            v[IX(0, 0, N)] = 0.5f * (v[IX(1, 0, N)] + v[IX(0, 1, N)]);
            u[IX(N + 1, 0, N)] = 0.5f * (u[IX(N, 0, N)] + u[IX(N + 1, 1, N)]);
            v[IX(N + 1, 0, N)] = 0.5f * (v[IX(N, 0, N)] + v[IX(N + 1, 1, N)]);
            u[IX(0, M + 1, N)] = 0.5f * (u[IX(1, M + 1, N)] + u[IX(0, M, N)]);
            v[IX(0, M + 1, N)] = 0.5f * (v[IX(1, M + 1, N)] + v[IX(0, M, N)]);
            u[IX(N + 1, M + 1, N)] = 0.5f * (u[IX(N, M + 1, N)] + u[IX(N + 1, M, N)]);
            v[IX(N + 1, M + 1, N)] = 0.5f * (v[IX(N, M + 1, N)] + v[IX(N + 1, M, N)]);
        }

        // PARALLEL ADVECTION (Semi-Lagrangian)
        private void AdvectParallel(int N, int M, bool[] obs, float[] d, float[] d0, float[] u, float[] v, float dt)
        {
            float dt0 = dt; 
            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obs[idx]) continue;

                    float x = i - dt0 * u[idx];
                    float y = j - dt0 * v[idx];

                    if (x < 0.5f) x = 0.5f; if (x > N + 0.5f) x = N + 0.5f;
                    int i0 = (int)x; int i1 = i0 + 1;

                    if (y < 0.5f) y = 0.5f; if (y > M + 0.5f) y = M + 0.5f;
                    int j0 = (int)y; int j1 = j0 + 1;

                    float s1 = x - i0; float s0 = 1.0f - s1;
                    float t1 = y - j0; float t0 = 1.0f - t1;

                    d[idx] = s0 * (t0 * d0[IX(i0, j0, N)] + t1 * d0[IX(i0, j1, N)]) +
                             s1 * (t0 * d0[IX(i1, j0, N)] + t1 * d0[IX(i1, j1, N)]);
                }
            });
            SetBndVelocity(N, M, obs, d);
        }

        // PARALLEL DIFFUSION (Explicit ping-pong buffer with dimensionless diffusion coeff)
        private void DiffuseParallel(int N, int M, bool[] obs, float[] x, float[] x0, float diffCoeff)
        {
            if (diffCoeff <= 0.0f) return;
            float a = diffCoeff;
            int totalCells = (N + 2) * (M + 2);
            float[] src = x;
            float[] dst = new float[totalCells];
            Array.Copy(src, dst, totalCells);

            const int passes = 10;
            for (int k = 0; k < passes; k++)
            {
                Parallel.For(1, N + 1, i =>
                {
                    for (int j = 1; j <= M; j++)
                    {
                        int idx = IX(i, j, N);
                        if (obs[idx]) continue;
                        dst[idx] = (x0[idx] + a * (src[IX(i - 1, j, N)] + src[IX(i + 1, j, N)] + src[IX(i, j - 1, N)] + src[IX(i, j + 1, N)])) / (1 + 4 * a);
                    }
                });
                SetBndVelocity(N, M, obs, dst);
                float[] swap = src; src = dst; dst = swap;
            }

            // Ensure destination array contains the final field
            if (src != x)
            {
                Array.Copy(src, x, totalCells);
            }
        }

        // RED-BLACK GAUSS-SEIDEL WITH SUCCESSIVE OVER-RELAXATION (SOR) & WARM-STARTED PRESSURE
        private void ProjectRedBlackSOR(int N, int M, bool[] obs, float[] u, float[] v, float[] p, int passes, float windU, float windV)
        {
            int totalCells = (N + 2) * (M + 2);
            float[] div = new float[totalCells];

            // 1. Calculate velocity divergence
            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obs[idx])
                    {
                        div[idx] = 0;
                        continue;
                    }
                    div[idx] = -0.5f * (u[IX(i + 1, j, N)] - u[IX(i - 1, j, N)] +
                                        v[IX(i, j + 1, N)] - v[IX(i, j - 1, N)]);
                }
            });

            SetBndVelocity(N, M, obs, div);
            SetBndPressure(N, M, obs, p);

            // Optimal SOR over-relaxation parameter
            const float omega = 1.6f;

            // 2. Red-Black Gauss-Seidel iterations (Parallel safe because red cells only depend on black, and vice versa)
            for (int k = 0; k < passes; k++)
            {
                // RED PASS: (i + j) % 2 == 0
                Parallel.For(1, N + 1, i =>
                {
                    for (int j = 1; j <= M; j++)
                    {
                        if ((i + j) % 2 != 0) continue;
                        int idx = IX(i, j, N);
                        if (obs[idx]) continue;
                        float pStar = (div[idx] + p[IX(i - 1, j, N)] + p[IX(i + 1, j, N)] +
                                                p[IX(i, j - 1, N)] + p[IX(i, j + 1, N)]) * 0.25f;
                        p[idx] += omega * (pStar - p[idx]);
                    }
                });
                SetBndPressure(N, M, obs, p);

                // BLACK PASS: (i + j) % 2 == 1
                Parallel.For(1, N + 1, i =>
                {
                    for (int j = 1; j <= M; j++)
                    {
                        if ((i + j) % 2 != 1) continue;
                        int idx = IX(i, j, N);
                        if (obs[idx]) continue;
                        float pStar = (div[idx] + p[IX(i - 1, j, N)] + p[IX(i + 1, j, N)] +
                                                p[IX(i, j - 1, N)] + p[IX(i, j + 1, N)]) * 0.25f;
                        p[idx] += omega * (pStar - p[idx]);
                    }
                });
                SetBndPressure(N, M, obs, p);
            }

            // 3. Correct velocity field with pressure gradient
            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obs[idx]) continue;
                    u[idx] -= 0.5f * (p[IX(i + 1, j, N)] - p[IX(i - 1, j, N)]);
                    v[idx] -= 0.5f * (p[IX(i, j + 1, N)] - p[IX(i, j - 1, N)]);
                }
            });

            SetBndVelocity(N, M, obs, u);
            SetBndVelocity(N, M, obs, v);
            SetDomainBoundaries(N, M, obs, u, v, p, windU, windV);
        }

        // VELOCITY BOUNDARY CONDITIONS (No-slip at solid obstacles)
        private void SetBndVelocity(int N, int M, bool[] obs, float[] x)
        {
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obs[idx])
                    {
                        x[idx] = 0.0f;
                    }
                }
            }
        }

        // NEUMANN PRESSURE BOUNDARY CONDITIONS (Zero normal gradient dp/dn = 0)
        // Prevents artificial vacuum / suction holes at solid building facets
        private void SetBndPressure(int N, int M, bool[] obs, float[] p)
        {
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obs[idx])
                    {
                        float sum = 0f;
                        int count = 0;
                        int left = IX(i - 1, j, N);
                        int right = IX(i + 1, j, N);
                        int down = IX(i, j - 1, N);
                        int up = IX(i, j + 1, N);

                        if (i > 1 && !obs[left]) { sum += p[left]; count++; }
                        if (i < N && !obs[right]) { sum += p[right]; count++; }
                        if (j > 1 && !obs[down]) { sum += p[down]; count++; }
                        if (j < M && !obs[up]) { sum += p[up]; count++; }

                        if (count > 0)
                            p[idx] = sum / count;
                        else
                            p[idx] = 0.0f;
                    }
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("FastCFD.png");
        public override Guid ComponentGuid => new Guid("11223344-5566-7788-9900-AABBCCDDEEFF");
    }
}
