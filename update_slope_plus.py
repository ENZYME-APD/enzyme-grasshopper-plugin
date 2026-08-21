import re

with open('Components/SlopeTerrainPlus.cs', 'r') as f:
    content = f.read()

# Add Newtonsoft.Json
if 'using Newtonsoft.Json;' not in content:
    content = 'using Newtonsoft.Json;\n' + content
if 'using Newtonsoft.Json.Linq;' not in content:
    content = 'using Newtonsoft.Json.Linq;\n' + content

# Add output parameter
out_params = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("AnalyzedMeshes", "AnalyzedMeshes", "Colored Meshes", GH_ParamAccess.list);
            pManager.AddColourParameter("LegendColors", "LegendColors", "Legend Colors", GH_ParamAccess.list);
            pManager.AddTextParameter("LegendValues", "LegendValues", "Legend Values", GH_ParamAccess.list);
            pManager.AddNumberParameter("OverThresholdRatio", "OverThresholdRatio", "Ratio of faces over threshold", GH_ParamAccess.list);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "JSON Legend Data", GH_ParamAccess.item);
        }"""
content = re.sub(r'protected override void RegisterOutputParams\(GH_OutputParamManager pManager\).*?}', out_params, content, flags=re.DOTALL)

# Find where data is set in SolveInstance
set_data = """            DA.SetDataList(0, coloredMeshes);
            DA.SetDataList(1, legendColors);
            DA.SetDataList(2, legendValues);
            DA.SetDataList(3, overRatios);

            if (coloredMeshes.Count > 0)
            {
                var jColors = new JArray();
                foreach (var c in legendColors) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var jLabels = new JArray();
                foreach (var v in legendValues) jLabels.Add(v);
                
                double avgRatio = 0;
                foreach (var r in overRatios) avgRatio += r;
                avgRatio = (avgRatio / overRatios.Count) * 100.0;
                
                var legendObj = new JObject
                {
                    ["Type"] = binaryMode ? "Blocks" : "Gradient",
                    ["Title"] = $"Slope Terrain (Thresh: {thresholdValue:F1})",
                    ["Colors"] = jColors,
                    ["Labels"] = jLabels,
                    ["SubLabels"] = new JArray($"{avgRatio:F1}% over threshold")
                };
                DA.SetData(4, legendObj.ToString());
            }"""
            
content = re.sub(r'DA\.SetDataList\(0, coloredMeshes\);.*?DA\.SetDataList\(3, overRatios\);', set_data, content, flags=re.DOTALL)

with open('Components/SlopeTerrainPlus.cs', 'w') as f:
    f.write(content)
