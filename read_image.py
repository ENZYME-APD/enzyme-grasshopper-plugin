from PIL import Image
import pytesseract
img = Image.open("/Users/jb/.gemini/antigravity/brain/a7e00657-d94f-4987-96d6-c925345dc377/.user_uploaded/media_1788396401727.png")
cropped = img.crop((100, 100, 600, 600))
text = pytesseract.image_to_string(cropped)
print(text)
