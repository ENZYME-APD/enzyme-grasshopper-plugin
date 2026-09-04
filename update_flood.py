import re

with open("Components/GlobalFloodEngine.cs", "r") as f:
    ts = f.read()

ts = ts.replace('"Global Volumetric Flood Engine"', '"Global Flood Engine"')

out_old = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("FloodMesh", "FM", "Flooded terrain heatmap mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("WaterDepths", "WD", "Water depths at each vertex in meters", GH_ParamAccess.list);
            pManager.AddPointParameter("AnalysisPoints", "Pts", "Points corresponding to the water depth values", GH_ParamAccess.list);
        }'''
out_new = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("FloodMesh", "FM", "Flooded terrain heatmap mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("WaterDepths", "WD", "Water depths at each vertex in meters", GH_ParamAccess.list);
            pManager.AddPointParameter("AnalysisPoints", "Pts", "Points corresponding to the water depth values", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }'''
ts = ts.replace(out_old, out_new)

solve_end_old = '''            DA.SetData(0, outMesh);
            DA.SetDataList(1, finalDepths);
            DA.SetDataList(2, analysisPoints);

            sw.Stop();'''
solve_end_new = '''            DA.SetData(0, outMesh);
            DA.SetDataList(1, finalDepths);
            DA.SetDataList(2, analysisPoints);

            string info = 
                "GLOBAL FLOOD ENGINE\\n" +
                "===================\\n\\n" +
                "HOW IT WORKS:\\n" +
                "Simulates ponding (accumulation volume). You input a rain intensity and duration, and the engine calculates how much water falls on the site and fills local depressions, outputting exact water depths.\\n\\n" +
                "INTERPRETATION & IMPORTANCE:\\n" +
                "Essential for flood risk assessment. Reveals trapped water areas, calculates retention pond volumes, and shows submerged regions during storms. It shows the 'destination' of water.";
            DA.SetData(3, info);

            sw.Stop();'''
ts = ts.replace(solve_end_old, solve_end_new)

with open("Components/GlobalFloodEngine.cs", "w") as f:
    f.write(ts)
