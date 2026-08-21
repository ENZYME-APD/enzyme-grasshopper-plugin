import re

with open('Components/SlopeTerrainPlus.cs', 'r') as f:
    content = f.read()

set_data = """            DA.SetDataList(0, out_meshes);
            DA.SetDataList(1, out_colors);
            DA.SetDataList(2, out_values);
            DA.SetDataList(3, out_ratios);

            if (out_meshes.Count > 0)
            {
                var jColors = new JArray();
                foreach (var c in out_colors) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var jLabels = new JArray();
                foreach (var v in out_values) jLabels.Add(v.ToString());
                
                double avgRatio = 0;
                foreach (var r in out_ratios) avgRatio += r;
                if (out_ratios.Count > 0) avgRatio = (avgRatio / out_ratios.Count) * 100.0;
                
                var legendObj = new JObject
                {
                    ["Type"] = is_binary ? "Blocks" : "Gradient",
                    ["Title"] = $"Slope Terrain (Thresh: {deg:F1}°)",
                    ["Colors"] = jColors,
                    ["Labels"] = jLabels,
                    ["SubLabels"] = new JArray($"{avgRatio:F1}% over threshold")
                };
                DA.SetData(4, legendObj.ToString());
            }"""

content = re.sub(r'DA\.SetDataList\(0, out_meshes\);.*?DA\.SetDataList\(3, out_ratios\);', set_data, content, flags=re.DOTALL)

with open('Components/SlopeTerrainPlus.cs', 'w') as f:
    f.write(content)
