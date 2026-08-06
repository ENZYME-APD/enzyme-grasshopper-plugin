import Rhino.Geometry as rg
from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path
import time

# --- Component Metadata ---
ghenv.Component.Name = "Curve Segment Dispatcher"
ghenv.Component.NickName = "SegDisp"
ghenv.Component.Description = "Explodes curves into Lines and Arcs, extracting Radii, Centers, and visual Dimensions."

# Start the timer
start_time = time.time()

# Initialize DataTrees
tree_lines = DataTree[object]()
tree_arcs = DataTree[object]()
tree_radii = DataTree[object]()
tree_centers = DataTree[object]()
tree_dims = DataTree[object]()

def process_geometry(crv, r_offset, show_dims, decimals, path):
    # Explode curve into segments
    segments = crv.DuplicateSegments()
    if not segments or len(segments) == 0:
        segments = [crv]

    for seg in segments:
        if seg is None: continue
        
        # 1. Handle Linear Segments
        if seg.IsLinear():
            tree_lines.Add(seg, path)
            
        # 2. Handle Arcs/Curves
        else:
            success, arc_prim = seg.TryGetArc(0.001)
            if success:
                tree_arcs.Add(seg, path)
                tree_radii.Add(arc_prim.Radius, path)
                tree_centers.Add(arc_prim.Center, path)
                
                # 3. Create Visual Dimensions if toggled ON
                if show_dims:
                    mid_angle = arc_prim.AngleDomain.Mid
                    pt_on_arc = arc_prim.PointAt(mid_angle)
                    
                    dir_vec = pt_on_arc - arc_prim.Center
                    dir_vec.Unitize()
                    
                    offset_pt = pt_on_arc + (dir_vec * r_offset)
                    
                    # Create leader line
                    leader_line = rg.Line(pt_on_arc, offset_pt).ToNurbsCurve()
                    
                    # Create Text Dot with Dynamic Decimals
                    fmt = "R {:." + str(decimals) + "f}"
                    label = fmt.format(arc_prim.Radius)
                    
                    text_dot = rg.TextDot(label, offset_pt)
                    
                    tree_dims.Add(leader_line, path)
                    tree_dims.Add(text_dot, path)

# --- Main Execution ---
# Sanitize Inputs
val_offset = offset if 'offset' in globals() and offset is not None else 10.0
val_toggle = dim_toggle if 'dim_toggle' in globals() and dim_toggle is not None else True
val_decimals = int(num_dec) if 'num_dec' in globals() and num_dec is not None else 2

if 'curves' in globals() and curves is not None:
    for i in range(curves.BranchCount):
        branch = curves.Branch(i)
        path = curves.Paths[i]
        for crv in branch:
            if crv: 
                process_geometry(crv, val_offset, val_toggle, val_decimals, path)

# Stop the timer and calculate duration
elapsed_ms = (time.time() - start_time) * 1000.0

# Update Component Message
status_str = "DIMS: ON" if val_toggle else "DIMS: OFF"
total_count = tree_lines.DataCount + tree_arcs.DataCount
# Line 1: Toggle & Decimal setting | Line 2: Stats & Time
ghenv.Component.Message = "{} ({} dec)\n{} Segs | {:.2f} ms".format(status_str, val_decimals, total_count, elapsed_ms)

# Assign to outputs
lines = tree_lines
arcs = tree_arcs
radii = tree_radii
centers = tree_centers
dims = tree_dims