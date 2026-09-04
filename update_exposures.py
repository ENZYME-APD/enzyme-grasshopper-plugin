import re
import os

def set_exposure(file_path, new_exposure):
    with open(file_path, 'r') as f:
        content = f.read()
    
    if "public override GH_Exposure Exposure" in content:
        content = re.sub(r'public override GH_Exposure Exposure\s*=>\s*GH_Exposure\.[a-zA-Z]+;', f'public override GH_Exposure Exposure => GH_Exposure.{new_exposure};', content)
    else:
        # Insert it before the RegisterInputParams method
        insert_str = f'\n        public override GH_Exposure Exposure => GH_Exposure.{new_exposure};\n\n'
        content = re.sub(r'(protected override void RegisterInputParams)', insert_str + r'\1', content)
        
    with open(file_path, 'w') as f:
        f.write(content)

# Water
set_exposure('Components/FlowHeat.cs', 'primary')
set_exposure('Components/GlobalFloodEngine.cs', 'primary')
set_exposure('Components/WaterFlow.cs', 'primary')

# Wind
set_exposure('Components/WindEngineHTVer.cs', 'secondary')

# Morphology
set_exposure('Components/AdaptiveTerrainGrader.cs', 'tertiary')
set_exposure('Components/ElevationLabel.cs', 'tertiary')
set_exposure('Components/MeshHeightAnalysis.cs', 'tertiary')
set_exposure('Components/RoadSlopeAnalyzer.cs', 'tertiary')
set_exposure('Components/SlopeTerrainPlus.cs', 'tertiary')
set_exposure('Components/TerrainSections.cs', 'tertiary')

# Misc
set_exposure('Components/TerrainGeneratorPro.cs', 'quarternary')
set_exposure('Components/LegendGeometry.cs', 'quarternary')

# MeshColorReset (Reset Mesh Colors) needs subcategory change and exposure
with open('Components/MeshColorReset.cs', 'r') as f:
    content = f.read()

content = content.replace('"Utilities"', '"Terrain"')
if "public override GH_Exposure Exposure" in content:
    content = re.sub(r'public override GH_Exposure Exposure\s*=>\s*GH_Exposure\.[a-zA-Z]+;', r'public override GH_Exposure Exposure => GH_Exposure.quarternary;', content)
else:
    insert_str = '\n        public override GH_Exposure Exposure => GH_Exposure.quarternary;\n\n'
    content = re.sub(r'(protected override void RegisterInputParams)', insert_str + r'\1', content)
with open('Components/MeshColorReset.cs', 'w') as f:
    f.write(content)

print("Exposures updated.")
