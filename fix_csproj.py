with open("enzGhPlugin.csproj", "r") as f:
    text = f.read()

text = text.replace("""<<<<<<< HEAD
    <Version>1.9.29</Version>
=======
    <Version>1.9.30</Version>
>>>>>>> ea59114 (feat: adjust slider decimals for TerrainGeneratorPro)""", "    <Version>1.9.30</Version>")

with open("enzGhPlugin.csproj", "w") as f:
    f.write(text)
