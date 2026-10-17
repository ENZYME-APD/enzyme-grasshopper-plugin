from PIL import Image

def resize_clean_icon():
    # Load the clean image provided by the user
    img = Image.open('/Users/jb/.gemini/antigravity/brain/a7e00657-d94f-4987-96d6-c925345dc377/.user_uploaded/media_1787303696106.png')
    
    # Ensure it's RGBA (keeps native transparency)
    img = img.convert("RGBA")
    
    # Resize with high-quality resampling to 24x24
    img = img.resize((24, 24), Image.Resampling.LANCZOS)
    
    # Overwrite the old logo file
    img.save('Resources/enzyme_logo_24.png', "PNG")

if __name__ == "__main__":
    resize_clean_icon()
