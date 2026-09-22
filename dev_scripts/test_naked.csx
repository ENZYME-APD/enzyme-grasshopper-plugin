using System;
using Rhino.Geometry;

Mesh mesh = new Mesh();
mesh.Vertices.Add(0, 0, 0);
mesh.Vertices.Add(1, 0, 0);
mesh.Vertices.Add(1, 1, 0);
mesh.Vertices.Add(0, 1, 0);
mesh.Faces.AddFace(0, 1, 2, 3);
bool[] naked = mesh.GetNakedEdgePointStatus();
Console.WriteLine(naked.Length);
