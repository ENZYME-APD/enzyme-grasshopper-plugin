with open("Components/GradientGenerator.cs", "r") as f:
    text = f.read()

text = text.replace('pManager.AddColourParameter("Colors", "C",', 'pManager.AddColourParameter("Colors", "Colors",')
text = text.replace('pManager.AddIntegerParameter("Steps", "N",', 'pManager.AddIntegerParameter("Steps", "Steps",')
text = text.replace('pManager.AddColourParameter("Generated Colors", "C",', 'pManager.AddColourParameter("Generated Colors", "Colors",')

with open("Components/GradientGenerator.cs", "w") as f:
    f.write(text)
