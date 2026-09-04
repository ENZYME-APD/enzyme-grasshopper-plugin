import re

with open("Components/TerrainSections.cs", "r") as f:
    ts = f.read()

# 1. Update the cursor initialization
ts = ts.replace("double cursorYXSecs = globalBB.IsValid ? globalBB.Min.Y - padding : -padding;",
"double cursorYXSecs = globalBB.IsValid ? globalBB.Max.Y + padding : padding;")

ts = ts.replace("double cursorXYSecs = globalBB.IsValid ? globalBB.Min.X - padding : -padding;",
"double cursorXYSecs = globalBB.IsValid ? globalBB.Max.X + padding : padding;")

# 2. Update X-section translation and stepping (Stacking UPWARDS from Max.Y)
ts = ts.replace("var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorYXSecs - bbFlat.Max.Y, 0));",
"var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorYXSecs - bbFlat.Min.Y, 0));")

ts = ts.replace("cursorYXSecs -= ((bbFlat.Max.Y - bbFlat.Min.Y) + globalBB.Diagonal.Length * 0.05);",
"cursorYXSecs += ((bbFlat.Max.Y - bbFlat.Min.Y) + padding);")
# Also need to make sure padding is used, wait, let's use the explicit padding variable which is already in scope.
ts = ts.replace("globalBB.Diagonal.Length * 0.05", "padding")

# 3. Update Y-section mapping, translation and stepping (Stacking RIGHTWARDS from Max.X)
# Original code for target plane in v1.9.7:
# Plane targetPlaneY = Plane.WorldXY;
# targetPlaneY.Rotate(Math.PI / 2, Rhino.Geometry.Vector3d.ZAxis);
# We will replace it with YZ to YX (X-axis = World.Y, Y-axis = World.X)
new_target_plane = '''Plane targetPlaneY = new Plane(Point3d.Origin, Rhino.Geometry.Vector3d.YAxis, Rhino.Geometry.Vector3d.XAxis);'''

ts = re.sub(r'Plane targetPlaneY = Plane\.WorldXY;\s*targetPlaneY\.Rotate\(Math\.PI / 2, Rhino\.Geometry\.Vector3d\.ZAxis\);', new_target_plane, ts)

ts = ts.replace("var xformMove = Transform.Translation(new Vector3d(cursorXYSecs - bbFlat.Max.X, globalBB.Min.Y - bbFlat.Min.Y, 0));",
"var xformMove = Transform.Translation(new Vector3d(cursorXYSecs - bbFlat.Min.X, globalBB.Min.Y - bbFlat.Min.Y, 0));")

ts = ts.replace("cursorXYSecs -= ((bbFlat.Max.X - bbFlat.Min.X) + padding);",
"cursorXYSecs += ((bbFlat.Max.X - bbFlat.Min.X) + padding);")
# If it had the old diagonal length:
ts = ts.replace("cursorXYSecs -= ((bbFlat.Max.X - bbFlat.Min.X) + globalBB.Diagonal.Length * 0.05);",
"cursorXYSecs += ((bbFlat.Max.X - bbFlat.Min.X) + padding);")

with open("Components/TerrainSections.cs", "w") as f:
    f.write(ts)

