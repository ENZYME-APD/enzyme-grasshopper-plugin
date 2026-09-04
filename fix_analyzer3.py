import re

with open("Components/MeshHeightAnalysis.cs", "r") as f:
    orig = f.read()

# RegisterInputParams
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

# RegisterOutputParams
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

# AddedToDocument
added_to_doc = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 0, "mesh", 200, -20);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 1, 1, 20, 5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 5.0, 0.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, true, 210, 140);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 5, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, true, 210, 220);
            }
        }"""
orig = re.sub(r'public override void AddedToDocument\(GH_Document document\).*?\}\s*\}', added_to_doc, orig, flags=re.DOTALL, count=1)

# In SolveInstance, remove the reading of section params
orig = re.sub(r'\s*Plane secPlane = Plane\.WorldXY;\s*DA\.GetData\(7, ref secPlane\);\s*int secCountX = 0;\s*DA\.GetData\(8, ref secCountX\);\s*int secCountY = 0;\s*DA\.GetData\(9, ref secCountY\);\s*bool layoutFlat = false;\s*DA\.GetData\(10, ref layoutFlat\);', '', orig)

# Remove the instantiations of sectionOutlines etc.
orig = re.sub(r'\s*var sectionOutlinesX = new GH_Structure<GH_Curve>\(\);\s*var sectionOutlinesY = new GH_Structure<GH_Curve>\(\);\s*var flatSectionsX = new GH_Structure<GH_Curve>\(\);\s*var flatSectionsY = new GH_Structure<GH_Curve>\(\);\s*var labelText3D = new GH_Structure<GH_String>\(\);\s*var labelPoints3D = new GH_Structure<GH_Point>\(\);\s*var labelTextFlat = new GH_Structure<GH_String>\(\);\s*var labelPointsFlat = new GH_Structure<GH_Point>\(\);\s*var sectionMetadata = new GH_Structure<GH_String>\(\);\s*int totalSectionsX = 0;\s*int totalSectionsY = 0;', '', orig)

# Remove the section generating block:
# It starts with 'if (secCountX > 0 || secCountY > 0)' and ends before 'string instructions = '
# I will use a simple substring replace for the large block.
sec_start = orig.find('if (secCountX > 0 || secCountY > 0)')
sec_end = orig.find('string instructions = ')
if sec_start != -1 and sec_end != -1:
    orig = orig[:sec_start] + orig[sec_end:]

# Set outputs
# Remove DA.SetDataTree(10..18)
orig = re.sub(r'\s*DA\.SetDataTree\(10, sectionOutlinesX\);.*?DA\.SetDataTree\(18, sectionMetadata\);', '', orig, flags=re.DOTALL)
orig = orig.replace('DA.SetData(19, legendObj.ToString());', 'DA.SetData(10, legendObj.ToString());')
orig = orig.replace('"Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels."', '"Analyzes mesh extremes and generates topo heatmaps."')

new_hud = """
            double terrainRelief = totalVerticesCount > 0 ? Math.Round(globalTerrainZMax - globalTerrainZMin, 2) : 0.0;
            double meanElevation = totalVerticesCount > 0 ? Math.Round(totalZSum / totalVerticesCount, 2) : 0.0;
            
            Message = $"TERRAIN ANALYZER\\nTime: {t_start.ElapsedMilliseconds:F2} ms\\n---\\nArea: {Math.Round(totalTerrainArea, 2)}\\nRelief (ΔZ): {terrainRelief}\\nAvg Elev: {meanElevation}\\nMax Height: {Math.Round(globalTerrainZMax, 2)}\\nMin Height: {Math.Round(globalTerrainZMin, 2)}\\nPeaks: {totalPeaksFound} | Valleys: {totalValleysFound}";
        }"""
orig = re.sub(r'double terrainRelief = totalVerticesCount > 0 \? Math\.Round.*?\}', new_hud, orig, flags=re.DOTALL)

with open("Components/MeshHeightAnalysis.cs", "w") as f:
    f.write(orig)
