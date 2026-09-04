with open("Components/LegendGeometry.cs", "r") as f:
    ts = f.read()

# Fix Normals
old_mesh_code = '''                    var mesh = new Rhino.Geometry.Mesh();
                    mesh.Vertices.Add(pl[0]);
                    mesh.Vertices.Add(pl[1]);
                    mesh.Vertices.Add(pl[2]);
                    mesh.Vertices.Add(pl[3]);
                    mesh.Faces.AddFace(0, 1, 2, 3);
                    m_displayMeshes.Add(mesh);'''

new_mesh_code = '''                    var mesh = new Rhino.Geometry.Mesh();
                    mesh.Vertices.Add(pl[0]);
                    mesh.Vertices.Add(pl[1]);
                    mesh.Vertices.Add(pl[2]);
                    mesh.Vertices.Add(pl[3]);
                    mesh.Faces.AddFace(0, 1, 2, 3);
                    mesh.Normals.ComputeNormals();
                    mesh.VertexColors.CreateMonotoneMesh(result.Colors[i]);
                    m_displayMeshes.Add(mesh);'''

ts = ts.replace(old_mesh_code, new_mesh_code)

with open("Components/LegendGeometry.cs", "w") as f:
    f.write(ts)
