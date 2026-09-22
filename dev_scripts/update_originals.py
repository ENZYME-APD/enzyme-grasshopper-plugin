import re

def update_file(filename, is_slope):
    with open(filename, 'r') as f:
        content = f.read()

    if 'using Newtonsoft.Json;' not in content:
        content = 'using Newtonsoft.Json;\n' + content
    if 'using Newtonsoft.Json.Linq;' not in content:
        content = 'using Newtonsoft.Json.Linq;\n' + content

    if is_slope:
        old_create = """        private object CreateLegendData(Color startColor, Color endColor, double threshold, double percentOverThreshold)
        {
            // Create a simple data structure to hold legend information
            // In a real implementation, this would be a custom class
            // For now, we'll use a dynamic object for simplicity
            var legendData = new
            {
                StartColor = startColor,
                EndColor = endColor,
                Threshold = threshold,
                PercentOverThreshold = percentOverThreshold,
                Title = $"Slope Analysis (Threshold: {threshold:F3}°)",
                Description = $"{percentOverThreshold:F2}% of area exceeds {threshold:F3}° slope"
            };

            return legendData;
        }"""
        new_create = """        private string CreateLegendData(Color startColor, Color endColor, double threshold, double percentOverThreshold)
        {
            var legendObj = new JObject
            {
                ["Type"] = "Gradient",
                ["Title"] = $"Slope Analysis (Threshold: {threshold:F1}°)",
                ["Colors"] = new JArray(
                    new JObject { ["R"] = startColor.R, ["G"] = startColor.G, ["B"] = startColor.B },
                    new JObject { ["R"] = endColor.R, ["G"] = endColor.G, ["B"] = endColor.B }
                ),
                ["Labels"] = new JArray("0°", $"{threshold:F1}°+"),
                ["SubLabels"] = new JArray($"{percentOverThreshold:F1}% over threshold")
            };
            return legendObj.ToString();
        }"""
        content = content.replace(old_create, new_create)
    else:
        old_create = """        private object CreateHeightLegendData(System.Collections.Generic.List<Color> colors, double minZ, double maxZ, bool flipColors)
        {
            // Create a simple data structure to hold legend information
            // In a real implementation, this would be a custom class
            // For now, we'll use a dynamic object for simplicity
            var legendData = new
            {
                Colors = new System.Collections.Generic.List<Color>(colors),
                MinHeight = minZ,
                MaxHeight = maxZ,
                FlipColors = flipColors,
                Title = "Height Map Analysis",
                Description = $"Height range: {minZ:F2} to {maxZ:F2}",
                HeightRange = maxZ - minZ
            };

            return legendData;
        }"""
        new_create = """        private string CreateHeightLegendData(System.Collections.Generic.List<Color> colors, double minZ, double maxZ, bool flipColors)
        {
            var jColors = new JArray();
            foreach (var c in colors) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
            var legendObj = new JObject
            {
                ["Type"] = "Blocks",
                ["Title"] = "Height Map Analysis",
                ["Colors"] = jColors,
                ["Labels"] = new JArray($"{minZ:F1}m", $"{maxZ:F1}m"),
                ["SubLabels"] = new JArray($"Relief: {(maxZ - minZ):F1}m")
            };
            return legendObj.ToString();
        }"""
        content = content.replace(old_create, new_create)

    with open(filename, 'w') as f:
        f.write(content)

update_file('Components/SlopeAnalysisMesh.cs', True)
update_file('Components/HeightMapAnalysisMesh.cs', False)
