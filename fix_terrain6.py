import re

with open("Components/TerrainSections.cs", "r") as f:
    ts = f.read()

# Fix 1: The X-loop stepping
# Current: Point3d origin = rotPlane.PointAt(localBox.X.Min + stepX * i, localBox.Y.Mid, localBox.Z.Mid);
# Needs to be: Point3d origin = rotPlane.PointAt(localBox.X.Mid, localBox.Y.Min + stepY * i, localBox.Z.Mid);
# But wait, stepX and stepY are declared as:
# double stepX = 0; double stepY = 0;
# if (sectionsX > 1) stepX = localBox.X.Length / (sectionsX + 1);
# if (sectionsY > 1) stepY = localBox.Y.Length / (sectionsY + 1);
# If we change X-sections to step along Y, we need a stepYForX = localBox.Y.Length / (sectionsX + 1).

# Let's just redefine stepX and stepY carefully.
ts = ts.replace("double stepX = 0;", "double stepYForX = 0;")
ts = ts.replace("double stepY = 0;", "double stepXForY = 0;")

ts = ts.replace("if (sectionsX > 1) stepX = localBox.X.Length / (sectionsX + 1);",
"if (sectionsX > 1) stepYForX = localBox.Y.Length / (sectionsX + 1);")

ts = ts.replace("if (sectionsY > 1) stepY = localBox.Y.Length / (sectionsY + 1);",
"if (sectionsY > 1) stepXForY = localBox.X.Length / (sectionsY + 1);")


ts = ts.replace("Point3d origin = rotPlane.PointAt(localBox.X.Min + stepX * i, localBox.Y.Mid, localBox.Z.Mid);",
"Point3d origin = rotPlane.PointAt(localBox.X.Mid, localBox.Y.Min + stepYForX * i, localBox.Z.Mid);")

ts = ts.replace("Plane cutPlane = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);",
"Plane cutPlane = new Plane(origin, rotPlane.XAxis, rotPlane.ZAxis);")

ts = ts.replace("Point3d origin = rotPlane.PointAt(localBox.X.Mid, localBox.Y.Min + stepY * i, localBox.Z.Mid);",
"Point3d origin = rotPlane.PointAt(localBox.X.Min + stepXForY * i, localBox.Y.Mid, localBox.Z.Mid);")

# We must be careful because the second Plane cutPlane was already rotPlane.XAxis!
# Let's just use exact regex.
