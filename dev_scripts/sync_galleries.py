import os
import glob
import re

components_dir = "docs/components"
images_dir = "docs/images/components"

for html_file in glob.glob(os.path.join(components_dir, "*.html")):
    comp_id = os.path.basename(html_file).replace(".html", "")
    comp_img_dir = os.path.join(images_dir, comp_id)
    
    # Check if there are any images
    if not os.path.exists(comp_img_dir):
        continue
        
    images = [f for f in os.listdir(comp_img_dir) if f.lower().endswith(('.png', '.jpg', '.jpeg', '.gif'))]
    images.sort()
    
    with open(html_file, 'r', encoding='utf-8') as f:
        content = f.read()
        
    # Remove existing gallery if it exists
    content = re.sub(r'<div class="component-gallery">.*?</div><!-- end-gallery -->\n*', '', content, flags=re.DOTALL)
    
    if images:
        gallery_html = '<div class="component-gallery" style="margin: 2rem 0; display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1rem;">\n'
        for img in images:
            img_path = f"../images/components/{comp_id}/{img}"
            gallery_html += f'    <img src="{img_path}" alt="{img}" style="width: 100%; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.3); border: 1px solid #3f3f46;">\n'
        gallery_html += '</div><!-- end-gallery -->\n\n            '
        
        # Inject before io-grid
        content = content.replace('<div class="io-grid">', gallery_html + '<div class="io-grid">')
        
        with open(html_file, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Added {len(images)} images to {comp_id}.html")

print("Gallery sync complete!")
