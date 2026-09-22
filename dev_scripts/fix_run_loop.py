with open('Components/AdvancedExportViews.cs', 'r') as f:
    content = f.read()

# Add _prevRun field
field_orig = """        private bool _taskPending = false;
        private int _stateIndex = 0;"""
field_new = """        private bool _taskPending = false;
        private int _stateIndex = 0;
        private bool _prevRun = false;"""
content = content.replace(field_orig, field_new)

# Modify SolveInstance trigger
solve_orig = """            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool run = false;
            DA.GetData("Run", ref run);

            if (run && !_isExporting)"""

solve_new = """            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool run = false;
            DA.GetData("Run", ref run);
            
            bool triggerStart = run && !_prevRun;
            _prevRun = run;

            if (triggerStart && !_isExporting)"""
content = content.replace(solve_orig, solve_new)

# Remove the dangerous end schedule
danger_orig = """                    // Fire one last false to reset things safely if run is still connected
                    if (run)
                    {
                        var doc = OnPingDocument();
                        if (doc != null)
                        {
                            doc.ScheduleSolution(5, d => {
                                this.ExpireSolution(false);
                            });
                        }
                    }"""
danger_new = """                    // Automatically reset the run state in case it was a toggle
                    _prevRun = true; // Prevents it from immediately restarting on next frame if they left it true
"""
content = content.replace(danger_orig, danger_new)

with open('Components/AdvancedExportViews.cs', 'w') as f:
    f.write(content)
