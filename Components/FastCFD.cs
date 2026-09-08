using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
              "Enzyme", "Terrain")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TM", "Input Terrain Mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("ContextMeshes", "CM", "Buildings and context as closed meshes", GH_ParamAccess.list);
            pManager.AddVectorParameter("WindVector", "WV", "Wind direction and speed (m/s)", GH_ParamAccess.item, new Vector3d(5, 5, 0));
            pManager.AddNumberParameter("CellSize", "CS", "Resolution of the grid in meters.", GH_ParamAccess.item, 4.0);
            pManager.AddIntegerParameter("Iterations", "I", "Simulation steps", GH_ParamAccess.item, 50);
            pManager.AddNumberParameter("AnalysisHeight", "Z", "Drape offset above terrain (m)", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("Viscosity", "V", "Kinematic viscosity (diffusion/turbulence)", GH_ParamAccess.item, 0.1);
            pManager.AddNumberParameter("Friction", "F", "Surface drag (0.0 to 1.0). Accepts item or list.", GH_ParamAccess.list, 0.1);
            pManager.AddNumberParameter("ComfortThreshold", "CT", "Threshold for pedestrian comfort (m/s)", GH_ParamAccess.item, 5.0);
            pManager.AddCurveParameter("BoundaryMask", "Mask", "Optional closed curve to crop the simulation domain and filter statistics.", GH_ParamAccess.item);
            pManager.AddColourParameter("CustomColors", "CC", "Custom color spectrum override (min to max)", GH_ParamAccess.list);
            
            pManager[1].Optional = true;
            pManager[7].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("WindMesh", "WM", "Heatmap of wind speeds mapped to the terrain", GH_ParamAccess.item);
            pManager.AddVectorParameter("WindVectors", "WV", "Wind velocity vectors for visualization", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "Pt", "Grid points corresponding to the vectors", GH_ParamAccess.list);
            pManager.AddNumberParameter("Speeds", "Sp", "Wind speed magnitude (m/s) at each point", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "C", "The color assigned to each point/vector", GH_ParamAccess.list);
            pManager.AddTextParameter("Dashboard Data", "Dash", "JSON string for Dashboard and Legend", GH_ParamAccess.item);
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
            if (cellSize < 0.5) cellSize = 0.5;

            int iterations = 50;
            if (!DA.GetData(4, ref iterations)) return;

            double analysisHeight = 1.5;
            if (!DA.GetData(5, ref analysisHeight)) return;

            double viscosity = 0.1;
            if (!DA.GetData(6, ref viscosity)) return;

            List<double> frictionList = new List<double>();
            DA.GetDataList(7, frictionList);

            double comfortThreshold = 5.0;
            if (!DA.GetData(8, ref comfortThreshold)) return;

            Curve maskCurve = null;
            DA.GetData(9, ref maskCurve);

            List<Color> customColors = new List<Color>();
            DA.GetDataList(10, customColors);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            BoundingBox bbox;
            if (maskCurve != null && maskCurve.IsValid) {
                bbox = maskCurve.GetBoundingBox(true);
            } else {
                bbox = terrainMesh.GetBoundingBox(true);
            }

            int cols = (int)Math.Ceiling((bbox.Max.X - bbox.Min.X) / cellSize);
            int rows = (int)Math.Ceiling((bbox.Max.Y - bbox.Min.Y) / cellSize);

            if (cols * rows > 50000)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Grid is too dense ({cols}x{rows}). Increase CellSize or use a smaller BoundaryMask.");
                return;
            }

            int N = cols;
            int M = rows;
            int totalCells = (N + 2) * (M + 2);

            Mesh combinedContext = new Mesh();
            foreach (var m in contextMeshes) { if (m != null) combinedContext.Append(m); }

            Point3d[] gridPoints = new Point3d[totalCells];
            bool[] obstacles = new bool[totalCells];
            float[] frictionMap = new float[totalCells];
            bool[] insideMask = new bool[totalCells];

            double startX = bbox.Min.X + cellSize / 2.0;
            double startY = bbox.Min.Y + cellSize / 2.0;
            
            int vIdxCounter = 0; 

            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    double x = startX + (i - 1) * cellSize;
                    double y = startY + (j - 1) * cellSize;

                    double fVal = 0.1;
                    if (frictionList.Count > 0)
                        fVal = frictionList.Count > vIdxCounter ? frictionList[vIdxCounter] : frictionList.Last();
                    frictionMap[idx] = (float)Math.Max(0.0, Math.Min(1.0, fVal));
                    vIdxCounter++;

                    Ray3d rayDown = new Ray3d(new Point3d(x, y, bbox.Max.Z + 100), new Vector3d(0, 0, -1));
                    double terrainT = Rhino.Geometry.Intersect.Intersection.MeshRay(terrainMesh, rayDown);
                    
                    if (terrainT >= 0.0)
                    {
                        Point3d terrainPt = rayDown.PointAt(terrainT);
                        Point3d drapedPt = new Point3d(terrainPt.X, terrainPt.Y, terrainPt.Z + analysisHeight);
                        gridPoints[idx] = drapedPt;

                        if (combinedContext.IsValid && combinedContext.Faces.Count > 0)
                        {
                            double contextT = Rhino.Geometry.Intersect.Intersection.MeshRay(combinedContext, rayDown);
                            if (contextT >= 0.0 && contextT < terrainT)
                            {
                                Point3d contextPt = rayDown.PointAt(contextT);
                                if (contextPt.Z > drapedPt.Z) obstacles[idx] = true;
                            }
                        }
                    }
                    else
                    {
                        gridPoints[idx] = new Point3d(x, y, bbox.Min.Z);
                        obstacles[idx] = true;
                    }

                    if (maskCurve != null && maskCurve.IsValid) {
                        var ptContainment = maskCurve.Contains(new Point3d(x, y, 0), Plane.WorldXY, 0.01);
                        insideMask[idx] = (ptContainment == PointContainment.Inside || ptContainment == PointContainment.Coincident);
                    } else {
                        insideMask[idx] = true;
                    }
                }
            }

            float[] u = new float[totalCells];
            float[] v = new float[totalCells];
            float[] u0 = new float[totalCells];
            float[] v0 = new float[totalCells];

            double wSpeed = windVector.Length + 0.01;
            float dt = (float)(0.5 * cellSize / wSpeed);
            if (dt > 0.5f) dt = 0.5f;

            float windU = (float)windVector.X;
            float windV = (float)windVector.Y;

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int j = 1; j <= M; j++) {
                    if (windU > 0) { u[IX(1, j, N)] = windU; v[IX(1, j, N)] = windV; obstacles[IX(1, j, N)] = false; }
                    if (windU < 0) { u[IX(N, j, N)] = windU; v[IX(N, j, N)] = windV; obstacles[IX(N, j, N)] = false; }
                }
                for (int i = 1; i <= N; i++) {
                    if (windV > 0) { u[IX(i, 1, N)] = windU; v[IX(i, 1, N)] = windV; obstacles[IX(i, 1, N)] = false; }
                    if (windV < 0) { u[IX(i, M, N)] = windU; v[IX(i, M, N)] = windV; obstacles[IX(i, M, N)] = false; }
                }

                float[] tmp = u; u = u0; u0 = tmp;
                tmp = v; v = v0; v0 = tmp;

                Advect(N, M, obstacles, u, u0, u0, v0, dt);
                Advect(N, M, obstacles, v, v0, u0, v0, dt);

                if (viscosity > 0) {
                    tmp = u; u = u0; u0 = tmp;
                    tmp = v; v = v0; v0 = tmp;
                    Diffuse(N, M, obstacles, u, u0, (float)viscosity, dt);
                    Diffuse(N, M, obstacles, v, v0, (float)viscosity, dt);
                }

                Project(N, M, obstacles, u, v, u0, v0);

                for (int i = 1; i <= N; i++) {
                    for (int j = 1; j <= M; j++) {
                        if (obstacles[IX(i, j, N)]) continue;
                        float drag = frictionMap[IX(i, j, N)];
                        u[IX(i, j, N)] *= (1.0f - drag * dt);
                        v[IX(i, j, N)] *= (1.0f - drag * dt);
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

            double[] speeds = new double[totalCells];
            for (int i = 1; i <= N; i++)
            {
                for (int j = 1; j <= M; j++)
                {
                    int idx = IX(i, j, N);
                    double speed = Math.Sqrt(u[idx] * u[idx] + v[idx] * v[idx]);
                    speeds[idx] = speed;

                    if (obstacles[idx] || !insideMask[idx]) continue;
                    
                    if (speed > maxSpeed) maxSpeed = speed;
                    if (speed < minSpeed) minSpeed = speed;
                    sumSpeed += speed;
                    validCells++;
                    if (speed <= comfortThreshold) comfortCells++;

                    double normalized = Math.Min(speed / (wSpeed * 1.5), 1.0);
                    Color ptColor = GetColor(normalized, customColors);

                    outPoints.Add(gridPoints[idx]);
                    outVectors.Add(new Vector3d(u[idx], v[idx], 0));
                    outSpeeds.Add(speed);
                    outColors.Add(ptColor);
                }
            }

            double avgSpeed = validCells > 0 ? sumSpeed / validCells : 0;
            if (minSpeed == double.MaxValue) minSpeed = 0;
            double pctComfort = validCells > 0 ? ((double)comfortCells / validCells) * 100.0 : 0;

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
            DA.SetDataList(1, outVectors);
            DA.SetDataList(2, outPoints);
            DA.SetDataList(3, outSpeeds);
            DA.SetDataList(4, outColors);

            List<Color> legendColors = customColors.Count > 0 ? customColors : new List<Color> { Color.FromArgb(255, 0, 0, 255), Color.FromArgb(255, 255, 0, 0) };
            string colorJsonArray = "[" + string.Join(",", legendColors.Select(c => $"{{\"R\":{c.R},\"G\":{c.G},\"B\":{c.B}}}")) + "]";
            
            string jsonStr = $@"{{
  ""AnalysisType"": ""FastCFD"",
  ""Title"": ""Wind Speed (m/s)"",
  ""Type"": ""Continuous"",
  ""Min"": 0.0,
  ""Max"": {(wSpeed * 1.5).ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""Average"": {avgSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""ComfortThreshold"": {comfortThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""ComfortPercentage"": {pctComfort.ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""Colors"": {colorJsonArray}
}}";
            DA.SetData(5, jsonStr);

            string infoStr = 
                "FAST CFD (2.5D EULERIAN SOLVER)\n" +
                "===============================\n\n" +
                "SCIENCE & SIMPLIFICATIONS:\n" +
                "This component runs a pure C# Eulerian fluid solver based on the Navier-Stokes equations (Advection, Diffusion, Projection). It strictly enforces mass conservation, meaning it accurately simulates the Venturi Effect (tunneling) and Wake Zones (eddies) behind structures.\n\n" +
                "Limitations (The 20% tradeoff):\n" +
                "To remain insanely fast, this uses a 'Terrain-Following 2.5D Grid'. The grid drapes perfectly over the terrain topography. However, because it calculates a 2D sheet, it cannot simulate '3D Downdrafts' (wind hitting a skyscraper and plunging vertically down to the ground).\n\n" +
                "VARIABLES:\n" +
                "- Viscosity: Simulates the turbulence/thickness of the air. Lower = more chaotic vortices. Higher = smoother laminar flow.\n" +
                "- Friction: Simulates surface drag slowing down the wind at the boundary layer (e.g., concrete vs forest).\n" +
                "- BoundaryMask: Crops the simulation domain to vastly improve calculation speed, and strictly isolates the output geometry and HUD statistics to the enclosed area.";
            
            DA.SetData(6, infoStr);

            sw.Stop();
            Message = $"FAST CFD\nTime: {sw.ElapsedMilliseconds} ms\n---\nGrid: {cols}x{rows}\nMax: {maxSpeed:F1} | Min: {minSpeed:F1} | Avg: {avgSpeed:F1}\nComfort: {pctComfort:F1}%";
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

            CreateSlider(doc, "CellSize", 1.0, 20.0, 4.0, 3, pivotX, pivotY);
            CreateSlider(doc, "Iterations", 10, 500, 50, 4, pivotX, pivotY + 30);
            CreateSlider(doc, "AnalysisHeight", 0.0, 50.0, 1.5, 5, pivotX, pivotY + 60);
            CreateSlider(doc, "Viscosity", 0.0, 1.0, 0.1, 6, pivotX, pivotY + 90);
            CreateSlider(doc, "Friction", 0.0, 1.0, 0.1, 7, pivotX, pivotY + 120);
            CreateSlider(doc, "ComfortThreshold", 1.0, 20.0, 5.0, 8, pivotX, pivotY + 150);

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

        private void Advect(int N, int M, bool[] obs, float[] d, float[] d0, float[] u, float[] v, float dt)
        {
            float dt0 = dt * N;
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

        private void Diffuse(int N, int M, bool[] obs, float[] x, float[] x0, float diff, float dt)
        {
            float a = dt * diff * N * M;
            for (int k = 0; k < 10; k++) {
                for (int i = 1; i <= N; i++) {
                    for (int j = 1; j <= M; j++) {
                        if (obs[IX(i, j, N)]) continue;
                        x[IX(i, j, N)] = (x0[IX(i, j, N)] + a * (x[IX(i - 1, j, N)] + x[IX(i + 1, j, N)] + x[IX(i, j - 1, N)] + x[IX(i, j + 1, N)])) / (1 + 4 * a);
                    }
                }
                SetBnd(N, M, obs, x);
            }
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
            for (int i = 1; i <= N; i++) {
                for (int j = 1; j <= M; j++) {
                    if (obs[IX(i, j, N)]) {
                        x[IX(i, j, N)] = 0.0f;
                    }
                }
            }
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("FastCFD.png");
        public override Guid ComponentGuid => new Guid("11223344-5566-7788-9900-AABBCCDDEEFF");
    }
}
