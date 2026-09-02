import os
import re

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
            
            <div class="workflow-section" style="margin: 2rem 0; padding: 1.5rem; background: #27272a; border-radius: 8px; border-left: 4px solid #3b82f6;">
                <h3 style="margin-top: 0; color: #60a5fa;">Workflow & Methodology</h3>
                {workflow_html}
            </div>
            
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

components = [
    {
        "id": "hydro-dem-engine",
        "name": "Hydro-DEM Engine",
        "desc": "Calculates Flow Direction and Flow Accumulation on a terrain mesh to extract stream networks.",
        "nickname": "HydroDEM",
        "category": "LEAP",
        "workflow": """<p><strong>Phase: Locate & Evaluate (TNFD/LEAP)</strong></p>
        <p>This engine acts as the foundational step for hydrological analysis. Traditional GIS algorithms rely on strict raster grids, but this engine is built directly on Rhino's <code>TopologyVertices</code> graph, allowing it to process standard parametric meshes, chaotic Delaunay triangulations, or native surveyor point clouds seamlessly.</p>
        <p><strong>How it works:</strong></p>
        <ol>
            <li><strong>Steepest Descent:</strong> It analyzes every vertex on your 3D mesh, calculates the slope to all connected neighbors, and maps the steepest path downwards.</li>
            <li><strong>Flow Accumulation:</strong> It sorts the terrain from highest peak to lowest valley, cascading simulated water drop by drop until it pools in sinks.</li>
            <li><strong>Stream Extraction:</strong> Using your `Threshold` slider, it traces these high-accumulation paths to generate continuous, clean Polylines that map exactly where water flows and where erosion is most likely to occur.</li>
        </ol>""",
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
        "id": "keypoint-finder",
        "name": "Keypoint Finder",
        "desc": "Analyzes stream slopes to mathematically isolate the inflection point (Keypoint) and extracts the Master Keyline contour.",
        "nickname": "Keypoint",
        "category": "LEAP",
        "workflow": """<p><strong>Phase: Prepare (TNFD/LEAP)</strong></p>
        <p>In regenerative agriculture (specifically P.A. Yeomans' Keyline Design), the "Keypoint" is the critical inflection point in a primary valley where the slope transitions from steep (convex) to flat (concave). Identifying this manually on a jagged surveyor mesh is highly error-prone.</p>
        <p><strong>How it works:</strong></p>
        <ol>
            <li><strong>Slope Analysis:</strong> The component walks down the stream polyline (from the Hydro-DEM engine) and calculates the exact gradient of every geometric segment.</li>
            <li><strong>Noise Filtering:</strong> Using a moving-average window, it smooths out minor mesh bumps and artifacts to prevent "false" keypoints.</li>
            <li><strong>Inflection Isolation:</strong> It isolates the specific vertex experiencing the maximum deceleration (the greatest steep-to-flat transition) and outputs this as the <code>Keypoint</code>.</li>
            <li><strong>Master Keyline Extraction:</strong> If the Terrain mesh is provided, it slices the site horizontally at the exact elevation of the Keypoint, filters out disconnected site contours, and outputs the true Master Keyline contour.</li>
        </ol>""",
        "inputs": [
            ("Terrain", "Mesh", "The base topography mesh"),
            ("Streams", "Curve", "Stream curves from Hydro-DEM"),
            ("Smoothing", "Integer", "Smoothing window for slope analysis (helps ignore mesh noise)")
        ],
        "outputs": [
            ("Keypoints", "Point", "The identified points of inflection (steep to flat)"),
            ("Master Keylines", "Curve", "The specific horizontal terrain contours passing through the Keypoints")
        ]
    },
    {
        "id": "keyline-pattern-engine",
        "name": "Keyline Pattern Engine",
        "desc": "Generates parametric plowing lines or swale networks by offsetting guide curves along a terrain mesh.",
        "nickname": "Keyline",
        "category": "LEAP",
        "workflow": """<p><strong>Phase: Prepare (TNFD/LEAP)</strong></p>
        <p>Standard contour plowing keeps water static. Keyline plowing forces water to move from wet, eroding valleys out toward dry ridges by plowing <em>slightly off-contour</em>. Grasshopper natively struggles with geodesic mesh offsets, so this engine provides a robust 2.5D parametric solution.</p>
        <p><strong>How it works:</strong></p>
        <ol>
            <li><strong>Base Contour:</strong> You provide the Master Keyline (from the Keypoint Finder) as the <code>Guide Curve</code>.</li>
            <li><strong>2D Patterning:</strong> The engine offsets this curve outward perfectly parallel in the horizontal plane using your specified tractor/swale <code>Spacing</code>.</li>
            <li><strong>3D Raycasting:</strong> It raycasts these sprawling offset networks straight down onto the 3D terrain mesh.</li>
            <li><strong>The Result:</strong> Because the valleys and ridges have different slopes, these parallel lines naturally warp and fall slightly off-contour, creating the exact geometric pattern required to passively distribute rainwater and restore soil hydration.</li>
        </ol>""",
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
        "nickname": "DataVis",
        "category": "LEAP",
        "workflow": """<p><strong>Performance & Visualization</strong></p>
        <p>Visualizing big data (like 100,000+ points of flow accumulation) usually crashes Grasshopper if you try to bake or render individual geometry objects like Spheres or Cylinders. This component bypasses Grasshopper's bottleneck by mathematically generating a single, unified mesh painted with Vertex Colors.</p>
        <p><strong>How it works:</strong></p>
        <ol>
            <li><strong>Data Normalization:</strong> It takes your raw values, finds the absolute minimum and maximum, and normalizes the entire dataset.</li>
            <li><strong>Domain Mapping:</strong> It scales the physical size of the geometry (Radius or Height) precisely into your target <code>Domain</code> interval.</li>
            <li><strong>Color Interpolation:</strong> It smoothly samples your provided <code>Colors</code> palette to generate a gradient based on the normalized values.</li>
            <li><strong>Low-Poly Meshing:</strong> Based on your <code>Type</code> selection, it generates highly optimized mesh primitives (e.g., mathematically perfect Icosahedrons for Spheres, or Hexagonal Prisms for Bars), assigns the vertex colors, and merges them into one single mesh for instant viewport rendering.</li>
        </ol>""",
        "inputs": [
            ("Points", "Point", "List of 3D Points"),
            ("Values", "Number", "List of data values matching the points"),
            ("Colors", "Colour", "Gradient color palette"),
            ("Domain", "Interval", "Target domain for geometry size (Radius/Height)"),
            ("Type", "Integer", "Visual Type (0: Bar, 1: Flat Dot, 2: Sphere)"),
            ("Bar Thickness", "Number", "List of Thicknesses for Bar Chart (Type 0 only)")
        ],
        "outputs": [
            ("Visualization", "Mesh", "A single joined mesh representing the data (for fast viewport rendering)")
        ]
    }
]

