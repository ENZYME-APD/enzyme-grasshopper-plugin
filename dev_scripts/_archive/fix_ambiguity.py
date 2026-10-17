import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# Fix Plane ambiguity
content = content.replace("Plane.WorldXY", "Rhino.Geometry.Plane.WorldXY")

# Fix FaceEx -> Face
content = content.replace("List<Grasshopper.Kernel.Geometry.Delaunay.FaceEx>", "List<Grasshopper.Kernel.Geometry.Delaunay.Face>")

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
