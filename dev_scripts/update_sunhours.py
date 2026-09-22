import re

with open("Components/SunHoursAnalysis.cs", "r") as f:
    content = f.read()

# Add Offset Input
if 'pManager.AddNumberParameter("Offset"' not in content:
    content = content.replace('pManager.AddBooleanParameter("Run", "Run", "Trigger the analysis.", GH_ParamAccess.item, true);',
        'pManager.AddBooleanParameter("Run", "Run", "Trigger the analysis.", GH_ParamAccess.item, true);\n            pManager.AddNumberParameter("Offset", "Offset", "Offset distance for output points.", GH_ParamAccess.item, 0.1);\n            pManager[6].Optional = true;')

# Fix Outputs
if 'pManager.AddTextParameter("Dashboard Data"' not in content:
    content = content.replace('pManager.AddMeshParameter("Analysis Mesh", "Mesh", "The auto-subdivided Massing mesh (for easy gradient coloring).", GH_ParamAccess.item);',
        'pManager.AddMeshParameter("Analysis Mesh", "Mesh", "The auto-subdivided Massing mesh (for easy gradient coloring).", GH_ParamAccess.item);\n            pManager.AddColourParameter("Colors", "Colors", "Color mapped to each point.", GH_ParamAccess.list);\n            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON legend data", GH_ParamAccess.item);')

# Rewrite SolveInstance caching fields to include colors and json
cache_fields_old = """        private List<int> _cachedHits = new List<int>();
        private List<double> _cachedExposures = new List<double>();
        private List<Point3d> _cachedPoints = new List<Point3d>();
        private Mesh _cachedMesh = new Mesh();
        private long _cachedTime = 0;
        private int _cachedRays = 0;"""
cache_fields_new = """        private List<int> _cachedHits = new List<int>();
        private List<double> _cachedExposures = new List<double>();
        private List<Point3d> _cachedPoints = new List<Point3d>();
        private Mesh _cachedMesh = new Mesh();
        private List<System.Drawing.Color> _cachedColors = new List<System.Drawing.Color>();
        private string _cachedJson = "";
        private long _cachedTime = 0;
        private int _cachedRays = 0;"""
content = content.replace(cache_fields_old, cache_fields_new)

# Now we rewrite the solve instance logic
start_idx = content.find("protected override void SolveInstance(IGH_DataAccess DA)")
end_idx = content.find("private System.Drawing.Color InterpolateColor")

