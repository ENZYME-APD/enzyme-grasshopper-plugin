import re

with open("Components/KeypointFinder.cs", "r") as f:
    ts = f.read()

ts = ts.replace('"Keypoint Finder"', '"Keypoint Engine"')

out_old = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Keypoint", "KP", "The extracted keypoint of the stream", GH_ParamAccess.item);
            pManager.AddCurveParameter("Master Keyline", "KL", "The contour line passing through the keypoint", GH_ParamAccess.item);
        }'''
out_new = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Keypoint", "KP", "The extracted keypoint of the stream", GH_ParamAccess.item);
            pManager.AddCurveParameter("Master Keyline", "KL", "The contour line passing through the keypoint", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }'''
ts = ts.replace(out_old, out_new)

solve_end_old = '''            DA.SetData(0, bestKp);
            DA.SetData(1, keyline);
        }'''
solve_end_new = '''            DA.SetData(0, bestKp);
            DA.SetData(1, keyline);
            
            string info = 
                "KEYPOINT ENGINE\\n" +
                "===============\\n\\n" +
                "HOW IT WORKS:\\n" +
                "Reads streams (typically from Hydro-DEM) to find the 'Keypoint'—the exact inflection point where a valley slope shifts from steep to flat. Extracts the Master Keyline contour at that elevation.\\n\\n" +
                "INTERPRETATION & IMPORTANCE:\\n" +
                "A core component of Keyline Design. Locating the keypoint is essential for designing plowing patterns or swales that passively manage water, spread it from wet valleys to dry ridges, and reduce erosion.";
            DA.SetData(2, info);
        }'''
ts = ts.replace(solve_end_old, solve_end_new)

with open("Components/KeypointFinder.cs", "w") as f:
    f.write(ts)
