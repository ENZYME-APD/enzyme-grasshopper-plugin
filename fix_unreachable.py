import re
import glob

for filepath in glob.glob("Components/*.cs"):
    with open(filepath, "r") as f:
        content = f.read()
    
    # We look for "return;\n                            DA.SetData("
    # and we want to move the DA.SetData to the END of SolveInstance.
    match = re.search(r'return;\s+(DA\.SetData\(\d+,\s*".*?"\);)', content, re.DOTALL)
    if match:
        set_data_str = match.group(1)
        # Remove it from the incorrect place
        content = content.replace(set_data_str, "")
        
        # Find the end of SolveInstance. It's the last closing brace before the next method/property.
        # Let's find "protected override void SolveInstance"
        idx_solve = content.find("protected override void SolveInstance")
        if idx_solve != -1:
            # We can find the matching closing brace for SolveInstance.
            # Start from the first '{' after idx_solve
            idx_start = content.find("{", idx_solve)
            brace_count = 1
            idx_end = -1
            for i in range(idx_start + 1, len(content)):
                if content[i] == '{':
                    brace_count += 1
                elif content[i] == '}':
                    brace_count -= 1
                    if brace_count == 0:
                        idx_end = i
                        break
            
            if idx_end != -1:
                # Insert set_data_str before idx_end
                indent = "            "
                content = content[:idx_end] + f"{indent}{set_data_str}\n        " + content[idx_end:]
                with open(filepath, "w") as f:
                    f.write(content)
                print(f"Fixed {filepath}")
