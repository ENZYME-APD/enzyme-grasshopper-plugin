import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# Add missing using
if 'using Grasshopper.Kernel.Geometry;' not in content:
    content = content.replace('using Grasshopper.Kernel.Geometry.Delaunay;', 'using Grasshopper.Kernel.Geometry.Delaunay;\nusing Grasshopper.Kernel.Geometry;')

# Fix Connectivity enumeration
content = content.replace('foreach (var f in faces)', 'var faceList = faces.GetFaces();\n                foreach (var f in faceList)')

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
