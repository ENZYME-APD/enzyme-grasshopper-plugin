import re

with open('Components/ColorMeshTiles.cs', 'r') as f:
    content = f.read()

start_str = "if (!hasSources)"
end_str = "        }"

start_idx = content.find(start_str)
end_idx = content.find("        protected override void RegisterInputParams", start_idx)

if end_idx == -1: # Wait, ColorMeshTiles might have RegisterOutputParams instead
    end_idx = content.find("        protected override void RegisterOutputParams", start_idx)

lines = []
lines.append('                var colors = new System.Drawing.Color[] {')
lines.append('                    System.Drawing.Color.FromArgb(240, 120, 120),')
lines.append('                    System.Drawing.Color.FromArgb(200, 120, 120),')
lines.append('                    System.Drawing.Color.FromArgb(250, 210, 210)')
lines.append('                };')
lines.append('                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 1, colors, 150, -140);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 2, System.Drawing.Color.FromArgb(255, 30, 0), 210, -50);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 3, new string[]{"X", "Y", "Z"}, new string[]{"0", "1", "2"}, 300, -10);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 100.0, 30.0, 330, 30);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 100.0, 40.0, 330, 70);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 1.0, 0.94, 330, 110);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 7, 210, 150);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -120);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, -50, 180, 50);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 20, 180, 50);')
lines.append('                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "mesh", 220, 90);')

new_block = "if (!hasSources)\n            {\n" + "\n".join(lines) + "\n            }\n        }\n\n"

content = content[:start_idx] + new_block + content[end_idx:]

with open('Components/ColorMeshTiles.cs', 'w') as f:
    f.write(content)
