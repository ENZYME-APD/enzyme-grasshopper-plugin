with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

# Add State variables
class_def = "    public class TurntableCamera : GH_Component\\n    {"
state_vars = """    public class TurntableCamera : GH_Component
    {
        private string _lastExportDate = "Never";
        private string _lastExportDuration = "-";
        private int _lastExportFrames = 0;"""
content = content.replace(class_def, state_vars)

# Update RegisterOutputParams
out_orig = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Orbit Path", "Orbit", "The circular path of the camera.", GH_ParamAccess.item);
            pManager.AddPointParameter("Camera Points", "Pts", "Calculated 3D camera coordinates.", GH_ParamAccess.list);
            pManager.AddTextParameter("File Paths", "Files", "Paths of exported PNG frames.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Component execution HUD.", GH_ParamAccess.item);
        }"""
out_new = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Orbit Path", "Orbit", "The circular path of the camera.", GH_ParamAccess.item);
            pManager.AddPointParameter("Camera Points", "Pts", "Calculated 3D camera coordinates.", GH_ParamAccess.list);
            pManager.AddPointParameter("Target Out", "Target", "Passthrough of the focal target point.", GH_ParamAccess.item);
            pManager.AddTextParameter("File Paths", "Files", "Paths of exported PNG frames.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Component execution HUD.", GH_ParamAccess.item);
        }"""
content = content.replace(out_orig, out_new)

# Update SolveInstance end logic
solve_end_orig = """            timer.Stop();

            DA.SetData(0, orbitPath);
            DA.SetDataList(1, camPoints);
            DA.SetDataList(2, savedFiles);

            string status = run ? (savedFiles.Count > 0 ? $"Exported {savedFiles.Count} frames" : "Failed to export") : "Sleeping (Run is False)";
            
            Message = $"{this.NickName}\\nTime: {timer.ElapsedMilliseconds} ms\\n---\\n{status}";

            DA.SetData(3, "TURNTABLE CAMERA ENGINE\\n" """

solve_end_new = """            timer.Stop();

            if (run && savedFiles.Count > 0)
            {
                _lastExportDate = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");
                _lastExportDuration = timer.ElapsedMilliseconds.ToString() + " ms";
                _lastExportFrames = savedFiles.Count;
            }

            DA.SetData(0, orbitPath);
            DA.SetDataList(1, camPoints);
            DA.SetData(2, target);
            DA.SetDataList(3, savedFiles);
            
            Message = $"{this.NickName}\\nTime: {_lastExportDuration}\\n---\\nLast: {_lastExportDate}\\nFrames: {_lastExportFrames}";

            DA.SetData(4, "TURNTABLE CAMERA ENGINE\\n" """
content = content.replace(solve_end_orig, solve_end_new)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
