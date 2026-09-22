import re

with open('Components/SlopeTerrainPlus.cs', 'r') as f:
    content = f.read()

# 1. RegisterInputParams
new_inputs = """        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "TargetMeshes", "Meshes to analyze", GH_ParamAccess.list);
            pManager.AddNumberParameter("ThresholdValue", "ThresholdValue", "Threshold for slope analysis", GH_ParamAccess.item, 30.0);
            pManager.AddIntegerParameter("ThresholdMode", "ThresholdMode", "0: Degrees, 1: Percentage, 2: Ratio", GH_ParamAccess.item, 0);
            pManager.AddColourParameter("Custom Colors", "Custom Colors", "List of colors for gradient or binary mapping", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("EnableBinaryMode", "EnableBinaryMode", "If true, snaps to binary colors (Under/Over threshold)", GH_ParamAccess.item, true);
        }"""
content = re.sub(r'protected override void RegisterInputParams\(GH_InputParamManager pManager\)\s*\{.*?\}(?=\s*protected override void RegisterOutputParams)', new_inputs, content, flags=re.DOTALL)


# 2. RegisterOutputParams
new_outputs = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("AnalyzedMeshes", "AnalyzedMeshes", "Colored Meshes", GH_ParamAccess.list);
            pManager.AddGenericParameter("Dashboard Data", "Dashboard Data", "JSON Legend Data", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Component information and interpretation", GH_ParamAccess.item);
        }"""
content = re.sub(r'protected override void RegisterOutputParams\(GH_OutputParamManager pManager\)\s*\{.*?\}(?=\s*protected override void SolveInstance)', new_outputs, content, flags=re.DOTALL)


with open('Components/SlopeTerrainPlus.cs', 'w') as f:
    f.write(content)
