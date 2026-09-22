import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

group_logic = """
            Grasshopper.Kernel.Special.GH_Group group = new Grasshopper.Kernel.Special.GH_Group();
            group.CreateAttributes();
            group.AddObject(preview.InstanceGuid);
            group.AddObject(swatch.InstanceGuid);
            if (preview.Params.Input.Count > 2) group.AddObject(slider.InstanceGuid);
            if (preview.Params.Input.Count > 3) group.AddObject(toggle.InstanceGuid);
            
            // Calculate bounds manually roughly
            float minX = preview.Attributes.Pivot.X - 160;
            float maxX = preview.Attributes.Pivot.X + 80;
            float minY = preview.Attributes.Pivot.X - 30;
            float maxY = preview.Attributes.Pivot.X + 90;
            
            // Wait, GH calculates it automatically if we do:
            // group.ExpireCaches();
            doc.AddObject(group, false);
"""

# I will skip the group if it's too much hassle. It's just a bounding box. 
