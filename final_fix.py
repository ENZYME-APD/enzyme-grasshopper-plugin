with open("Components/PixelatedSurface.cs", "r") as f:
    content = f.read()

content = content.replace("protected override void SolveInstance(IGH_DataAccess DA)\n        {\n            Stopwatch t_start = new Stopwatch();", "protected override void SolveInstance(IGH_DataAccess DA)\n        {\n            Random rnd = new Random();\n            Stopwatch t_start = new Stopwatch();")
content = content.replace("string bake_status = \"\\\\n\";", "string bake_status = \"\";")
content = content.replace("                bake_status = \"\\nBake: COMPLETED\";", "string bake_status = \"\";\n                bake_status = \"\\nBake: COMPLETED\";")

# Wait, `bake_status` needs to be declared BEFORE `if (run_bake)`.
# Let's just fix it globally.
