import re

with open('Components/GlobalFloodEngine.cs', 'r') as f:
    content = f.read()

content = content.replace('new Guid("b871cda3-87a4-4f05-b1a7-15632af0c0bd")', 'new Guid("A1B2C3D4-E5F6-4789-9A0B-1C2D3E4F5A6B")')

with open('Components/GlobalFloodEngine.cs', 'w') as f:
    f.write(content)

