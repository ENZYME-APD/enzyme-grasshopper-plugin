import re

with open("Components/PixelatedSurface.cs", "r") as f:
    content = f.read()

# Fix rnd
if "Random rnd =" not in content:
    content = content.replace("Stopwatch t_start = new Stopwatch();", "Random rnd = new Random(42);\n            Stopwatch t_start = new Stopwatch();")

# Fix bake_status
content = re.sub(r'(\s*)if \(run_bake\)', r'\1string bake_status = "";\1if (run_bake)', content)

with open("Components/PixelatedSurface.cs", "w") as f:
    f.write(content)
