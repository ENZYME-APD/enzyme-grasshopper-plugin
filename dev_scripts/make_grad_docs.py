import os

comp = {
    "id": "gradient-generator",
    "name": "Gradient Generator",
    "desc": "Creates an interpolated color gradient based on a list of input colors and a number of steps.",
    "nickname": "GradientGen",
    "category": "Utilities",
    "inputs": [
        ("Colors", "Colour", "List of input colors to interpolate"),
        ("Steps", "Integer", "Number of output colors to generate")
    ],
    "outputs": [
        ("Generated Colors", "Colour", "The interpolated list of colors")
    ]
}

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

if "gradient-generator.html" not in index_html:
    new_card = card_template.format(
        id=comp["id"],
        category=comp["category"],
        cat_lower=comp["category"].lower(),
        name=comp["name"],
        desc=comp["desc"],
        num_in=len(comp["inputs"]),
        num_out=len(comp["outputs"])
    )
    insert_marker = '<div class="components-grid">'
    index_html = index_html.replace(insert_marker, insert_marker + "\n" + new_card)
    with open('docs/index.html', 'w') as f:
        f.write(index_html)
        print("Updated docs/index.html with Gradient Generator card")
