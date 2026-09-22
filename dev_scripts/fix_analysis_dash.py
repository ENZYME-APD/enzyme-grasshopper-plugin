import re
with open("Components/AnalysisDashboard.cs", "r") as f:
    text = f.read()

# Replace the broken newline
text = text.replace('this.Message = $"HUD ACTIVE', 'this.Message = $"HUD ACTIVE\\n{_title}"; //')

with open("Components/AnalysisDashboard.cs", "w") as f:
    f.write(text)

