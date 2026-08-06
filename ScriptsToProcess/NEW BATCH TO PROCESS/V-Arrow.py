"""
JB Grasshopper Gem | High-Performance Geometry Logic
Component: Vector Arrow Generator
Optimized for: Minimal Input, Clean Boolean Output, and UI Feedback
"""

import Rhino.Geometry as rg
import time

def SetComponentInfo():
    """Sets the Component Identity Metadata"""
    ghenv.Component.Name = "Vector Arrow Generator"
    ghenv.Component.NickName = "V-Arrow"
    ghenv.Component.Description = "Generates high-fidelity 2D arrow outlines and meshes from input lines with custom mode logic."

def solve_arrows():
    # Start performance timer
    start_time = time.time()
    
    # 1. Component Metadata Setup
    SetComponentInfo()
    
    # 2. Validation Gate: Ensure 'Lines' input exists and is not empty
    if 'Lines' not in globals() or not Lines: 
        ghenv.Component.Message = "Awaiting Lines..."
        return [], []
    
    # 3. Parameter Parsing with JB 'Lean' defaults
    # Mode: 0=End, 1=Start, 2=Double
    m_val = int(Mode) if ('Mode' in globals() and Mode is not None) else 2
    bw = float(BodyWidth) if ('BodyWidth' in globals() and BodyWidth is not None) else 0.5
    hw = float(HeadWidth) if ('HeadWidth' in globals() and HeadWidth is not None) else 1.5
    hl = float(HeadLength) if ('HeadLength' in globals() and HeadLength is not None) else 2.0
    
    # Mode Mapping for UX Clarity
    mode_labels = {0: "End Head", 1: "Start Head", 2: "Double Head"}
    current_mode_str = mode_labels.get(m_val, "Custom")

    arrow2D = []
    arrowMesh = []

    # 4. Geometry Processing Loop
    for ln in Lines:
        if ln is None or not ln.IsValid: 
            continue
        
        p_start, p_end = ln.From, ln.To
        v_dir = p_end - p_start
        v_length = v_dir.Length
        
        # Safety Check: Length must exceed head length to avoid inverted geometry
        if v_length < (hl * 1.1): 
            continue
        
        v_dir.Unitize()
        # Create perpendicular vector for width offsets
        v_perp = rg.Vector3d(-v_dir.Y, v_dir.X, 0)
        
        parts = []
        half_bw = bw * 0.5
        half_hw = hw * 0.5

        # Helper: Generate Arrowhead Polylines
        def get_head_pts(anchor, direction, is_end):
            rev = 1 if is_end else -1
            tip = anchor
            base_center = anchor - (direction * hl * rev)
            side_a = base_center + (v_perp * half_hw)
            side_b = base_center - (v_perp * half_hw)
            return [tip, side_a, side_b, tip]

        # Shaft logic: Adjust endpoints to overlap with heads for a clean Boolean Union
        s_start, s_end = p_start, p_end
        overlap = hl * 0.1
        if m_val == 0 or m_val == 2: s_end -= v_dir * (hl - overlap)
        if m_val == 1 or m_val == 2: s_start += v_dir * (hl - overlap)
        
        shaft_pts = [
            s_start + v_perp * half_bw,
            s_end + v_perp * half_bw,
            s_end - v_perp * half_bw,
            s_start - v_perp * half_bw,
            s_start + v_perp * half_bw
        ]
        parts.append(rg.Polyline(shaft_pts).ToPolylineCurve())

        # Add heads based on selected Mode
        if m_val == 0 or m_val == 2: 
            parts.append(rg.Polyline(get_head_pts(p_end, v_dir, True)).ToPolylineCurve())
        if m_val == 1 or m_val == 2: 
            parts.append(rg.Polyline(get_head_pts(p_start, v_dir, False)).ToPolylineCurve())

        # 5. Data Consolidation: Boolean Union
        # This merges the overlapping shaft and head(s) into one single closed curve
        merged_crvs = rg.Curve.CreateBooleanUnion(parts, 0.001)
        
        if merged_crvs:
            for c in merged_crvs:
                arrow2D.append(c)
                # Generate planar mesh for better viewport visualization
                pm = rg.Mesh.CreateFromPlanarBoundary(c, rg.MeshingParameters.Default, 0.001)
                if pm: arrowMesh.append(pm)

    # 6. Dynamic UI Feedback & Performance Telemetry
    end_time = time.time()
    msec = (end_time - start_time) * 1000
    ghenv.Component.Message = "{}\nn: {} | {:.1f}ms".format(current_mode_str, len(arrowMesh), msec)
    
    return arrow2D, arrowMesh

# Execute Logic
Arrow2D, ArrowMesh = solve_arrows()