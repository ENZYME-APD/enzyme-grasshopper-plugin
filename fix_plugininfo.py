import re

with open('Components/PluginInfo.cs', 'r') as f:
    content = f.read()

# Current: 
# Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "string", ox, -20);
# Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "string", ox, 20);

new_code = """                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, ox, -20, 150, 40);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, ox, 20, 150, 40);"""

content = re.sub(r'Enzyme\.Utils\.AutoWireHelper\.WireOutputParam\(this, document, 0, "string", ox, -20\);\s*Enzyme\.Utils\.AutoWireHelper\.WireOutputParam\(this, document, 1, "string", ox, 20\);', new_code, content)

with open('Components/PluginInfo.cs', 'w') as f:
    f.write(content)
