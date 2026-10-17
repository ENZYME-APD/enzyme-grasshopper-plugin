import re

with open('Components/GlobalFloodEngine.cs', 'r') as f:
    content = f.read()

content = content.replace('outMesh.TopologyEdges.SortEdges(); // Ensure topology is built\n', '')

with open('Components/GlobalFloodEngine.cs', 'w') as f:
    f.write(content)

