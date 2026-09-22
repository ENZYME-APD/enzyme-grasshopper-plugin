import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

content = content.replace("            pManager[10].Optional = true;\n\n        private bool hasSources = false;", "            pManager[10].Optional = true;\n        }\n\n        private bool hasSources = false;")

with open('Components/PixelatedSurface.cs', 'w') as f:
    f.write(content)
