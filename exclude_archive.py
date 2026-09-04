with open("enzGhPlugin.csproj", "r") as f:
    text = f.read()

text = text.replace('<Compile Remove="ScriptsToProcess\\**" />', '<Compile Remove="ScriptsToProcess\\**" />\n    <Compile Remove="Archive\\**" />')

with open("enzGhPlugin.csproj", "w") as f:
    f.write(text)
