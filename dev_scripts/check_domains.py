import os
import re

COMPONENTS_DIR = "Components"

for filename in os.listdir(COMPONENTS_DIR):
    if not filename.endswith(".cs"): continue
    
    with open(os.path.join(COMPONENTS_DIR, filename), 'r') as f:
        content = f.read()
        
    input_match = re.search(r'protected override void RegisterInputParams[^{]*\{([^}]+)\}', content)
    if not input_match: continue
    
    for line in input_match.group(1).split(';'):
        if 'AddNumberParameter' in line or 'AddIntegerParameter' in line:
            desc_match = re.search(r'Parameter\s*\(\s*".*?"\s*,\s*".*?"\s*,\s*"(.*?)"', line)
            if desc_match:
                desc = desc_match.group(1)
                # Does it have a number?
                if re.search(r'\d', desc):
                    print(f"{filename}: {desc}")
