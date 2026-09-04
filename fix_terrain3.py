with open("Components/TerrainSections.cs", "r") as f:
    ts = f.read()

import re

ts = re.sub(r'Plane cutPlaneYDir = new Plane\(origin, rotPlane\.YAxis, rotPlane\.ZAxis\);\s*Transform xformToWorld = Transform\.PlaneToPlane\(cutPlaneYDir, Plane\.WorldXY\);',
'''Plane cutPlaneYDir = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);
                                    Plane targetPlaneY = Plane.WorldXY;
                                    targetPlaneY.Rotate(Math.PI / 2, Rhino.Geometry.Vector3d.ZAxis);
                                    Transform xformToWorld = Transform.PlaneToPlane(cutPlaneYDir, targetPlaneY);''', ts)

with open("Components/TerrainSections.cs", "w") as f:
    f.write(ts)
