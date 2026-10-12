using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class FastCFD : GH_Component
    {
        public FastCFD()
          : base("Fast CFD", "FastCFD",
              "A 2.5D Terrain-Following Grid Fluid Solver for fast urban wind analysis.",
              "Enzyme", "Site Analysis")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TerrainMesh", "Input Terrain Mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("ContextMeshes", "ContextMeshes", "Buildings and context as closed meshes", GH_ParamAccess.list);
            pManager.AddVectorParameter("WindVector", "WindVector", "Wind direction", GH_ParamAccess.item, new Vector3d(1, 1, 0));
            pManager.AddNumberParameter("WindSpeed", "WindSpeed", "Baseline wind velocity (m/s)", GH_ParamAccess.item, 10.0);
            pManager.AddNumberParameter("CellSize", "CellSize", "Resolution of the grid in meters.", GH_ParamAccess.item, 4.0);
            pManager.AddIntegerParameter("Iterations", "Iterations", "Simulation steps", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("AnalysisHeight", "AnalysisHeight", "Drape offset above terrain (m)", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("Viscosity", "Viscosity", "Kinematic viscosity (diffusion/turbulence)", GH_ParamAccess.item, 0.1);
            pManager.AddNumberParameter("Friction", "Friction", "Surface drag (0.0 to 1.0). Accepts item or list.", GH_ParamAccess.list, 0.1);
            pManager.AddNumberParameter("ComfortThreshold", "ComfortThreshold", "Threshold for pedestrian comfort (m/s)", GH_ParamAccess.item, 5.0);
            pManager.AddCurveParameter("BoundaryMask", "BoundaryMask", "Optional closed curve to crop the simulation domain and filter statistics.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "Colors", "Custom color spectrum override (min to max)", GH_ParamAccess.list);
            
            pManager[1].Optional = true;
            pManager[8].Optional = true;
            pManager[10].Optional = true;
            pManager[11].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("WindMesh", "WindMesh", "Heatmap of wind speeds mapped to the terrain", GH_ParamAccess.item);
            pManager.AddPointParameter("Points", "Points", "Grid points corresponding to the vectors", GH_ParamAccess.list);
            pManager.AddVectorParameter("WindVectors", "WindVectors", "Wind velocity vectors for visualization", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "The color assigned to each point/vector", GH_ParamAccess.list);
            pManager.AddNumberParameter("Speeds", "Speeds", "Wind speed magnitude (m/s) at each point", GH_ParamAccess.list);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON string for Dashboard and Legend", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Simulation data and timing", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh terrainMesh = null;
            if (!DA.GetData(0, ref terrainMesh)) return;

            List<Mesh> contextMeshes = new List<Mesh>();
            DA.GetDataList(1, contextMeshes);

            Vector3d windDir = new Vector3d(1, 1, 0);
            if (!DA.GetData(2, ref windDir)) return;

            double windSpeed = 10.0;
            if (!DA.GetData(3, ref windSpeed)) return;

            if (!windDir.Unitize()) windDir = new Vector3d(1, 0, 0);
            Vector3d windVector = windDir * windSpeed;

            double cellSize = 4.0;
            if (!DA.GetData(4, ref cellSize)) return;
            if (cellSize < 0.5) cellSize = 0.5;

            int iterations = 50;
            if (!DA.GetData(5, ref iterations)) return;

            double analysisHeight = 1.5;
            if (!DA.GetData(6, ref analysisHeight)) return;

            double viscosity = 0.1;
            if (!DA.GetData(7, ref viscosity)) return;

            List<double> frictionList = new List<double>();
            DA.GetDataList(8, frictionList);

            double comfortThreshold = 5.0;
            if (!DA.GetData(9, ref comfortThreshold)) return;

            Curve maskCurve = null;
            DA.GetData(10, ref maskCurve);

            List<Color> customColors = new List<Color>();
            DA.GetDataList(11, customColors);

            var sw = System.Diagnostics.Stopwatch.StartNew();

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

            // 1. OPTIMIZATION: Pre-filter valid context meshes & PRE-CACHE BOUNDING BOXES ONCE
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

            double startX = bbox.Min.X + cellSize / 2.0;
            double startY = bbox.Min.Y + cellSize / 2.0;

            // 2. OPTIMIZATION: Multi-threaded grid initialization with cached context lookups
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
                        obstacles[idx] = true;
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

            float[] u = new float[totalCells];
            float[] v = new float[totalCells];
            float[] u0 = new float[totalCells];
            float[] v0 = new float[totalCells];

            double wSpeed = windSpeed + 0.01;
            
            // CONVERT TO CELLS/SEC TO STABILIZE GRID-SCALE PHYSICS
            float windU_cells = (float)(windVector.X / cellSize);
            float windV_cells = (float)(windVector.Y / cellSize);
            float wSpeed_cells = (float)(wSpeed / cellSize);

            float dt = 0.5f / wSpeed_cells;
            if (dt > 0.5f) dt = 0.5f;

            // Initialize interior velocities
            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (!obstacles[idx])
                    {
                        u[idx] = windU_cells;
                        v[idx] = windV_cells;
                    }
                }
            });

            // 4. OPTIMIZATION: Dynamic Poisson iteration count based on domain extent
            int poissonIterations = Math.Max(15, Math.Min(60, Math.Max(N, M) / 2));

            for (int iter = 0; iter < iterations; iter++)
            {
                // Boundary conditions at domain edges
                for (int j = 1; j <= M; j++) {
                    if (windU_cells > 0) { u[IX(1, j, N)] = windU_cells; v[IX(1, j, N)] = windV_cells; obstacles[IX(1, j, N)] = false; }
                    if (windU_cells < 0) { u[IX(N, j, N)] = windU_cells; v[IX(N, j, N)] = windV_cells; obstacles[IX(N, j, N)] = false; }
                }
                for (int i = 1; i <= N; i++) {
                    if (windV_cells > 0) { u[IX(i, 1, N)] = windU_cells; v[IX(i, 1, N)] = windV_cells; obstacles[IX(i, 1, N)] = false; }
                    if (windV_cells < 0) { u[IX(i, M, N)] = windU_cells; v[IX(i, M, N)] = windV_cells; obstacles[IX(i, M, N)] = false; }
                }

                float[] tmp = u; u = u0; u0 = tmp;
                tmp = v; v = v0; v0 = tmp;

                AdvectParallel(N, M, obstacles, u, u0, u0, v0, dt);
                AdvectParallel(N, M, obstacles, v, v0, u0, v0, dt);

                if (viscosity > 0)
                {
                    tmp = u; u = u0; u0 = tmp;
                    tmp = v; v = v0; v0 = tmp;
                    DiffuseParallel(N, M, obstacles, u, u0, (float)viscosity, dt);
                    DiffuseParallel(N, M, obstacles, v, v0, (float)viscosity, dt);
                }

                ProjectParallel(N, M, obstacles, u, v, u0, v0, poissonIterations);

                // Surface Drag / Ground Friction
                Parallel.For(1, N + 1, i =>
                {
                    for (int j = 1; j <= M; j++)
                    {
                        int idx = IX(i, j, N);
                        if (obstacles[idx]) continue;
                        float drag = frictionMap[idx];
                        u[idx] *= (1.0f - drag * dt);
                        v[idx] *= (1.0f - drag * dt);
                    }
                });
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

            // Pedestrian comfort categories (Lawson scale counts)
            int sittingCount = 0;   // < 4 m/s
            int standingCount = 0;  // 4 - 6 m/s
            int strollingCount = 0; // 6 - 8 m/s
            int uncomfortCount = 0; // > 8 m/s
            double maxTurbulenceIntensity = 0.0;

            double[] speeds = new double[totalCells];
            double[] turbulenceIntensity = new double[totalCells];

            // 4. OPTIMIZATION: Compute velocity & localized velocity gradients (Turbulence Intensity)
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    
                    // Revert cells/sec back to m/s
                    double u_ms = u[idx] * cellSize;
                    double v_ms = v[idx] * cellSize;
                    double speed = Math.Sqrt(u_ms * u_ms + v_ms * v_ms);
                    speeds[idx] = speed;

                    if (obstacles[idx] || !insideMask[idx]) continue;

                    // Calculate local shear rate / velocity gradient for turbulence estimation
                    double du_dx = (u[IX(i + 1, j, N)] - u[IX(i - 1, j, N)]) * 0.5;
                    double du_dy = (u[IX(i, j + 1, N)] - u[IX(i, j - 1, N)]) * 0.5;
                    double dv_dx = (v[IX(i + 1, j, N)] - v[IX(i - 1, j, N)]) * 0.5;
                    double dv_dy = (v[IX(i, j + 1, N)] - v[IX(i, j - 1, N)]) * 0.5;

                    double shear = Math.Sqrt(2 * du_dx * du_dx + 2 * dv_dy * dv_dy + Math.Pow(du_dy + dv_dx, 2));
                    // Turbulent Kinetic Energy approximation (sub-grid mixing length)
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
    {{ ""Name"": ""Strolling (<8 m/s)"", ""Value"": ""{pctStrolling:F1}%"" }},
    {{ ""Name"": ""Uncomfortable"", ""Value"": ""{pctUncomfort:F1}%"" }}
  ]
}}";
            DA.SetData(5, jsonStr);

            string infoStr = 
                "FAST CFD (2.5D MULTI-THREADED EULERIAN SOLVER)\n" +
                "============================================\n\n" +
                "PHYSICS & SPEED UPGRADES:\n" +
                "- Multi-core Parallel Processing: Grid draping, Advection, Diffusion, and Poisson projection run in parallel.\n" +
                "- Pre-cached Bounding Box Index: Context building collision checks bypass redundant 3D raycasting.\n" +
                "- Zero-gradient Neumann Boundaries: Fluid hitting facades accurately develops stagnation pressure and deflects outward instead of being artificially absorbed.\n" +
                "- Dynamic Poisson Scaling: Convergence passes dynamically adapt to domain scale for true wake formation.\n" +
                "- Pedestrian Comfort Scale: Local velocity gradients quantify Turbulence Intensity (TI) and Lawson comfort criteria.\n\n" +
                "LAWSON COMFORT DISTRIBUTION:\n" +
                $"- Sitting / Cafe (<4 m/s): {pctSitting:F1}%\n" +
                $"- Standing / Waiting (<6 m/s): {pctStanding:F1}%\n" +
                $"- Strolling / Walking (<8 m/s): {pctStrolling:F1}%\n" +
                $"- Uncomfortable / High Gust (>8 m/s): {pctUncomfort:F1}%\n" +
                $"- Peak Turbulence Intensity: {maxTurbulenceIntensity:P1}\n\n" +
                "LIMITATIONS:\n" +
                "Calculates a terrain-following 2.5D layer. Simulates horizontal tunneling (Venturi) and recirculating eddies, but does not simulate 3D skyscraper vertical downwash vortices.";
            
            DA.SetData(6, infoStr);

            sw.Stop();
            Message = $"FAST CFD\nTime: {sw.Elapsed.TotalMilliseconds:F2} ms\n---\nGrid: {cols}x{rows}\nAvg: {avgSpeed:F1} | Max: {maxSpeed:F1}\nSitting: {pctSitting:F1}%\nStrolling: {pctStrolling:F1}%";
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

        // PARALLEL ADVECTION
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

        // PARALLEL DIFFUSION (Ping-pong buffers for Jacobi iterations)
        private void DiffuseParallel(int N, int M, bool[] obs, float[] x, float[] x0, float diff, float dt)
        {
            float a = dt * diff;
            int totalCells = (N + 2) * (M + 2);
            float[] next = new float[totalCells];
            Array.Copy(x, next, totalCells);

            for (int k = 0; k < 10; k++)
            {
                Parallel.For(1, N + 1, i =>
                {
                    for (int j = 1; j <= M; j++)
                    {
                        int idx = IX(i, j, N);
                        if (obs[idx]) continue;
                        next[idx] = (x0[idx] + a * (x[IX(i - 1, j, N)] + x[IX(i + 1, j, N)] + x[IX(i, j - 1, N)] + x[IX(i, j + 1, N)])) / (1 + 4 * a);
                    }
                });
                SetBndVelocity(N, M, obs, next);
                float[] tmp = x; x = next; next = tmp;
            }
        }

        // PARALLEL PROJECTION WITH NEUMANN PRESSURE BOUNDARY CONDITIONS
        private void ProjectParallel(int N, int M, bool[] obs, float[] u, float[] v, float[] p, float[] div, int iterations)
        {
            int totalCells = (N + 2) * (M + 2);
            float[] pNext = new float[totalCells];

            Parallel.For(1, N + 1, i =>
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    if (obs[idx])
                    {
                        div[idx] = 0;
                        p[idx] = 0;
                        continue;
                    }
                    div[idx] = -0.5f * (u[IX(i + 1, j, N)] - u[IX(i - 1, j, N)] +
                                        v[IX(i, j + 1, N)] - v[IX(i, j - 1, N)]);
                    p[idx] = 0;
                }
            });

            SetBndVelocity(N, M, obs, div);
            SetBndPressure(N, M, obs, p);

            // Parallel Jacobi Poisson solver with ping-pong buffers
            for (int k = 0; k < iterations; k++)
            {
                Parallel.For(1, N + 1, i =>
                {
                    for (int j = 1; j <= M; j++)
                    {
                        int idx = IX(i, j, N);
                        if (obs[idx]) continue;
                        pNext[idx] = (div[idx] + p[IX(i - 1, j, N)] + p[IX(i + 1, j, N)] +
                                                p[IX(i, j - 1, N)] + p[IX(i, j + 1, N)]) * 0.25f;
                    }
                });
                SetBndPressure(N, M, obs, pNext);
                float[] tmp = p; p = pNext; pNext = tmp;
            }

            // Correct velocities using pressure gradient
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
        }

        // VELOCITY BOUNDARY CONDITIONS (No-slip at obstacles)
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
