import re

def ensure_json_using(text):
    if "using Newtonsoft.Json.Linq;" not in text:
        text = "using Newtonsoft.Json.Linq;\n" + text
    return text

# --- HydroDEM ---
with open("Components/HydroDEM.cs", "r") as f:
    t = f.read()
t = ensure_json_using(t)

op = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Streams", "S", "Extracted stream networks (Polylines)", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Accumulation", "A", "Flow accumulation value per topology vertex", GH_ParamAccess.list);
            pManager.AddPointParameter("Topology Points", "P", "Topology vertices matching the accumulation list", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and methodology", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard JSON", "JSON", "Unified JSON payload containing legend and key analysis metrics for the Analysis Dashboard", GH_ParamAccess.item);
        }"""
t = re.sub(r'        protected override void RegisterOutputParams\(GH_OutputParamManager pManager\)\s*\{[^\}]+\}', op, t, flags=re.DOTALL)

jl = """            Message = $"Hydro-DEM\\n---\\nThreshold: {threshold}\\nStreams: {streams.Count}";
            
            JObject payload = new JObject();
            payload["Title"] = "HYDRO DEM";
            payload["Type"] = "Discrete";
            payload["Colors"] = new JArray(new JObject { ["R"] = 0, ["G"] = 100, ["B"] = 255 });
            payload["Labels"] = new JArray("Streams");
            
            JArray metrics = new JArray();
            metrics.Add(new JObject { ["Name"] = "Stream Networks", ["Value"] = streams.Count.ToString() });
            payload["Metrics"] = metrics;
            
            DA.SetData(3, "HYDROLOGICAL DEM\\n"
                + "\\n"
                + "METHODOLOGY:\\n"
                + "Uses the D8 flow routing algorithm. Each mesh vertex assesses its 8 immediate topological neighbors to find the steepest descent path. "
                + "Rainfall (1 unit per vertex) is then accumulated down these natural gradients. Flow networks are generated where accumulation exceeds the specified threshold.\\n\\n"
                + "INTERPRETATION & IMPORTANCE:\\n"
                + "Critical for predicting natural drainage, identifying flood-prone catchments, and optimizing agricultural water capture systems before detailed engineering begins.");
            DA.SetData(4, payload.ToString(Newtonsoft.Json.Formatting.None));

            DA.SetDataList(0, streams);"""
t = t.replace("            Message = $\"Hydro-DEM\\n---\\nThreshold: {threshold}\\nStreams: {streams.Count}\";\n            DA.SetDataList(0, streams);", jl)
with open("Components/HydroDEM.cs", "w") as f:
    f.write(t)


# --- KeypointFinder ---
with open("Components/KeypointFinder.cs", "r") as f:
    t = f.read()
t = ensure_json_using(t)

op = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Keypoints", "P", "The identified points of inflection (steep to flat)", GH_ParamAccess.list);
            pManager.AddCurveParameter("Master Keylines", "K", "The specific horizontal terrain contours passing through the Keypoints", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and methodology", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard JSON", "JSON", "Unified JSON payload containing legend and key analysis metrics for the Analysis Dashboard", GH_ParamAccess.item);
        }"""
t = re.sub(r'        protected override void RegisterOutputParams\(GH_OutputParamManager pManager\)\s*\{[^\}]+\}', op, t, flags=re.DOTALL)

jl = """            Message = $"Keypoint Finder\\n---\\nSmoothing: {window}\\nFound: {keypoints.Count}";
            
            JObject payload = new JObject();
            payload["Title"] = "KEYPOINT FINDER";
            payload["Type"] = "Discrete";
            payload["Colors"] = new JArray(new JObject { ["R"] = 255, ["G"] = 50, ["B"] = 50 });
            payload["Labels"] = new JArray("Keypoints");
            
            JArray metrics = new JArray();
            metrics.Add(new JObject { ["Name"] = "Points Found", ["Value"] = keypoints.Count.ToString() });
            payload["Metrics"] = metrics;
            
            DA.SetData(2, "KEYPOINT FINDER\\n"
                + "\\n"
                + "METHODOLOGY:\\n"
                + "Applies topographic curvature analysis along identified valley thalwegs (stream networks). "
                + "It calculates the second derivative of elevation along the flow path to isolate the exact point of inflection—"
                + "the geomorphic transition where a steep, convex valley head flattens into a concave valley floor.\\n\\n"
                + "INTERPRETATION & IMPORTANCE:\\n"
                + "In P.A. Yeomans' Keyline Design, the Keypoint is the optimal location for capturing and storing water (dams), as it represents the highest workable contour where water can be gravity-fed away from the valley toward drier ridges.");
            DA.SetData(3, payload.ToString(Newtonsoft.Json.Formatting.None));

            DA.SetDataList(0, keypoints);"""
t = t.replace("            Message = $\"Keypoint Finder\\n---\\nSmoothing: {window}\\nFound: {keypoints.Count}\";\n            DA.SetDataList(0, keypoints);", jl)
with open("Components/KeypointFinder.cs", "w") as f:
    f.write(t)


# --- KeylinePattern ---
with open("Components/KeylinePattern.cs", "r") as f:
    t = f.read()
t = ensure_json_using(t)

op = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Keylines", "K", "Generated 3D swale/plow curves projected on terrain", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and methodology", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard JSON", "JSON", "Unified JSON payload containing legend and key analysis metrics for the Analysis Dashboard", GH_ParamAccess.item);
        }"""
t = re.sub(r'        protected override void RegisterOutputParams\(GH_OutputParamManager pManager\)\s*\{[^\}]+\}', op, t, flags=re.DOTALL)

jl = """            Message = $"Keyline Pattern\\n---\\nSpacing: {spacing}m\\nCount: {count}\\nGenerated: {keylines.Count}";
            
            double totalLen = 0;
            foreach (var crv in keylines) if (crv != null) totalLen += crv.GetLength();
            
            JObject payload = new JObject();
            payload["Title"] = "KEYLINE PATTERN";
            payload["Type"] = "Discrete";
            payload["Colors"] = new JArray(new JObject { ["R"] = 50, ["G"] = 200, ["B"] = 50 });
            payload["Labels"] = new JArray("Swales / Plow Lines");
            
            JArray metrics = new JArray();
            metrics.Add(new JObject { ["Name"] = "Keylines Generated", ["Value"] = keylines.Count.ToString() });
            metrics.Add(new JObject { ["Name"] = "Total Length", ["Value"] = $"{totalLen:F1} m" });
            payload["Metrics"] = metrics;
            
            DA.SetData(1, "KEYLINE PATTERN\\n"
                + "\\n"
                + "METHODOLOGY:\\n"
                + "Generates agricultural earthworks (swales, plow lines) via contour-parallel offsetting originating from the Master Keyline. "
                + "Because hills naturally spread apart while valleys converge, offsetting downward from the keyline naturally creates a slightly off-contour geometry with a constant fall toward the ridges.\\n\\n"
                + "INTERPRETATION & IMPORTANCE:\\n"
                + "Passively rehydrates landscapes without pumping by naturally intercepting surface runoff and distributing it from wet valleys out to dry ridges, maximizing soil moisture infiltration.");
            DA.SetData(2, payload.ToString(Newtonsoft.Json.Formatting.None));

            DA.SetDataList(0, keylines);"""
t = t.replace("            Message = $\"Keyline Pattern\\n---\\nSpacing: {spacing}m\\nCount: {count}\\nGenerated: {keylines.Count}\";\n            DA.SetDataList(0, keylines);", jl)
with open("Components/KeylinePattern.cs", "w") as f:
    f.write(t)

