import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

bad_str = '            Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m\\nTime: {stopwatch.ElapsedMilliseconds} ms";'
good_str = '            Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m\\nTime: {stopwatch.ElapsedMilliseconds} ms";'

# Since it actually has real newlines in the file, let's just do a regex replace from stopwatch.Stop() to the end of the method.
content = re.sub(r'stopwatch\.Stop\(\);\s*Message =.*?";\s*\}', 'stopwatch.Stop();\n            Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m\\nTime: {stopwatch.ElapsedMilliseconds} ms";\n        }', content, flags=re.DOTALL)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
