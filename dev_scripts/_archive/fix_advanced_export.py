with open('Components/AdvancedExportViews.cs', 'r') as f:
    content = f.read()

# Add _taskPending field
field_orig = """        private bool _isExporting = false;
        private int _stateIndex = 0;"""
field_new = """        private bool _isExporting = false;
        private bool _taskPending = false;
        private int _stateIndex = 0;"""
content = content.replace(field_orig, field_new)

# Fix SolveInstance
solve_orig = """            if (_isExporting)
            {
                if (_stateIndex < _ghStates.Count)
                {
                    string currentState = _ghStates[_stateIndex];
                    DA.SetData(1, currentState);
                    _statusInfo = $"Exporting State {_stateIndex + 1}/{_ghStates.Count}\\n{currentState}";

                    // Safely delay the capture to allow GH Canvas and geometries to fully update
                    System.Threading.Tasks.Task.Delay(500).ContinueWith(t => 
                    {
                        RhinoApp.InvokeOnUiThread(new Action(() => 
                        {
                            CaptureCurrentViews(currentState);
                            
                            _stateIndex++;
                            
                            var doc = OnPingDocument();
                            if (doc != null)
                            {
                                doc.ScheduleSolution(5, d => {
                                    this.ExpireSolution(false);
                                });
                            }
                        }));
                    });
                }
                else
                {
                    // Finished
                    _isExporting = false;
                    _statusInfo = "Export Complete\\n" + _savedFiles.Count + " images";
                    DA.SetDataList(0, _savedFiles);
                }
            }"""

solve_new = """            if (_isExporting)
            {
                if (_stateIndex < _ghStates.Count)
                {
                    if (_taskPending) 
                    {
                        // Output the current state to prevent grasshopper warnings while waiting
                        DA.SetDataList(0, _savedFiles);
                        return;
                    }
                    _taskPending = true;

                    string currentState = _ghStates[_stateIndex];
                    DA.SetData(1, currentState);
                    _statusInfo = $"Exporting State {_stateIndex + 1}/{_ghStates.Count}\\n{currentState}";

                    // Safely delay the capture to allow GH Canvas and geometries to fully update
                    System.Threading.Tasks.Task.Delay(500).ContinueWith(t => 
                    {
                        RhinoApp.InvokeOnUiThread(new Action(() => 
                        {
                            CaptureCurrentViews(currentState);
                            
                            _stateIndex++;
                            _taskPending = false;
                            
                            var doc = OnPingDocument();
                            if (doc != null)
                            {
                                doc.ScheduleSolution(5, d => {
                                    this.ExpireSolution(false);
                                });
                            }
                        }));
                    });
                }
                else
                {
                    // Finished
                    _isExporting = false;
                    _taskPending = false;
                    _statusInfo = "Export Complete\\n" + _savedFiles.Count + " images";
                    DA.SetDataList(0, _savedFiles);
                    
                    // Fire one last false to reset things safely if run is still connected
                    if (run)
                    {
                        var doc = OnPingDocument();
                        if (doc != null)
                        {
                            doc.ScheduleSolution(5, d => {
                                this.ExpireSolution(false);
                            });
                        }
                    }
                }
            }
            else
            {
                DA.SetDataList(0, _savedFiles);
            }"""
content = content.replace(solve_orig, solve_new)

with open('Components/AdvancedExportViews.cs', 'w') as f:
    f.write(content)
