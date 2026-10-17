import os
import re

updates = {
    # UTILITIES
    'CanvasStyle.cs': ('Utilities', 'primary'),
    'CanvasUsageInfo.cs': ('Utilities', 'primary'),
    'CanvasStateManager.cs': ('Utilities', 'primary'),
    'CanvasAuditor.cs': ('Utilities', 'primary'),
    'GroupColours.cs': ('Utilities', 'primary'),

    'CreateNamedViews.cs': ('Utilities', 'secondary'),
    'SaveNamedView.cs': ('Utilities', 'secondary'),
    'ActivateViewSettings.cs': ('Utilities', 'secondary'),
    'TurntableCamera.cs': ('Utilities', 'secondary'),
    'PaperSizeToPixels.cs': ('Utilities', 'secondary'),

    'ExportViews.cs': ('Utilities', 'tertiary'),
    'AdvancedExportViews.cs': ('Utilities', 'tertiary'),

    'CustomTextTag.cs': ('Utilities', 'quarternary'),
    'DataVisualizer.cs': ('Utilities', 'quarternary'),

    'AreaConv.cs': ('Utilities', 'quinary'),
    'LayerMat.cs': ('Utilities', 'quinary'),
    'ChangePulse.cs': ('Utilities', 'quinary'),
    'MapToGradient.cs': ('Utilities', 'quinary'),
    'OptimizedRemap.cs': ('Utilities', 'quinary'),
    'GradientGenerator.cs': ('Utilities', 'quinary'),
    
    # AutoListItem to Data
    'AutoListItem.cs': ('Data', None),

    # SITE ANALYSIS (Formerly Terrain)
    'GlobalFloodEngine.cs': ('Site Analysis', 'primary'),
    'AdaptiveTerrainGrader.cs': ('Site Analysis', 'primary'),
    'WaterFlow.cs': ('Site Analysis', 'primary'),
    'TerrainSections.cs': ('Site Analysis', 'primary'),
    'SlopeTerrainPlus.cs': ('Site Analysis', 'primary'),
    'RoadSlopeAnalyzer.cs': ('Site Analysis', 'primary'),
    'TerrainGeneratorPro.cs': ('Site Analysis', 'primary'),
    'MeshColorReset.cs': ('Site Analysis', 'primary'),
    'FastCFD.cs': ('Site Analysis', 'primary'),
    'FlowHeat.cs': ('Site Analysis', 'primary'),
    'WindEngineHTVer.cs': ('Site Analysis', 'primary'),
    'AnalysisDashboard.cs': ('Site Analysis', 'primary'),
    'MeshHeightAnalysis.cs': ('Site Analysis', 'primary'),
    'ElevationLabel.cs': ('Site Analysis', 'primary'),
    'LegendGeometry.cs': ('Site Analysis', 'primary'),
    'ThermalComfortAnalyzer.cs': ('Site Analysis', 'primary'),
    'Streamlines.cs': ('Site Analysis', 'primary'),

    # Site Analysis - Solar
    'Heliodon.cs': ('Site Analysis', 'secondary'),
    'SunHoursAnalysis.cs': ('Site Analysis', 'secondary'),

    # Site Analysis - LEAP
    'HydroDEM.cs': ('Site Analysis', 'tertiary'),
    'KeylinePattern.cs': ('Site Analysis', 'tertiary'),
    'KeypointFinder.cs': ('Site Analysis', 'tertiary'),

    # Masterplan
    'Clearance.cs': ('Masterplan', None),
    'VArrow.cs': ('Masterplan', None),
    'RoadGenerator.cs': ('Masterplan', None),
    'AutoGrade.cs': ('Masterplan', None),
}

def replace_subcategory(content, old_sub, new_sub):
    # Regex to find: "Enzyme", "OldSub"
    return re.sub(r'("Enzyme",\s*")' + re.escape(old_sub) + r'(")', r'\g<1>' + new_sub + r'\2', content)

for root, _, files in os.walk('Components'):
    for file in files:
        if not file.endswith('.cs'): continue
        filepath = os.path.join(root, file)
        
        with open(filepath, 'r') as f:
            content = f.read()
            
        original_content = content
        
        # 1. Global rename: Terrain -> Site Analysis (for those not mapped above)
        content = replace_subcategory(content, 'Terrain', 'Site Analysis')
        
        # 2. Global rename: Masterplan (Beta) -> JSON Masterplan (Beta)
        content = replace_subcategory(content, 'Masterplan (Beta)', 'JSON Masterplan (Beta)')
        
        # 3. Global rename: MP Analysis -> Masterplan
        content = replace_subcategory(content, 'MP Analysis', 'Masterplan')

        # 4. Global rename: Export & Display -> Utilities
        content = replace_subcategory(content, 'Export', 'Utilities')
        content = replace_subcategory(content, 'Display', 'Utilities')
        content = replace_subcategory(content, 'LEAP', 'Site Analysis')
        content = replace_subcategory(content, 'Analysis', 'Site Analysis')

        # 5. Apply specific file mappings
        if file in updates:
            new_subcat, exposure = updates[file]
            
            # Replace subcategory specifically for this file
            # Since we already did global replacements, we just make sure it's the right one
            # The most robust way is to just replace the last string in the base(...) call
            # e.g., base("Name", "Nick", "Desc", "Enzyme", "Whatever")
            # We can use regex:
            content = re.sub(r'(base\s*\([^)]*"Enzyme",\s*")[^"]+(")', r'\g<1>' + new_subcat + r'\2', content)
            
            # Inject exposure
            if exposure and ("public override GH_Exposure Exposure" not in content):
                exposure_prop = f"\n        public override GH_Exposure Exposure => GH_Exposure.{exposure};\n"
                
                # Insert before ComponentGuid
                guid_match = re.search(r'public override Guid ComponentGuid', content)
                if guid_match:
                    content = content[:guid_match.start()] + exposure_prop + content[guid_match.start():]

        if content != original_content:
            with open(filepath, 'w') as f:
                f.write(content)

print("Done updating files.")
