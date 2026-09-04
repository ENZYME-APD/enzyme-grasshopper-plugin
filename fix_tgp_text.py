import re

with open("Components/TerrainGeneratorPro.cs", "r") as f:
    content = f.read()

# Replace in C# file
old_how = "Takes raw input data (points, curves, or GIS contour lines) and triangulates a clean, unified, watertight 3D mesh."
new_how = "A procedural terrain generator developed specifically to test different analysis components across a wide variety of topographic conditions."
content = content.replace(old_how, new_how)

old_why = "The foundational step for all digital site analysis. It converts messy, disconnected surveyor data into a usable computational surface for grading, water, and slope analysis."
new_why = "Generates synthetic, highly controllable terrains (ridges, valleys, noise). This allows designers to rigorously test and calibrate drainage, slope, and wind analysis tools before applying them to real-world GIS data."
content = content.replace(old_why, new_why)

with open("Components/TerrainGeneratorPro.cs", "w") as f:
    f.write(content)

with open("docs/Terrain_and_LEAP_Guide.md", "r") as f:
    md = f.read()

md = md.replace(old_how, new_how)
md = md.replace(old_why, new_why)

with open("docs/Terrain_and_LEAP_Guide.md", "w") as f:
    f.write(md)
