import re

with open('Components/KeylinePattern.cs', 'r') as f:
    content = f.read()

# Replace the slider for input 3
old_slider = 'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 1, 50, 5, 330, 60);'
new_slider = 'Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 0, 10, 5, 330, 60);'
content = content.replace(old_slider, new_slider)

with open('Components/KeylinePattern.cs', 'w') as f:
    f.write(content)
