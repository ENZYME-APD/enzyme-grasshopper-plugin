"""
[INPUTS]
TerrainMesh  : Mesh (Item Access) - The unified topological surface.
FlowPaths    : Curve (Tree Access) - The flow lines generated from the Raindrop Engine.
VisualScale  : float (Item Access) - Multiplier to intensify the visual color mapping (Try 1.5 to 3.0).

[OUTPUTS]
HeatmapMesh      : Mesh (Item Access) - The colored terrain mesh displaying flow accumulation.
VertexCounts     : int (Tree Access) - Raw accumulation data mapped 1-to-1 with mesh vertices.
Instructions_Out : string (Item Access) - Node configuration guide.
"""

import time
import math
import Rhino.Geometry as rg
import Rhino.Collections as rc
import System.Drawing as sd
from Grasshopper import DataTree
import Grasshopper.Kernel.Data as gh_data

# --- [ASSISTANT-GENERATED COMPONENT METADATA] ---
ghenv.Component.Name = "Flow Accumulation Heatmap"
ghenv.Component.NickName = "FlowHeat"
ghenv.Component.Description = "Generates a flow accumulation heatmap by evaluating water paths against mesh vertices."

Instructions_Out = __doc__

# --- [EXECUTION] ---
start_time = time.perf_counter()

VertexCounts = DataTree[object]()
HeatmapMesh = None
path_count = 0
max_accum = 0

if TerrainMesh and FlowPaths:
    # 1. Duplicate mesh for coloring to preserve original data
    colored_mesh = TerrainMesh.DuplicateMesh()
    
    # CRITICAL FIX: Purge any inherited vertex colors before adding new ones
    colored_mesh.VertexColors.Clear()
    
    # 2. Build a high-speed search index for the mesh vertices
    mesh_pts = rc.Point3dList(colored_mesh.Vertices.ToPoint3dArray())
    vertex_hits = [0] * colored_mesh.Vertices.Count
    
    # 3. Accumulate hits by scanning the FlowPaths
    for i in range(FlowPaths.BranchCount):
        branch = FlowPaths.Branch(i)
        for crv in branch:
            if not crv: continue
            path_count += 1
            
            # Extract polyline points
            success, poly = crv.TryGetPolyline()
            if success:
                for pt in poly:
                    # Find closest mesh vertex and increment its count
                    closest_idx = mesh_pts.ClosestIndex(pt)
                    vertex_hits[closest_idx] += 1
                    
    # 4. Color Gradient Mapping
    # Prevent division by zero if no flow occurred
    max_accum = max(vertex_hits) if vertex_hits else 1
    if max_accum == 0: 
        max_accum = 1 
        
    scale = VisualScale if VisualScale else 1.0
    path_index = gh_data.GH_Path(0)
    
    for i, hits in enumerate(vertex_hits):
        # Apply a square root normalization so lower accumulations visually pop
        normalized = math.sqrt(hits / float(max_accum)) * scale
        intensity = min(1.0, max(0.0, normalized)) # Clamp strictly between 0.0 and 1.0
        
        # Map to color: 0 = Light Gray Terrain, 1.0 = Deep Water Blue
        r = int(220 * (1.0 - intensity) + 10 * intensity)
        g = int(220 * (1.0 - intensity) + 50 * intensity)
        b = int(220 * (1.0 - intensity) + 255 * intensity)
        
        colored_mesh.VertexColors.Add(sd.Color.FromArgb(255, r, g, b))
        
        # Load raw data into the output tree
        VertexCounts.Add(hits, path_index)
        
    HeatmapMesh = colored_mesh

# --- [TELEMETRY & HUD] ---
end_time = time.perf_counter()
duration_ms = (end_time - start_time) * 1000.0

ghenv.Component.Message = (
    f"{ghenv.Component.NickName}\n"
    f"Time: {duration_ms:.1f} ms\n"
    f"---\n"
    f"Paths Evaluated: {path_count}\n"
    f"Peak Accumulation: {max_accum}\n"
    f"● Visual Scale: {VisualScale or 1.0}"
)