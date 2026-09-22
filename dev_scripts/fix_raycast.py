import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

old_block = """                        if (onTerrain)
                        {
                            Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                            if (tL >= 0.0) zLeftT = rL.PointAt(tL).Z;

                            Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                            if (tR >= 0.0) zRightT = rR.PointAt(tR).Z;"""

new_block = """                        if (onTerrain)
                        {
                            Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                            if (tL >= 0.0) zLeftT = rL.PointAt(tL).Z;
                            else zLeftT = zTerrain; // Fallback to center terrain height to prevent 0-width embankments if raycast misses a tiny hole

                            Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                            if (tR >= 0.0) zRightT = rR.PointAt(tR).Z;
                            else zRightT = zTerrain;"""

content = content.replace(old_block, new_block)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
