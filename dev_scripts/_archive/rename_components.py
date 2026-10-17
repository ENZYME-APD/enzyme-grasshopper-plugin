import re
import os

def update_file(filepath, replacements):
    with open(filepath, 'r') as f:
        content = f.read()
    
    for old, new in replacements:
        content = content.replace(old, new)
        
    with open(filepath, 'w') as f:
        f.write(content)

# 1. Slope Terrain Plus -> Terrain Slope
update_file("Components/SlopeTerrainPlus.cs", [
    ('base("Slope Terrain Plus", "SlopeMesh+",', 'base("Terrain Slope", "TerrainSlope",'),
    ('"SLOPE TERRAIN PLUS\\n"', '"TERRAIN SLOPE\\n"'),
    ('SLOPE TERRAIN PLUS', 'TERRAIN SLOPE')
])

# 2. TERRAIN ANALYSER (MeshHeightAnalysis.cs) -> Terrain Height
update_file("Components/MeshHeightAnalysis.cs", [
    ('base("Mesh Terrain Analyzer", "Terrain",', 'base("Terrain Height", "TerrainHeight",'),
    ('Message = "TERRAIN ANALYZER\\n";', 'Message = "TERRAIN HEIGHT\\n";'),
    ('"MESH HEIGHT ANALYSIS\\n"', '"TERRAIN HEIGHT ANALYSIS\\n"')
])

# 3. ELEV_LABEL -> Elevation Label
update_file("Components/ElevationLabel.cs", [
    ('base("Elevation Labeler Pro", "ELEV_LABEL",', 'base("Elevation Label", "ElevLabel",'),
    ('Message = $"ELEV_LABEL\\n', 'Message = $"ELEVATION LABEL\\n')
])

# 4. Thermal Comfort Analyzer -> Higrothermal Comfort
update_file("Components/ThermalComfortAnalyzer.cs", [
    ('base("Thermal Comfort Analyzer", "ThermalComfort",', 'base("Higrothermal Comfort", "HigroComfort",'),
    ('"THERMAL COMFORT ANALYZER\\n"', '"HIGROTHERMAL COMFORT\\n"')
])

