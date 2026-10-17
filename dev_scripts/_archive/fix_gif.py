with open('Components/GifCompiler.cs', 'r') as f:
    content = f.read()
content = content.replace("AnimatedGif.AnimatedGifCreator.Create", "AnimatedGif.AnimatedGif.Create")
with open('Components/GifCompiler.cs', 'w') as f:
    f.write(content)
