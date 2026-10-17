import os
import glob
import re

count_patched = 0
for filepath in glob.glob("Components/*.cs"):
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    # Skip if it already has the menu item
    if "Auto-wire Default Inputs" in content or "Auto-Fill Defaults" in content:
        continue
        
    # Check if it has AutoWireHelper and AddedToDocument
    if "Enzyme.Utils.AutoWireHelper" not in content or "void AddedToDocument" not in content:
        continue

    # Find the if (!hasSources) block
    match = re.search(r'if\s*\(\!hasSources\)\s*\{', content)
    if not match:
        continue
        
    start_idx = match.end()
    
    # Extract the block
    brace_count = 1
    end_idx = -1
    for i in range(start_idx, len(content)):
        if content[i] == '{':
            brace_count += 1
        elif content[i] == '}':
            brace_count -= 1
            if brace_count == 0:
                end_idx = i
                break
                
    if end_idx == -1:
        continue
        
    extracted_body = content[start_idx:end_idx]
    
    # Replace the body in the original content
    new_if_block = "\n                AutoWireDefaultInputs(document);\n            "
    content = content[:start_idx] + new_if_block + content[end_idx:]
    
    # Prepare the new methods to inject
    new_methods = f"""
        private void AutoWireDefaultInputs(GH_Document document)
        {{{extracted_body}}}

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {{
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) =>
            {{
                var doc = OnPingDocument();
                if (doc != null) AutoWireDefaultInputs(doc);
            }});
        }}
"""
    
    # Inject new_methods before RegisterInputParams or RegisterOutputParams, or just before the end of the class.
    # A safe place is right before `public override Guid ComponentGuid`
    guid_match = re.search(r'public\s+override\s+Guid\s+ComponentGuid', content)
    if guid_match:
        content = content[:guid_match.start()] + new_methods + "\n        " + content[guid_match.start():]
        
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Patched {os.path.basename(filepath)}")
        count_patched += 1
    else:
        print(f"Could not find ComponentGuid in {os.path.basename(filepath)}")

print(f"\nTotal patched: {count_patched}")
