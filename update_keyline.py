import re

with open("Components/KeylinePattern.cs", "r") as f:
    ts = f.read()

ts = ts.replace('"Keyline Pattern Engine"', '"Keyline Engine"')

out_old = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Pattern Curves", "P", "Generated keyline pattern curves", GH_ParamAccess.list);
        }'''
out_new = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Pattern Curves", "P", "Generated keyline pattern curves", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }'''
ts = ts.replace(out_old, out_new)

solve_end_old = '''            DA.SetDataList(0, resultLines);
        }'''
solve_end_new = '''            DA.SetDataList(0, resultLines);
            
            string info = 
                "KEYLINE ENGINE\\n" +
                "==============\\n\\n" +
                "HOW IT WORKS:\\n" +
                "Takes guide curves (like the Master Keyline) and generates perfectly offset plowing/swale lines across the topography, maintaining parametric spacing.\\n\\n" +
                "INTERPRETATION & IMPORTANCE:\\n" +
                "A specialized ecological design workflow used in regenerative agriculture and masterplanning. Generates grading or plowing lines designed to passively slow water runoff, distribute it evenly, and maximize absorption.";
            DA.SetData(1, info);
        }'''
ts = ts.replace(solve_end_old, solve_end_new)

with open("Components/KeylinePattern.cs", "w") as f:
    f.write(ts)
