import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# Fix array out of bounds in SafeFilletPolyline
old_code = """                    Point3d nextNext = p[(i + 2) % (isClosed ? count : count + 1)]; // Careful with open array
                    if (isClosed || i + 2 < p.Length)
                    {
                        nextNext = p[i + 2];"""

new_code = """                    Point3d nextNext = p[(i + 2) % (isClosed ? count : count + 1)];
                    if (!isClosed && i + 2 < p.Length)
                    {
                        nextNext = p[i + 2];
                    }
                    if (isClosed)
                    {
                        nextNext = p[(i + 2) % count];"""

content = content.replace(old_code, new_code)

# Fix Delaunay mesh vertex mapping out of bounds
# If Solve_Mesh removes points, newTerrain.Vertices.Count != pts.Count. 
# We should map Z by doing:
# newTerrain.Vertices[i] = new Point3f(..., newTerrain.Vertices[i].Y, 0) is wrong.
# Actually Solve_Mesh returns vertices with Z=0. We can just project them back to the original pts list by matching XY, OR we just trust Delaunay if we used Solve_Connectivity.
# Let's fix the Delaunay vertex Z mapping to be robust.

old_del = """                Mesh newTerrain = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 1e-6, ref faces_placeholder);
                
                if (newTerrain != null)
                {
                    for (int i = 0; i < newTerrain.Vertices.Count; i++) {
                        newTerrain.Vertices[i] = new Point3f((float)pts[i].X, (float)pts[i].Y, (float)pts[i].Z);
                    }"""

new_del = """                Mesh newTerrain = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 1e-6, ref faces_placeholder);
                
                if (newTerrain != null)
                {
                    // Solve_Mesh can reorder or drop duplicate vertices, so we can't assume 1:1 mapping with 'pts'.
                    // Let's use a spatial hash or just a simple closest point to recover Z
                    for (int i = 0; i < newTerrain.Vertices.Count; i++) {
                        var nv = newTerrain.Vertices[i];
                        float bestZ = 0;
                        float minDist = float.MaxValue;
                        for (int j = 0; j < pts.Count; j++) {
                            float dx = (float)pts[j].X - nv.X;
                            float dy = (float)pts[j].Y - nv.Y;
                            float dsq = dx*dx + dy*dy;
                            if (dsq < minDist) { minDist = dsq; bestZ = (float)pts[j].Z; }
                            if (minDist < 1e-5) break;
                        }
                        newTerrain.Vertices[i] = new Point3f(nv.X, nv.Y, bestZ);
                    }"""
content = content.replace(old_del, new_del)

# Fix rMesh Z mapping
old_rmesh = """                if (rMesh != null)
                {
                    for (int i = 0; i < rMesh.Vertices.Count; i++)
                    {
                        rMesh.Vertices[i] = new Point3f((float)boundaryPts[i].X, (float)boundaryPts[i].Y, (float)boundaryPts[i].Z);
                    }"""

new_rmesh = """                if (rMesh != null)
                {
                    for (int i = 0; i < rMesh.Vertices.Count; i++)
                    {
                        var nv = rMesh.Vertices[i];
                        float bestZ = 0;
                        float minDist = float.MaxValue;
                        for (int j = 0; j < boundaryPts.Count; j++) {
                            float dx = (float)boundaryPts[j].X - nv.X;
                            float dy = (float)boundaryPts[j].Y - nv.Y;
                            float dsq = dx*dx + dy*dy;
                            if (dsq < minDist) { minDist = dsq; bestZ = (float)boundaryPts[j].Z; }
                            if (minDist < 1e-5) break;
                        }
                        rMesh.Vertices[i] = new Point3f(nv.X, nv.Y, bestZ);
                    }"""
content = content.replace(old_rmesh, new_rmesh)

# Fix facesToDelete in terrain using newTerrain.Vertices
old_del_faces = """                        var pA = pts[f.A];
                        var pB = pts[f.B];
                        var pC = pts[f.C];"""
new_del_faces = """                        var pA = newTerrain.Vertices[f.A];
                        var pB = newTerrain.Vertices[f.B];
                        var pC = newTerrain.Vertices[f.C];"""
content = content.replace(old_del_faces, new_del_faces)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
