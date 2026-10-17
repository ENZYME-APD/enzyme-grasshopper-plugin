import os

leap_components = [
    {
        "id": "hydro-dem-engine",
        "name": "Hydro-DEM Engine",
        "desc": "Calculates Flow Direction and Flow Accumulation on a terrain mesh to extract stream networks.",
        "icon": "",
        "nickname": "HydroDEM",
        "category": "LEAP",
        "inputs": [
            ("Terrain", "Mesh", "The base topography mesh"),
            ("Threshold", "Integer", "Minimum flow accumulation to form a stream")
        ],
        "outputs": [
            ("Streams", "Curve", "Extracted stream networks (Polylines)"),
            ("Accumulation", "Integer", "Flow accumulation value per topology vertex"),
            ("Topology Points", "Point", "Topology vertices matching the accumulation list")
        ]
    },
    {
        "id": "keyline-pattern-engine",
        "name": "Keyline Pattern Engine",
        "desc": "Generates parametric plowing lines or swale networks by offsetting guide curves along a terrain mesh.",
        "icon": "",
        "nickname": "Keyline",
        "category": "LEAP",
        "inputs": [
            ("Terrain", "Mesh", "The base topography mesh"),
            ("Guide Curves", "Curve", "The reference contours or keylines"),
            ("Spacing", "Number", "Horizontal distance between plowing lines"),
            ("Count", "Integer", "Number of parallel lines to generate per side")
        ],
        "outputs": [
            ("Keylines", "Curve", "Generated 3D swale/plow curves projected on terrain")
        ]
    },
    {
        "id": "data-visualizer",
        "name": "Data Visualizer",
        "desc": "Visualizes points and data values as a fast gradient mesh (Bars, Dots, or Spheres).",
        "icon": "",
        "nickname": "DataVis",
        "category": "LEAP",
        "inputs": [
            ("Points", "Point", "List of 3D Points"),
            ("Values", "Number", "List of data values matching the points"),
            ("Colors", "Colour", "Gradient color palette"),
            ("Domain", "Interval", "Target domain for geometry size (Radius/Height)"),
            ("Type", "Integer", "Visual Type (0: Bar, 1: Flat Dot, 2: Sphere)"),
            ("Bar Thickness", "Number", "Thickness for Bar Chart (Type 0 only)")
        ],
        "outputs": [
            ("Visualization", "Mesh", "A single joined mesh representing the data (for fast viewport rendering)")
        ]
    }
]

html_template = """<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{name} - Enzyme Plugin</title>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="../css/style.css">
</head>
<body>
    <nav class="navbar">
        <a href="../index.html" class="nav-brand">
            <img src="../images/enzyme_logo.png" alt="Enzyme Logo" class="logo-img">
        </a>
        <div class="nav-links">
            <a href="../index.html#components">Components</a>
            <a href="https://github.com/enzyme-apd/enzyme-grasshopper-plugin" target="_blank">GitHub</a>
        </div>
    </nav>

    <main class="container doc-page">
        <div class="doc-header" style="display: flex; align-items: center;">
            <div>
                <h1>{name}</h1>
                <p class="doc-subtitle">{desc}</p>
                <div class="doc-meta">
                    <span class="badge {cat_lower}">{category}</span>
                    <span>Nickname: <strong>{nickname}</strong></span>
                </div>
            </div>
        </div>

        <div class="doc-content">
            <h2>Overview</h2>
            <p>{desc}</p>
            
            <div class="io-grid">
                <div class="io-col">
                    <h3>Inputs ({num_in})</h3>
                    <ul>
                        {inputs_html}
                    </ul>
                </div>
                <div class="io-col">
                    <h3>Outputs ({num_out})</h3>
                    <ul>
                        {outputs_html}
                    </ul>
                </div>
            </div>
        </div>
    </main>

    <footer>
        <p><a href="https://github.com/enzyme-apd/enzyme-grasshopper-plugin" target="_blank" style="color: #a1a1aa; text-decoration: none; border-bottom: 1px solid #3f3f46; padding-bottom: 2px;">View on GitHub</a></p>
    </footer>
</body>
</html>"""

# Generate pages
for comp in leap_components:
    inputs_html = "\n                        ".join([f"<li><strong>{i[0]}</strong> ({i[1]}): {i[2]}</li>" for i in comp["inputs"]])
    outputs_html = "\n                        ".join([f"<li><strong>{o[0]}</strong> ({o[1]}): {o[2]}</li>" for o in comp["outputs"]])
    
    html = html_template.format(
        name=comp["name"],
        desc=comp["desc"],
        category=comp["category"],
        cat_lower=comp["category"].lower(),
        nickname=comp["nickname"],
        num_in=len(comp["inputs"]),
        num_out=len(comp["outputs"]),
        inputs_html=inputs_html,
        outputs_html=outputs_html
    )
    
    filepath = f"docs/components/{comp['id']}.html"
    with open(filepath, 'w') as f:
        f.write(html)
        print(f"Created {filepath}")

# Update index.html
with open('docs/index.html', 'r') as f:
    index_html = f.read()

card_template = """            <a href="components/{id}.html" class="card" data-category="{cat_lower}">
                <span class="badge {cat_lower}">{category}</span>
                <h3>{name}</h3>
                <p>{desc}</p>
                <div class="card-meta">
                    <span>{num_in} Inputs</span>
                    <span>{num_out} Outputs</span>
                </div>
                <span class="card-link">View docs &rarr;</span>
            </a>\n"""

cards_html = ""
for comp in leap_components:
    cards_html += card_template.format(
        id=comp["id"],
        category=comp["category"],
        cat_lower=comp["category"].lower(),
        name=comp["name"],
        desc=comp["desc"],
        num_in=len(comp["inputs"]),
        num_out=len(comp["outputs"])
    )

insert_marker = '<div class="components-grid">'
if cards_html not in index_html:
    index_html = index_html.replace(insert_marker, insert_marker + "\n" + cards_html)
    with open('docs/index.html', 'w') as f:
        f.write(index_html)
        print("Updated docs/index.html")
