import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

old_code = """                // Run Delaunay
                var nodes = new Node2List();
                foreach (var p in pts) nodes.Append(new Node2(p.X, p.Y));
                var faces = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Connectivity(nodes, 1e-6, false);
                
                Mesh newTerrain = new Mesh();
                foreach (var p in pts) newTerrain.Vertices.Add(p);
                var faceList = faces.GetFaces();
                foreach (var f in faceList)
                {
                    // Filter long edges on boundary
                    var pA = pts[f.A];
                    var pB = pts[f.B];
                    var pC = pts[f.C];
                    
                    if (pA.DistanceTo(pB) > 150 || pB.DistanceTo(pC) > 150 || pC.DistanceTo(pA) > 150)
                        continue;
                        
                    newTerrain.Faces.AddFace(f.A, f.B, f.C);
                }
                newTerrain.Normals.ComputeNormals();"""

new_code = """                // Run Delaunay
                var nodes = new Node2List();
                foreach (var p in pts) nodes.Append(new Node2(p.X, p.Y));
                Mesh newTerrain = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 1e-6, ref faces_placeholder);
                // The above returns a mesh where Z is 0. We need to copy our Z values back.
                for (int i = 0; i < newTerrain.Vertices.Count; i++) {
                    newTerrain.Vertices[i] = new Rhino.Geometry.Point3f((float)pts[i].X, (float)pts[i].Y, (float)pts[i].Z);
                }
                
                // Remove long boundary faces
                var facesToDelete = new List<int>();
                for (int i = 0; i < newTerrain.Faces.Count; i++)
                {
                    var f = newTerrain.Faces[i];
                    var pA = pts[f.A];
                    var pB = pts[f.B];
                    var pC = pts[f.C];
                    
                    if (pA.DistanceTo(pB) > 150 || pB.DistanceTo(pC) > 150 || pC.DistanceTo(pA) > 150)
                        facesToDelete.Add(i);
                }
                newTerrain.Faces.DeleteFaces(facesToDelete);
                newTerrain.Normals.ComputeNormals();"""

content = content.replace(old_code, new_code)
# Add faces_placeholder declaration
content = content.replace("var nodes = new Node2List();", "var nodes = new Node2List();\n                var faces_placeholder = new List<Grasshopper.Kernel.Geometry.Delaunay.FaceEx>();")

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
