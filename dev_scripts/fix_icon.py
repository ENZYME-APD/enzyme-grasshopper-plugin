from PIL import Image

def fix_icon():
    # Load the image
    img = Image.open('Resources/enzyme_logo.png').convert("RGBA")
    
    # Process pixels: make pure white (or near white) transparent
    datas = img.getdata()
    newData = []
    for item in datas:
        # If it's very light, make it transparent
        if item[0] > 240 and item[1] > 240 and item[2] > 240:
            newData.append((255, 255, 255, 0))
        else:
            newData.append(item)
            
    img.putdata(newData)
    
    # Resize with high-quality resampling
    img = img.resize((24, 24), Image.Resampling.LANCZOS)
    
    # Save the new icon
    img.save('Resources/enzyme_logo_24.png', "PNG")

if __name__ == "__main__":
    fix_icon()
