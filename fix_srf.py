import re

with open("Components/TileGridGenerator.cs", "r") as f:
    lines = f.readlines()

for i in range(len(lines)):
    if "Surface srf = brep.Faces[0].UnderlyingSurface();" in lines[i]:
        lines[i] = lines[i].replace("Surface srf =", "Surface brepSrf =")
    
with open("Components/TileGridGenerator.cs", "w") as f:
    f.writelines(lines)
