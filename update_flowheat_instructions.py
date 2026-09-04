import re

with open("Components/FlowHeat.cs", "r") as f:
    ts = f.read()

# We need to replace the string instructions block.
old_inst = '''            string instructions = 
                "[INPUTS]\\n" +
                "TerrainMesh  : Mesh (Item Access) - The unified topological surface.\\n" +
                "FlowPaths    : Curve (Tree Access) - The flow lines generated from the Raindrop Engine.\\n" +
                "VisualScale  : float (Item Access) - Multiplier to intensify the visual color mapping (Try 1.5 to 3.0).\\n\\n" +
                "[OUTPUTS]\\n" +
                "HeatmapMesh      : Mesh (Item Access) - The colored terrain mesh displaying flow accumulation.\\n" +
                "VertexCounts     : int (Tree Access) - Raw accumulation data mapped 1-to-1 with mesh vertices.\\n" +
                "Instructions : string (Item Access) - Node configuration guide.";'''

new_inst = '''            string instructions = 
                "FLOW ACCUMULATION HEATMAP\\n" +
                "=========================\\n\\n" +
                "WHAT IT MEASURES:\\n" +
                "Measures surface water runoff concentration by evaluating flow lines (typically from the Raindrop Engine) " +
                "against terrain vertices. Vertices where multiple flow paths converge receive a high accumulation score.\\n\\n" +
                "WHY IT IS RELEVANT FOR SITE ANALYSIS:\\n" +
                "- Natural Drainage: Reveals the invisible hydrological network (valleys, swales, streams).\\n" +
                "- Flood Risk: Identifies high accumulation areas where water will pool during heavy rain.\\n" +
                "- Erosion Control: Highlights intense flow paths susceptible to soil erosion.\\n" +
                "- Infrastructure: Informs placement of culverts, bioswales, and retention ponds.\\n\\n" +
                "[INPUTS]\\n" +
                "TerrainMesh  : Mesh (Item Access) - The unified topological surface.\\n" +
                "FlowPaths    : Curve (Tree Access) - Flow lines generated from the Raindrop Engine.\\n" +
                "VisualScale  : float (Item) - Multiplier to intensify the color mapping (Try 1.5 to 3.0).\\n\\n" +
                "[OUTPUTS]\\n" +
                "HeatmapMesh  : Mesh (Item Access) - Colorized mesh displaying flow accumulation.\\n" +
                "VertexCounts : int (Tree Access) - Raw numerical data of water traffic mapped 1-to-1 with mesh vertices.";'''

ts = ts.replace(old_inst, new_inst)

with open("Components/FlowHeat.cs", "w") as f:
    f.write(ts)
