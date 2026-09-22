with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

content = content.replace(
"""    public class TurntableCamera : GH_Component
    {
        public TurntableCamera()""",
"""    public class TurntableCamera : GH_Component
    {
        private string _lastExportDate = "Never";
        private string _lastExportDuration = "-";
        private int _lastExportFrames = 0;

        public TurntableCamera()""")

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
