with open("Components/MicroclimateComfort.cs", "r") as f:
    content = f.read()

# Add output
if 'pManager.AddTextParameter("Dashboard Data"' not in content:
    content = content.replace('pManager.AddColourParameter("Colors", "Col", "Gradient color mapping.", GH_ParamAccess.list);',
        'pManager.AddColourParameter("Colors", "Col", "Gradient color mapping.", GH_ParamAccess.list);\n            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON Legend Data", GH_ParamAccess.item);')

# Update SolveInstance end
end_solve = """            DA.SetDataList(0, points);
            DA.SetDataList(1, feelsLike);
            DA.SetDataList(2, categories);
            DA.SetDataList(3, colors);
        }"""

new_end_solve = """            
            var jColors = new Newtonsoft.Json.Linq.JArray();
            var stops = new double[] { -10, 5, 20, 30, 40 };
            var baseColors = new System.Drawing.Color[] { 
                System.Drawing.Color.FromArgb(0, 0, 139), 
                System.Drawing.Color.FromArgb(0, 191, 255), 
                System.Drawing.Color.FromArgb(34, 139, 34), 
                System.Drawing.Color.FromArgb(255, 215, 0), 
                System.Drawing.Color.FromArgb(139, 0, 0) 
            };
            foreach (var c in baseColors) jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
            
            var legendObj = new Newtonsoft.Json.Linq.JObject
            {
                ["Type"] = "Blocks",
                ["Title"] = "Microclimate Comfort",
                ["Colors"] = jColors,
                ["Labels"] = new Newtonsoft.Json.Linq.JArray("-10°C", "40°C"),
                ["SubLabels"] = new Newtonsoft.Json.Linq.JArray(method == 0 ? "Fast Apparent Temp" : "Precise UTCI")
            };

            DA.SetDataList(0, points);
            DA.SetDataList(1, feelsLike);
            DA.SetDataList(2, categories);
            DA.SetDataList(3, colors);
            DA.SetData(4, legendObj.ToString());
        }"""

content = content.replace(end_solve, new_end_solve)

with open("Components/MicroclimateComfort.cs", "w") as f:
    f.write(content)
