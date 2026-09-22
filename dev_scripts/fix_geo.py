with open('Components/SunHoursAnalysis.cs', 'r') as f:
    content = f.read()

# Fix Inputs to Geometry
inputs_orig = """pManager.AddBrepParameter("Massing", "Massing", "(Workflow 1) Low LOD: 3D Massing boxes or terrain to auto-subdivide.", GH_ParamAccess.list);"""
inputs_new = """pManager.AddGeometryParameter("Geometry", "Geo", "(Workflow 1) Low LOD: 3D Massing (Breps) or Terrain (Meshes) to analyze.", GH_ParamAccess.list);"""
content = content.replace(inputs_orig, inputs_new)

# Fix SolveInstance
solve_orig = """            List<Point3d> testPoints = new List<Point3d>();
            List<Brep> massings = new List<Brep>();
            double gridSize = 2.0;
            List<Mesh> context = new List<Mesh>();
            List<Vector3d> vectors = new List<Vector3d>();

            DA.GetDataList(0, testPoints);
            DA.GetDataList(1, massings);"""

solve_new = """            List<Point3d> testPoints = new List<Point3d>();
            List<Grasshopper.Kernel.Types.IGH_GeometricGoo> geos = new List<Grasshopper.Kernel.Types.IGH_GeometricGoo>();
            double gridSize = 2.0;
            List<Mesh> context = new List<Mesh>();
            List<Vector3d> vectors = new List<Vector3d>();

            DA.GetDataList(0, testPoints);
            DA.GetDataList(1, geos);"""
content = content.replace(solve_orig, solve_new)

# Fix meshing loop
loop_orig = """                foreach (Brep b in massings)
                {
                    if (b == null || !b.IsValid) continue;
                    Mesh[] ms = Mesh.CreateFromBrep(b, mp);
                    if (ms != null)
                    {
                        foreach (Mesh m in ms) displayMesh.Append(m);
                    }
                }"""

loop_new = """                foreach (var goo in geos)
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
                }"""
content = content.replace(loop_orig, loop_new)

# Fix isWorkflow1 condition
content = content.replace("massings.Count > 0", "geos.Count > 0")

with open('Components/SunHoursAnalysis.cs', 'w') as f:
    f.write(content)
