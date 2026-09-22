with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

content = content.replace("public class TurntableCamera : GH_Component\\n    {", 
"""public class TurntableCamera : GH_Component
    {
        private string _lastExportDate = "Never";
        private string _lastExportDuration = "-";
        private int _lastExportFrames = 0;""")

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
