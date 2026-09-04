import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

old_rot_input = """pManager.AddNumberParameter("Rotation", "Rot", "Optional rotation angle (in radians) applied after alignment.", GH_ParamAccess.item, 0.0);"""
new_rot_input = """pManager.AddNumberParameter("Rotation", "Rot", "Optional rotation angle (in degrees) applied after alignment.", GH_ParamAccess.item, 0.0);"""

content = content.replace(old_rot_input, new_rot_input)

old_rot_logic = """            if (rot != 0.0)
            {
                originPlane.Rotate(rot, originPlane.ZAxis);
            }"""
new_rot_logic = """            if (rot != 0.0)
            {
                originPlane.Rotate(rot * Math.PI / 180.0, originPlane.ZAxis);
            }"""

content = content.replace(old_rot_logic, new_rot_logic)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)
