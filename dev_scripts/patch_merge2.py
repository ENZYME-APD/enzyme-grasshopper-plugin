import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

content = content.replace(
    'slider.Slider.Value = (decimal)defaults[i];',
    'slider.Slider.Value = (decimal)defaults[i];\n                slider.NickName = $"Data {i + 1}";'
)

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)
