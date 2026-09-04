import re

with open("MeshHeightAnalysis.cs.backup", "r") as f:
    orig = f.read()

# 1. Update Inputs
input_params_new = """        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "M", "The meshes to analyze.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("SearchRings", "R", "Topological radius in rings.", GH_ParamAccess.item, 5);
            pManager.AddNumberParameter("ProminenceLimit", "P", "Minimum Z-delta to be considered a peak/valley.", GH_ParamAccess.item, 0.5);
            pManager.AddColourParameter("CustomColors", "C", "Custom colormap list.", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("CullGlobals", "CG", "Toggle to remove the absolute highest/lowest points.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("AvoidBoundaries", "AB", "Toggle to ignore naked edge vertices.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("EnableHeatmap", "EH", "Toggle to compute and output the vertex heatmap mesh.", GH_ParamAccess.item, true);
        }"""
orig = re.sub(r'protected override void RegisterInputParams.*?\}', input_params_new, orig, flags=re.DOTALL, count=1)

# 2. Update Outputs
output_params_new = """        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions", "I", "Component documentation and usage manual.", GH_ParamAccess.item);
            pManager.AddPointParameter("LocalPeaks", "LP", "Output points for local highs.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("PeakElevations", "PE", "Z-values for local highs.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMaxPoint", "GMP", "Absolute highest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMaxElevation", "GME", "Absolute highest Z-value.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LocalValleys", "LV", "Output points for local lows.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("ValleyElevations", "VE", "Z-values for local lows.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMinPoint", "GMI", "Absolute lowest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMinElevation", "GMIE", "Absolute lowest Z-value.", GH_ParamAccess.tree);
            pManager.AddMeshParameter("HeatmapMeshes", "HM", "The vertex-colored duplicate mesh.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "JSON Legend Data", GH_ParamAccess.item);
        }"""
orig = re.sub(r'protected override void RegisterOutputParams.*?\}', output_params_new, orig, flags=re.DOTALL, count=1)

# 3. Clean SolveInstance manually
start_idx = orig.find('Plane rotPlane = Plane.WorldXY;')
end_idx = orig.find('string instructions = "Analyzes mesh extremes')

if start_idx != -1 and end_idx != -1:
    orig = orig[:start_idx] + orig[end_idx:]
    
# Remove setting of removed data
orig = re.sub(r'DA\.SetDataTree\(10.*?DA\.SetDataTree\(18, sectionMetadata\);', '', orig, flags=re.DOTALL)
orig = orig.replace('DA.SetData(19, legendObj.ToString());', 'DA.SetData(10, legendObj.ToString());')

# Fix instructions
orig = orig.replace('"Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels."', '"Analyzes mesh extremes and generates topo heatmaps."')

# Fix HUD Message for Analyzer
# The user wants:
# TERRAIN ANALYZER
# Time: {timer}
# ---
# Area: {Area}
# Relief (ΔZ): {Relief}
# Avg Elev: {Avg Elev}
# Max Height: {MaxHeight}
# Min Height: {MinHeight}
# Peaks: {Peaks} | Valleys: {Valleys}
new_hud = """
            double terrainRelief = totalVerticesCount > 0 ? Math.Round(globalTerrainZMax - globalTerrainZMin, 2) : 0.0;
            double meanElevation = totalVerticesCount > 0 ? Math.Round(totalZSum / totalVerticesCount, 2) : 0.0;
            
            Message = $"TERRAIN ANALYZER\\nTime: {t_start.ElapsedMilliseconds:F2} ms\\n---\\nArea: {Math.Round(totalTerrainArea, 2)}\\nRelief (ΔZ): {terrainRelief}\\nAvg Elev: {meanElevation}\\nMax Height: {Math.Round(globalTerrainZMax, 2)}\\nMin Height: {Math.Round(globalTerrainZMin, 2)}\\nPeaks: {totalPeaksFound} | Valleys: {totalValleysFound}";
        }"""
orig = re.sub(r'double terrainRelief = totalVerticesCount > 0 \? Math\.Round.*?\}', new_hud, orig, flags=re.DOTALL)

with open("Components/MeshHeightAnalysis.cs", "w") as f:
    f.write(orig)