new_solve = """        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> testPoints = new List<Point3d>();
            List<Grasshopper.Kernel.Types.IGH_GeometricGoo> geos = new List<Grasshopper.Kernel.Types.IGH_GeometricGoo>();
            double gridSize = 2.0;
            List<Mesh> context = new List<Mesh>();
            List<Vector3d> vectors = new List<Vector3d>();

            DA.GetDataList(0, testPoints);
            DA.GetDataList(1, geos);
            DA.GetData(2, ref gridSize);
            DA.GetDataList(3, context); 
            if (!DA.GetDataList(4, vectors)) return;
            
            List<System.Drawing.Color> customColors = new List<System.Drawing.Color>();
            DA.GetDataList(5, customColors);

            bool run = true;
            DA.GetData(6, ref run);

            double offset = 0.1;
            DA.GetData(7, ref offset);

            if (!run)
            {
                if (_cachedHits.Count > 0)
                {
                    DA.SetDataList(0, _cachedHits);
                    DA.SetDataList(1, _cachedExposures);
                    DA.SetDataList(2, _cachedPoints);
                    if (_cachedMesh != null && _cachedMesh.IsValid) DA.SetData(3, _cachedMesh);
                    DA.SetDataList(4, _cachedColors);
                    DA.SetData(5, _cachedJson);
                    Message = $"Sun Hours\\n{_cachedTime} ms (Cached)\\n---\\nPoints: {_cachedPoints.Count}\\nRays: {_cachedRays}";
                }
                else
                {
                    Message = "Paused";
                }
                return;
            }

            if (vectors.Count == 0) return;

            if (customColors.Count == 0)
            {
                customColors = new List<System.Drawing.Color> { 
                    System.Drawing.Color.FromArgb(255, 0, 0, 139), // DarkBlue
                    System.Drawing.Color.FromArgb(255, 0, 255, 255), // Cyan
                    System.Drawing.Color.FromArgb(255, 255, 255, 0), // Yellow
                    System.Drawing.Color.FromArgb(255, 255, 0, 0) // Red
                };
            }

            Mesh displayMesh = new Mesh();
            List<Vector3d> normals = new List<Vector3d>();
            bool isWorkflow1 = false;

            if ((testPoints == null || testPoints.Count == 0) && geos.Count > 0)
            {
                isWorkflow1 = true;
                if (gridSize < 0.1) gridSize = 0.1;

                MeshingParameters mp = new MeshingParameters();
                mp.MaximumEdgeLength = gridSize;
                mp.GridAspectRatio = 1.0;

                foreach (var goo in geos)
                {
                    if (goo == null) continue;
                    GeometryBase geo = goo.ScriptVariable() as GeometryBase;
                    
                    if (geo is Brep b)
                    {
                        Mesh[] ms = Mesh.CreateFromBrep(b, mp);
                        if (ms != null)
                        {
                            foreach (Mesh m in ms) displayMesh.Append(m);
                        }
                    }
                    else if (geo is Mesh m)
                    {
                        displayMesh.Append(m);
                    }
                }

                displayMesh.Compact();
                displayMesh.FaceNormals.ComputeFaceNormals();

                testPoints = new List<Point3d>();
                for (int i = 0; i < displayMesh.Faces.Count; i++)
                {
                    testPoints.Add(displayMesh.Faces.GetFaceCenter(i));
                    normals.Add(displayMesh.FaceNormals[i]);
                }
            }
            else
            {
                // Ensure normals array matches testPoints count
                for (int i = 0; i < testPoints.Count; i++) normals.Add(Vector3d.ZAxis);
            }

            if (testPoints == null || testPoints.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide either Test Points (Detailed) or a Massing (Boxy) to analyze.");
                return;
            }

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            Mesh combinedContext = new Mesh();
            foreach (Mesh cm in context)
            {
                if (cm != null && cm.IsValid) combinedContext.Append(cm);
            }
            if (isWorkflow1 && displayMesh.IsValid)
            {
                combinedContext.Append(displayMesh);
            }
            combinedContext.Compact();
            
            int[] sunHits = new int[testPoints.Count];
            double[] exposures = new double[testPoints.Count];
            Point3d[] offsetPoints = new Point3d[testPoints.Count];
            int totalRays = vectors.Count;

            System.Threading.Tasks.Parallel.For(0, testPoints.Count, i =>
            {
                Point3d pt = testPoints[i];
                Vector3d normal = normals[i];
                offsetPoints[i] = pt + (normal * offset);
                
                int hits = 0;

                foreach (Vector3d vec in vectors)
                {
                    if (isWorkflow1 && Vector3d.Multiply(normal, vec) <= 0.001)
                    {
                        continue;
                    }

                    Ray3d ray = new Ray3d(pt + (vec * 0.01), vec);
                    bool isShaded = false;

                    if (combinedContext.IsValid && Rhino.Geometry.Intersect.Intersection.MeshRay(combinedContext, ray) >= 0.0)
                    {
                        isShaded = true;
                    }

                    if (!isShaded)
                    {
                        hits++;
                    }
                }

                sunHits[i] = hits;
                exposures[i] = (double)hits / totalRays;
            });

            System.Drawing.Color[] outColors = new System.Drawing.Color[testPoints.Count];
            System.Threading.Tasks.Parallel.For(0, testPoints.Count, i => {
                outColors[i] = InterpolateColor(customColors, exposures[i]);
            });

            if (isWorkflow1 && displayMesh.IsValid)
            {
                displayMesh.Unweld(0.0, true);
                displayMesh.VertexColors.CreateMonotoneMesh(System.Drawing.Color.White);
                
                for (int i = 0; i < displayMesh.Faces.Count; i++)
                {
                    if (i >= outColors.Length) break;
                    var face = displayMesh.Faces[i];
                    displayMesh.VertexColors[face.A] = outColors[i];
                    displayMesh.VertexColors[face.B] = outColors[i];
                    displayMesh.VertexColors[face.C] = outColors[i];
                    if (face.IsQuad)
                    {
                        displayMesh.VertexColors[face.D] = outColors[i];
                    }
                }
            }

            sw.Stop();
            
            var jColors = new Newtonsoft.Json.Linq.JArray();
            foreach (var c in customColors) jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
            
            var legendObj = new Newtonsoft.Json.Linq.JObject
            {
                ["Type"] = "Blocks",
                ["Title"] = "Sun Exposure",
                ["Colors"] = jColors,
                ["Labels"] = new Newtonsoft.Json.Linq.JArray("0 Hours", $"{totalRays} Hours"),
                ["SubLabels"] = new Newtonsoft.Json.Linq.JArray("Annual Exposure")
            };
            string jsonOut = legendObj.ToString();

            _cachedHits = new List<int>(sunHits);
            _cachedExposures = new List<double>(exposures);
            _cachedPoints = new List<Point3d>(offsetPoints);
            _cachedMesh = isWorkflow1 ? displayMesh : null;
            _cachedColors = new List<System.Drawing.Color>(outColors);
            _cachedJson = jsonOut;
            _cachedTime = sw.ElapsedMilliseconds;
            _cachedRays = totalRays;

            Message = $"Sun Hours\\n{_cachedTime} ms\\n---\\nPoints: {_cachedPoints.Count}\\nRays: {_cachedRays}";

            DA.SetDataList(0, _cachedHits);
            DA.SetDataList(1, _cachedExposures);
            DA.SetDataList(2, _cachedPoints);
            if (isWorkflow1 && displayMesh.IsValid) DA.SetData(3, displayMesh);
            DA.SetDataList(4, _cachedColors);
            DA.SetData(5, _cachedJson);
        }
"""
if start_idx != -1 and end_idx != -1:
    new_content = content[:start_idx] + new_solve + "        " + content[end_idx:]
    with open("Components/SunHoursAnalysis.cs", "w") as f:
        f.write(new_content)
