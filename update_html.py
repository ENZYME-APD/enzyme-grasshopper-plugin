import re

with open("docs/components/flow-accumulation-heatmap.html", "r") as f:
    html = f.read()

overview_old = '''            <h2>Overview</h2>
            <p>Generates a flow accumulation heatmap by evaluating water paths against mesh vertices.</p>'''

overview_new = '''            <h2>Overview</h2>
            <p>Generates a flow accumulation heatmap by evaluating water paths against mesh vertices. It measures <strong>surface water runoff concentration</strong> by finding where multiple simulated flow paths converge and overlap, keeping a "hit counter" for every vertex.</p>
            
            <h3>What is it measuring?</h3>
            <p>It measures the density of water traffic. Vertices where many flow paths converge and overlap get a high accumulation score, while ridges or peaks where water flows away get a score of zero.</p>
            
            <h3>Why is it relevant for Site Analysis?</h3>
            <ul>
                <li><strong>Identifying Natural Drainage:</strong> Reveals the invisible natural hydrological network of a site, showing exactly where water wants to channel (valleys, swales, and streams).</li>
                <li><strong>Flood & Ponding Risk:</strong> High accumulation areas indicate where water will pool during heavy rain events.</li>
                <li><strong>Erosion Control:</strong> Areas with intense, concentrated flow paths (especially on steep slopes) are highly susceptible to soil erosion and might require retaining walls, deep-rooted planting, or terracing.</li>
                <li><strong>Infrastructure Placement:</strong> Tells engineers and landscape architects exactly where to place stormwater management infrastructure, such as culverts, bioswales, retention ponds, and French drains.</li>
            </ul>'''

html = html.replace(overview_old, overview_new)

with open("docs/components/flow-accumulation-heatmap.html", "w") as f:
    f.write(html)
