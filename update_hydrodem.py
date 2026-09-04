import re

with open("Components/HydroDEM.cs", "r") as f:
    ts = f.read()

# Name is already "Hydro-DEM Engine", but let's check.

out_old = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("StreamNetwork", "S", "Extracted vector streams based on threshold", GH_ParamAccess.list);
        }'''
out_new = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("StreamNetwork", "S", "Extracted vector streams based on threshold", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }'''
ts = ts.replace(out_old, out_new)

solve_end_old = '''            DA.SetDataList(0, streams);

            watch.Stop();'''
solve_end_new = '''            DA.SetDataList(0, streams);

            string info = 
                "HYDRO-DEM ENGINE\\n" +
                "================\\n\\n" +
                "HOW IT WORKS:\\n" +
                "Uses standard GIS algorithms (like the D8 flow direction model). Evaluates every vertex against its neighbors to calculate flow direction and accumulation, automatically extracting a strict, connected vector 'stream network'.\\n\\n" +
                "INTERPRETATION & IMPORTANCE:\\n" +
                "The scientific, industry-standard approach to hydrology. Yields perfectly connected, single-line stream segments rather than overlapping curves. Used for rigorous topological analysis.";
            DA.SetData(1, info);

            watch.Stop();'''
ts = ts.replace(solve_end_old, solve_end_new)

with open("Components/HydroDEM.cs", "w") as f:
    f.write(ts)
