import os
import glob
import re

components_dir = "Components"
count = 0

for filepath in glob.glob(os.path.join(components_dir, "*.cs")):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Replace AddTextParameter("Info", "I", ... with AddTextParameter("Info", "Info", ...
    # Be robust with whitespace just in case
    new_content = re.sub(r'AddTextParameter\(\s*"Info"\s*,\s*"I"\s*,', 'AddTextParameter("Info", "Info",', content)
    new_content = re.sub(r'AddTextParameter\(\s*"Instructions"\s*,\s*"I"\s*,', 'AddTextParameter("Instructions", "Instructions",', new_content)
    
    if new_content != content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(new_content)
        count += 1
        print(f"Updated {filepath}")

print(f"Total files updated: {count}")
