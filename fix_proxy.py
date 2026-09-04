import re

with open("Utils/AutoWireHelper.cs", "r") as f:
    content = f.read()

content = content.replace('p.Name.Equals', 'p.Desc.Name.Equals')

with open("Utils/AutoWireHelper.cs", "w") as f:
    f.write(content)
