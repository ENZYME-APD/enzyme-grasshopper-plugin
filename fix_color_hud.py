import re

with open("Components/ColorMeshTiles.cs", "r") as f:
    content = f.read()

bad_hud = """            List<string> ui_lines = new List<string>
            {
                "COLORED MESH TILES",
                $"Time: {execution_ms:F2} ms",
                "---"
            };"""
            
good_hud = """            List<string> ui_lines = new List<string>
            {
                "PIXEL GRADIENT",
                $"Time: {execution_ms:F2} ms",
                "---"
            };"""

content = content.replace(bad_hud, good_hud)
content = content.replace('            ui_lines.Insert(0, "PIXEL GRADIENT");\n', "")

with open("Components/ColorMeshTiles.cs", "w") as f:
    f.write(content)
