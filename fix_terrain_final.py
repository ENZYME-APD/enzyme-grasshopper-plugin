with open("Components/TerrainSections.cs", "r") as f:
    lines = f.readlines()

# For X-sections (lines 169-181), it should be stepping along Y and cutting with XAxis
# Original:
# 169: double stepX = lenX / (sectionsX + 1);
# 172: Point3d origin = rotPlane.PointAt(localBox.X.Min + stepX * i, localBox.Y.Mid, localBox.Z.Mid);
# 173: Plane cutPlane = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);
# We need to change it to step along Y, and cut with XAxis.

lines[168] = "                            double stepYForX = lenY / (sectionsX + 1);\n"
lines[171] = "                                Point3d origin = rotPlane.PointAt(localBox.X.Mid, localBox.Y.Min + stepYForX * i, localBox.Z.Mid);\n"
lines[172] = "                                Plane cutPlane = new Plane(origin, rotPlane.XAxis, rotPlane.ZAxis);\n"

# For Y-sections (lines 247-261), it should be stepping along X and cutting with YAxis
# Original:
# 247: double stepY = lenY / (sectionsY + 1);
# 250: Point3d origin = rotPlane.PointAt(localBox.X.Mid, localBox.Y.Min + stepY * i, localBox.Z.Mid);
# 251: Plane cutPlane = new Plane(origin, rotPlane.XAxis, rotPlane.ZAxis);

lines[246] = "                            double stepXForY = lenX / (sectionsY + 1);\n"
lines[249] = "                                Point3d origin = rotPlane.PointAt(localBox.X.Min + stepXForY * i, localBox.Y.Mid, localBox.Z.Mid);\n"
lines[250] = "                                Plane cutPlane = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);\n"

with open("Components/TerrainSections.cs", "w") as f:
    f.writelines(lines)
