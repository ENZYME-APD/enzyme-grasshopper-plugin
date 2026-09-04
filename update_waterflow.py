import re

with open("Components/WaterFlow.cs", "r") as f:
    ts = f.read()

# 1. Update component name
ts = ts.replace('"Auto-Grid Raindrop Flow Engine"', '"Raindrop Flow Engine"')

# 2. Add Info output
out_old = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("FlowPaths", "P", "The resulting downhill flow curves", GH_ParamAccess.list);
        }'''
out_new = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("FlowPaths", "P", "The resulting downhill flow curves", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }'''
ts = ts.replace(out_old, out_new)

# 3. Add info string in SolveInstance
solve_end_old = '''            DA.SetDataList(0, paths);
            
            watch.Stop();'''
solve_end_new = '''            DA.SetDataList(0, paths);
            
            string info = 
                "RAINDROP FLOW ENGINE\\n" +
                "====================\\n\\n" +
                "HOW IT WORKS:\\n" +
                "Generates a grid of points above your site, drops a 'particle' at each point, and traces its exact physical path downhill along the mesh faces until it hits a flat area or the edge.\\n\\n" +
                "INTERPRETATION & IMPORTANCE:\\n" +
                "Provides raw, continuous curves representing the 'journey' of the water. Highly intuitive and visually striking for diagrams and presentation of surface drainage.";
            DA.SetData(1, info);

            watch.Stop();'''
ts = ts.replace(solve_end_old, solve_end_new)

with open("Components/WaterFlow.cs", "w") as f:
    f.write(ts)
