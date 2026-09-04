import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

# For Curve:
content = content.replace(
    'Point3d centroid = setoutPt.IsValid ? setoutPt : (amp != null ? amp.Centroid : boundary.PointAtStart);\n                originPlane.Origin = originPlane.ClosestPoint(centroid);',
    'Point3d centroid = amp != null ? amp.Centroid : boundary.PointAtStart;\n                originPlane.Origin = centroid;\n                if (setoutPt.IsValid) originPlane.Origin = originPlane.ClosestPoint(setoutPt);'
)

# For Surface:
content = content.replace(
    'Point3d centroid = setoutPt.IsValid ? setoutPt : (amp != null ? amp.Centroid : srf.PointAt(u, v));\n                originPlane = new Plane(centroid, normal);',
    'Point3d centroid = amp != null ? amp.Centroid : srf.PointAt(u, v);\n                originPlane = new Plane(centroid, normal);\n                if (setoutPt.IsValid) originPlane.Origin = originPlane.ClosestPoint(setoutPt);'
)

# For Brep:
content = content.replace(
    'Point3d centroid = setoutPt.IsValid ? setoutPt : (amp != null ? amp.Centroid : bsrf.PointAt(u, v));\n                originPlane = new Plane(centroid, normal);',
    'Point3d centroid = amp != null ? amp.Centroid : bsrf.PointAt(u, v);\n                originPlane = new Plane(centroid, normal);\n                if (setoutPt.IsValid) originPlane.Origin = originPlane.ClosestPoint(setoutPt);'
)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)

