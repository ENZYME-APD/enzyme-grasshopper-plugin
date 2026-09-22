import re

with open("Components/ThermalComfortAnalyzer.cs", "r") as f:
    text = f.read()

# Add JObject reference
if "using Newtonsoft.Json.Linq;" not in text:
    text = "using Newtonsoft.Json.Linq;\n" + text

# Add Output Parameter
out_params = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("BestComfortPoints", "Best", "Point(s) with apparent temperature closest to IdealTemperature, within ComfortTolerance of the single best", GH_ParamAccess.list);
            pManager.AddPointParameter("WorstComfortPoints", "Worst", "Point(s) with apparent temperature furthest from IdealTemperature, within ComfortTolerance of the single worst", GH_ParamAccess.list);
            pManager.AddMeshParameter("ComfortMesh", "ComfortMesh", "The input terrain mesh, vertex-colored by comfort deviation - same connectivity as TerrainMesh, no new grid/UVs", GH_ParamAccess.item);
            pManager.AddNumberParameter("ComfortValues", "ComfortValues", "Raw apparent temperature (deg C) per terrain vertex, aligned with ComfortMesh's vertex order", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard JSON", "JSON", "Unified JSON payload containing legend and key analysis metrics for the Analysis Dashboard", GH_ParamAccess.item);
        }"""
text = re.sub(r'        protected override void RegisterOutputParams\(GH_OutputParamManager pManager\)\s*\{[^\}]+\}', out_params, text, flags=re.DOTALL)

# Add JSON building logic
# Find DA.SetDataList(0, bestPoints);
json_logic = """
            if (execute && comfortValues.Count > 0)
            {
                double avgAT = 0;
                foreach (double v in comfortValues) avgAT += v;
                avgAT /= comfortValues.Count;

                double comfortablePct = (bestPoints.Count / Math.Max(1.0, tagPoints.Count)) * 100.0;

                JObject payload = new JObject();
                payload["Title"] = "HIGROTHERMAL COMFORT";
                payload["Type"] = "Gradient";
                
                JArray jcolors = new JArray();
                if (userColors.Count == 0) {
                    jcolors.Add(new JObject { ["R"] = 20, ["G"] = 180, ["B"] = 60 });
                    jcolors.Add(new JObject { ["R"] = 220, ["G"] = 30, ["B"] = 30 });
                } else {
                    foreach (var c in userColors) jcolors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                }
                payload["Colors"] = jcolors;
                
                JArray jlabels = new JArray();
                jlabels.Add("Best (Comfortable)");
                jlabels.Add("Worst (Uncomfortable)");
                payload["Labels"] = jlabels;
                
                JArray jmetrics = new JArray();
                jmetrics.Add(new JObject { ["Name"] = "Avg Apparent Temp", ["Value"] = avgAT.ToString("F1") + " °C" });
                jmetrics.Add(new JObject { ["Name"] = "Best Comfort Pts", ["Value"] = bestPoints.Count.ToString() });
                jmetrics.Add(new JObject { ["Name"] = "Worst Comfort Pts", ["Value"] = worstPoints.Count.ToString() });
                payload["Metrics"] = jmetrics;

                DA.SetData(5, payload.ToString(Newtonsoft.Json.Formatting.None));
            }

            DA.SetDataList(0, bestPoints);"""
text = text.replace("            DA.SetDataList(0, bestPoints);", json_logic)

# Replace the Info output text
info_logic = """            DA.SetData(4, "HIGROTHERMAL COMFORT\\n"
                + "\\n"
                + "METHODOLOGY (STEADMAN 1994):\\n"
                + "Calculates Apparent Temperature (AT) combining ambient dry-bulb temperature, relative humidity, and wind speed. "
                + "Vapor pressure is derived using e = (RH/100) * 6.105 * exp(17.27*T / (237.7+T)). "
                + "AT is calculated as T + 0.33*e - 0.70*v - 4.00 (where v is wind speed in m/s).\\n\\n"
                + "INTERPRETATION & IMPORTANCE:\\n"
                + "Best/Worst points flag where pedestrian comfort is strongest or weakest relative to IdealTemperature. "
                + "Use this after wind analysis to verify whether high-speed corridors or sheltered wakes improve or degrade outdoor comfort.");"""
text = re.sub(r'            DA\.SetData\(4,\s*"HIGROTHERMAL COMFORT\\n"[^;]+;', info_logic, text, flags=re.DOTALL)

with open("Components/ThermalComfortAnalyzer.cs", "w") as f:
    f.write(text)