# Generate/Update pages
for comp in components:
    inputs_html = "\n                        ".join([f"<li><strong>{i[0]}</strong> ({i[1]}): {i[2]}</li>" for i in comp["inputs"]])
    outputs_html = "\n                        ".join([f"<li><strong>{o[0]}</strong> ({o[1]}): {o[2]}</li>" for o in comp["outputs"]])
    
    html = html_template.format(
        name=comp["name"],
        desc=comp["desc"],
        category=comp["category"],
        cat_lower=comp["category"].lower(),
        nickname=comp["nickname"],
        workflow_html=comp["workflow"],
        num_in=len(comp["inputs"]),
        num_out=len(comp["outputs"]),
        inputs_html=inputs_html,
        outputs_html=outputs_html
    )
    
    filepath = f"docs/components/{comp['id']}.html"
    with open(filepath, 'w') as f:
        f.write(html)
        print(f"Updated {filepath}")
        
# Check index.html to add KeypointFinder if missing
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

# We only need to append KeypointFinder to index.html if it's not there
kp = components[1]
if "keypoint-finder.html" not in index_html:
    new_card = card_template.format(
        id=kp["id"],
        category=kp["category"],
        cat_lower=kp["category"].lower(),
        name=kp["name"],
        desc=kp["desc"],
        num_in=len(kp["inputs"]),
        num_out=len(kp["outputs"])
    )
    insert_marker = '<div class="components-grid">'
    index_html = index_html.replace(insert_marker, insert_marker + "\n" + new_card)
    with open('docs/index.html', 'w') as f:
        f.write(index_html)
        print("Updated docs/index.html with KeypointFinder card")

