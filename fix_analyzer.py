import re

with open("Components/MeshHeightAnalysis.cs.backup", "r") as f:
    orig = f.read()

# 1. Update Inputs (remove sections stuff)
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

# 2. Update Outputs (remove sections stuff)
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

# 3. Modify SolveInstance
# Remove the fetching of section inputs
orig = re.sub(r'Plane rotPlane = Plane.WorldXY;\s*DA.GetData\(7, ref rotPlane\);\s*int sectionsX = 0;\s*DA.GetData\(8, ref sectionsX\);\s*int sectionsY = 0;\s*DA.GetData\(9, ref sectionsY\);\s*bool layoutFlat = false;\s*DA.GetData\(10, ref layoutFlat\);', '', orig)

# Remove the section tree instantiations
orig = re.sub(r'GH_Structure<GH_Curve> sectionOutlinesX = new GH_Structure<GH_Curve>\(\);\s*GH_Structure<GH_Curve> sectionOutlinesY = new GH_Structure<GH_Curve>\(\);\s*GH_Structure<GH_Curve> flatSectionsX = new GH_Structure<GH_Curve>\(\);\s*GH_Structure<GH_Curve> flatSectionsY = new GH_Structure<GH_Curve>\(\);\s*GH_Structure<GH_String> labelText3D = new GH_Structure<GH_String>\(\);\s*GH_Structure<GH_Point> labelPoints3D = new GH_Structure<GH_Point>\(\);\s*GH_Structure<GH_String> labelTextFlat = new GH_Structure<GH_String>\(\);\s*GH_Structure<GH_Point> labelPointsFlat = new GH_Structure<GH_Point>\(\);\s*GH_Structure<GH_String> sectionMetadata = new GH_Structure<GH_String>\(\);\s*int totalSectionsX = 0;\s*int totalSectionsY = 0;', '', orig)

# Remove the large intersection block (if (sectionsX > 0 || sectionsY > 0) ... )
orig = re.sub(r'if \(sectionsX > 0 \|\| sectionsY > 0\)\s*\{.*?(?=\s*\}\s*\}\s*\}\s*string instructions = )', '', orig, flags=re.DOTALL)
# It might leave a few extra braces, I'll be careful. Let's just do a string replacement since we know where it starts and ends.
