import os

# 1. Update index.html
with open("docs/index.html", "r") as f:
    idx = f.read()

idx = idx.replace('auto-grid-raindrop-flow-engine.html', 'raindrop-flow-engine.html')
idx = idx.replace('Auto-Grid Raindrop Flow Engine', 'Raindrop Flow Engine')

idx = idx.replace('global-volumetric-flood-engine.html', 'global-flood-engine.html')
idx = idx.replace('Global Volumetric Flood Engine', 'Global Flood Engine')

idx = idx.replace('keypoint-finder.html', 'keypoint-engine.html')
idx = idx.replace('Keypoint Finder', 'Keypoint Engine')

idx = idx.replace('keyline-pattern-engine.html', 'keyline-engine.html')
idx = idx.replace('Keyline Pattern Engine', 'Keyline Engine')

# Include the Water Analysis Guide in the header or somewhere? Let's just add a link to the Markdown file in the intro.
intro_old = '<p>A suite of advanced utilities and terrain analysis components designed to streamline computational workflows in Rhino and Grasshopper.</p>'
intro_new = '<p>A suite of advanced utilities and terrain analysis components designed to streamline computational workflows in Rhino and Grasshopper. Check out the <a href="Water_Analysis_Guide.md" style="color:#007acc;">Water Analysis Guide</a> to learn how the Terrain and LEAP hydrology components work together.</p>'
if intro_old in idx:
    idx = idx.replace(intro_old, intro_new)

with open("docs/index.html", "w") as f:
    f.write(idx)

# 2. Update individual component HTML titles & h1
files_to_update = {
    "docs/components/raindrop-flow-engine.html": "Raindrop Flow Engine",
    "docs/components/global-flood-engine.html": "Global Flood Engine",
    "docs/components/keypoint-engine.html": "Keypoint Engine",
    "docs/components/keyline-engine.html": "Keyline Engine"
}

for filepath, new_name in files_to_update.items():
    if os.path.exists(filepath):
        with open(filepath, "r") as f:
            content = f.read()
        
        # Replace old title strings with new ones (rough replace, might catch other instances but should be fine)
        # We'll just replace the exact old strings.
        old_names = {
            "Raindrop Flow Engine": "Auto-Grid Raindrop Flow Engine",
            "Global Flood Engine": "Global Volumetric Flood Engine",
            "Keypoint Engine": "Keypoint Finder",
            "Keyline Engine": "Keyline Pattern Engine"
        }
        
        content = content.replace(old_names[new_name], new_name)
        
        with open(filepath, "w") as f:
            f.write(content)

