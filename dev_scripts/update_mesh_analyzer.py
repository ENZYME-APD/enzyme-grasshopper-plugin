import re

with open('Components/MeshHeightAnalysis.cs', 'r') as f:
    content = f.read()

if 'using Newtonsoft.Json;' not in content:
    content = 'using Newtonsoft.Json;\n' + content
if 'using Newtonsoft.Json.Linq;' not in content:
    content = 'using Newtonsoft.Json.Linq;\n' + content

# Add output parameter
out_params = """            pManager.AddTextParameter("SectionMetadata", "SM", "Dictionary keys containing spatial transform & ID data.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "JSON Legend Data", GH_ParamAccess.item);
        }"""
content = re.sub(r'pManager\.AddTextParameter\("SectionMetadata".*?\n\s*\}', out_params, content, flags=re.DOTALL)

# Add JSON generation at the end of SolveInstance
set_data = """            DA.SetDataTree(18, sectionMetadata);

            if (enableHeatmap && totalVerticesCount > 0)
            {
                var jColors = new JArray();
                var cList = customColorList.Count > 0 ? customColorList : new List<Color> { Color.Blue, Color.Cyan, Color.Lime, Color.Yellow, Color.Red };
                foreach (var c in cList) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var legendObj = new JObject
                {
                    ["Type"] = "Blocks",
                    ["Title"] = "Mesh Terrain Elevation",
                    ["Colors"] = jColors,
                    ["Labels"] = new JArray($"{globalTerrainZMin:F1}m", $"{globalTerrainZMax:F1}m"),
                    ["SubLabels"] = new JArray($"Relief: {(globalTerrainZMax - globalTerrainZMin):F1}m")
                };
                DA.SetData(19, legendObj.ToString());
            }

            double terrainRelief = totalVerticesCount > 0 ? Math.Round(globalTerrainZMax - globalTerrainZMin, 2) : 0.0;"""

content = re.sub(r'DA\.SetDataTree\(18, sectionMetadata\);\s*double terrainRelief = totalVerticesCount > 0', set_data, content, flags=re.DOTALL)

with open('Components/MeshHeightAnalysis.cs', 'w') as f:
    f.write(content)
