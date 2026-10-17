import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

dump_code = """
            try {
                var t = typeof(Grasshopper.Kernel.Special.GH_ImageSampler);
                var lines = new System.Collections.Generic.List<string>();
                foreach(var p in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) lines.Add("P: " + p.Name);
                foreach(var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) lines.Add("M: " + m.Name);
                System.IO.File.WriteAllLines("sampler_dump.txt", lines);
            } catch {}
"""

# Insert dump_code at the start of AddedToDocument
target = "public override void AddedToDocument(GH_Document document)\n        {"
content = content.replace(target, target + dump_code)

with open('Components/PixelatedSurface.cs', 'w') as f:
    f.write(content)
