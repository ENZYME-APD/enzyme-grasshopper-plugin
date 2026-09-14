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
              "Multi-threaded raycaster. Analyzes arbitrary 3D massing or custom points for sun exposure.",
              "Enzyme", "Site Analysis")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Test Points", "Pts", "(Workflow 2) High LOD: Specific points to test (e.g., window centroids). Overrides Massing.", GH_ParamAccess.list);
            pManager.AddGeometryParameter("Geometry", "Geo", "(Workflow 1) Low LOD: 3D Massing (Breps) or Terrain (Meshes) to analyze.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Grid Size", "Grid", "Subdivision size for auto-meshing the Massing.", GH_ParamAccess.item, 2.0);
            pManager.AddMeshParameter("Context", "Context", "Environment geometry (buildings, terrain) as shadow casters.", GH_ParamAccess.list);
            pManager.AddVectorParameter("Sun Vectors", "Vectors", "Solar vectors from the Heliodon.", GH_ParamAccess.list);
            pManager.AddColourParameter("Gradient", "Gradient", "Optional custom color gradient (list of colors from 0% to 100%).", GH_ParamAccess.list);
            
            pManager[0].Optional = true;
            
            pManager[5].Optional = true;
            
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Sun Hours", "Hours", "Total number of sun rays that reached the point.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Exposure %", "Exposure", "Percentage of time in the sun (0.0 to 1.0).", GH_ParamAccess.list);
            pManager.AddPointParameter("Points Out", "Pts", "The analyzed points.", GH_ParamAccess.list);
            pManager.AddMeshParameter("Analysis Mesh", "Mesh", "The auto-subdivided Massing mesh (for easy gradient coloring).", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "Colors", "Color mapped to each point based on exposure.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> testPoints = new List<Point3d>();
            List<Grasshopper.Kernel.Types.IGH_GeometricGoo> geos = new List<Grasshopper.Kernel.Types.IGH_GeometricGoo>();
            double gridSize = 2.0;
            List<Mesh> context = new List<Mesh>();
            List<Vector3d> vectors = new List<Vector3d>();

            DA.GetDataList(0, testPoints);
            DA.GetDataList(1, geos);
            DA.GetData(2, ref gridSize);
            DA.GetDataList(3, context); // Optional
            if (!DA.GetDataList(4, vectors)) return;
            
            List<System.Drawing.Color> customColors = new List<System.Drawing.Color>();
            DA.GetDataList(5, customColors);

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

            // WORKFLOW 1: Auto-Subdivide Massing if no points are provided
            if ((testPoints == null || testPoints.Count == 0) && geos.Count > 0)
            {
                isWorkflow1 = true;
                if (gridSize < 0.1) gridSize = 0.1;

                MeshingParameters mp = new MeshingParameters();
                mp.MaximumEdgeLength = gridSize;
                // mp.MinimumEdgeLength = gridSize * 0.5; // Optional constraint
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
                        // For terrain meshes, we just evaluate their existing faces to save massive remeshing overhead,
                        // unless we want to do a top-down grid. For now, appending is fastest and safest for colors.
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

            if (testPoints == null || testPoints.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Please provide either Test Points (Detailed) or a Massing (Boxy) to analyze.");
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
                bool hasNormal = (isWorkflow1 && normals.Count == testPoints.Count);
                Vector3d normal = hasNormal ? normals[i] : Vector3d.Unset;
                
                int hits = 0;

                foreach (Vector3d vec in vectors)
                {
                    // 1. Self-Shading Optimization (Workflow 1)
                    // If the sun vector (pointing TO sun) is behind the face normal, it's immediately shaded.
                    if (hasNormal)
                    {
                        if (Vector3d.Multiply(normal, vec) <= 0.001)
                        {
                            continue; // Self-shaded, no raycast needed
                        }
                    }

                    // Shift the point slightly along the vector to prevent self-intersection
                    Ray3d ray = new Ray3d(pt + (vec * 0.01), vec);
                    bool isShaded = false;

                    // 2. Check Context Geometry
                    for (int m = 0; m < contextMeshes.Length; m++)
                    {
                        if (contextMeshes[m] == null) continue;
                        
                        if (Rhino.Geometry.Intersect.Intersection.MeshRay(contextMeshes[m], ray) >= 0.0)
                        {
                            isShaded = true;
                            break;
                        }
                    }

                    // 3. Check Massing itself (so Building A shades Building B in Workflow 1)
                    if (!isShaded && isWorkflow1 && displayMesh.IsValid)
                    {
                        if (Rhino.Geometry.Intersect.Intersection.MeshRay(displayMesh, ray) >= 0.0)
                        {
                            isShaded = true;
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

            System.Drawing.Color[] outColors = new System.Drawing.Color[testPoints.Count];
            Parallel.For(0, testPoints.Count, i => {
                outColors[i] = InterpolateColor(customColors, exposures[i]);
            });

            sw.Stop();

            DA.SetDataList(0, sunHits);
            DA.SetDataList(1, exposures);
            DA.SetDataList(2, testPoints);
            
            if (isWorkflow1 && displayMesh.IsValid)
            {
                DA.SetData(3, displayMesh);
            }
            
            DA.SetDataList(4, outColors);

            Message = $"Points: {testPoints.Count:N0}\\nRays: {testPoints.Count * totalRays:N0}\\nTime: {sw.ElapsedMilliseconds} ms";
        }

        private System.Drawing.Color InterpolateColor(List<System.Drawing.Color> gradient, double t)
        {
            if (gradient == null || gradient.Count == 0) return System.Drawing.Color.White;
            if (gradient.Count == 1) return gradient[0];
            
            t = Math.Max(0.0, Math.Min(1.0, t));
            double scaledT = t * (gradient.Count - 1);
            int idx1 = (int)Math.Floor(scaledT);
            int idx2 = (int)Math.Ceiling(scaledT);
            if (idx1 >= gradient.Count - 1) return gradient[gradient.Count - 1];
            if (idx1 == idx2) return gradient[idx1];
            
            double blend = scaledT - idx1;
            System.Drawing.Color c1 = gradient[idx1];
            System.Drawing.Color c2 = gradient[idx2];
            
            int r = (int)(c1.R + (c2.R - c1.R) * blend);
            int g = (int)(c1.G + (c2.G - c1.G) * blend);
            int b = (int)(c1.B + (c2.B - c1.B) * blend);
            
            return System.Drawing.Color.FromArgb(255, r, g, b);
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("SunHoursAnalysis.png");

        
        public override GH_Exposure Exposure => GH_Exposure.secondary;
public override Guid ComponentGuid => new Guid("C4F812D3-A42E-4D7F-8C9B-9A3E4F5D6C11");
    }
}
