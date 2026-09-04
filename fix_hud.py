with open("Components/ThermalComfortAnalyzer.cs", "r") as f:
    text = f.read()

import re
text = re.sub(r'        public override void CreateAttributes\(\)\s*\{\s*m_attributes = new Enzyme\.Utils\.ComponentHUD\(this\);\s*\}\s*', '', text)

with open("Components/ThermalComfortAnalyzer.cs", "w") as f:
    f.write(text)
