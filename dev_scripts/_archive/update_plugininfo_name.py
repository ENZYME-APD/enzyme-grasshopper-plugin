import re

with open('Components/PluginInfo.cs', 'r') as f:
    content = f.read()

content = content.replace('"Enzyme Plugin Info", "EnzInfo",', '"Enzyme Version Info", "EnzVer",')
content = content.replace('public class PluginInfo : GH_Component', 'public class PluginInfo : GH_Component\n    {\n        public override GH_Exposure Exposure => GH_Exposure.primary;')

with open('Components/PluginInfo.cs', 'w') as f:
    f.write(content)
