import re

files = ["Components/TileGridGenerator.cs", "Components/VerticalProjection.cs", "Components/PlaneFinder.cs"]

for fname in files:
    with open(fname, 'r') as f:
        content = f.read()
    
    # Remove the broken line
    content = content.replace('        public override GH_Exposure Exposure => GH_Exposure.secondary;        {', '        {')
    content = content.replace('        public override GH_Exposure Exposure => GH_Exposure.secondary;\n', '')
    
    # Insert it properly before the last two closing braces
    # Usually it's \n    }\n}
    pattern = r'(\s*}\s*})$'
    new_code = r'\n        public override GH_Exposure Exposure => GH_Exposure.secondary;\n\1'
    content = re.sub(pattern, new_code, content)
    
    with open(fname, 'w') as f:
        f.write(content)

