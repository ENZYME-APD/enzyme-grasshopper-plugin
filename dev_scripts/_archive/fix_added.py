with open("Components/RoadSlopeAnalyzer.cs", "r") as f:
    content = f.read()

import re

# Remove the duplicate AddedToDocument we added
duplicate_pattern = r'public override void AddedToDocument\(Grasshopper\.Kernel\.GH_Document document\)\s*\{\s*base\.AddedToDocument\(document\);\s*if \(this\.Params\.Input\[4\]\.SourceCount == 0\)\s*\{.*?\}\s*\}'
content = re.sub(duplicate_pattern, '', content, flags=re.DOTALL)

# Now fix the original AddedToDocument
original_pattern = r'Enzyme\.Utils\.AutoWireHelper\.WireToggle\(this, document, 4, false, 210, 40\);'

new_wiring = """
                var vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X - 200, this.Attributes.Pivot.Y + 60);
                vl.ListItems.Clear();
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Degrees", "0"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Percentage", "1"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Ratio (1:X)", "2"));
                vl.SelectItem(1);
                document.AddObject(vl, false);
                this.Params.Input[4].AddSource(vl);
"""
content = content.replace(original_pattern, new_wiring)

with open("Components/RoadSlopeAnalyzer.cs", "w") as f:
    f.write(content)

