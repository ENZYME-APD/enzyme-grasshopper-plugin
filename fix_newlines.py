with open("Components/PixelatedSurface.cs", "r") as f:
    lines = f.readlines()

new_lines = []
i = 0
while i < len(lines):
    if 'bake_status = "\\\\n' in lines[i]:
        new_lines.append('                bake_status = "\\nBake: COMPLETED";\n')
        i += 2 # skip next line
        continue
    if 'Message = string.Join("' in lines[i]:
        new_lines.append('            Message = string.Join("\\n", ui_lines);\n')
        i += 2
        continue
    new_lines.append(lines[i])
    i += 1
with open("Components/PixelatedSurface.cs", "w") as f:
    f.writelines(new_lines)
