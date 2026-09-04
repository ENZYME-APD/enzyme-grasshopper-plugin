with open("Components/PixelatedSurface.cs", "r") as f:
    content = f.read()

content = content.replace('bake_status = "\\\nBake: COMPLETED";', 'bake_status = "\\nBake: COMPLETED";')
content = content.replace('Message = string.Join("\\\n", ui_lines);', 'Message = string.Join("\\n", ui_lines);')

with open("Components/PixelatedSurface.cs", "w") as f:
    f.write(content)
