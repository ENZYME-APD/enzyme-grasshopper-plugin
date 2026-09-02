import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# Add Diagnostics using
if "using System.Diagnostics;" not in content:
    content = content.replace("using Grasshopper.Kernel.Geometry.Delaunay;", "using Grasshopper.Kernel.Geometry.Delaunay;\nusing System.Diagnostics;")

# Add Stopwatch start
start_timer = """        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
"""
content = re.sub(r'protected override void SolveInstance\(IGH_DataAccess DA\)\s*\{', start_timer, content)

# Add Stopwatch end and HUD update
end_timer = """            stopwatch.Stop();
            Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m\\nTime: {stopwatch.ElapsedMilliseconds} ms";
        }"""
content = re.sub(r'Message = \$"Road Generator\\n---\\nLanes: \{totalLanes\}\\nWidth: \{totalHalfWidth\*2\}m";\s*\}', end_timer, content)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
