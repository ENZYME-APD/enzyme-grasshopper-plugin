import re

with open('Components/GradientGenerator.cs', 'r') as f:
    content = f.read()

old_msg = '            Message = $"Steps: {steps}";'
new_msg = '            Message = $"Gradient Generator\\n---\\nInput Colors: {inColors.Count}\\nSteps: {steps}";'

content = content.replace(old_msg, new_msg)

with open('Components/GradientGenerator.cs', 'w') as f:
    f.write(content)
