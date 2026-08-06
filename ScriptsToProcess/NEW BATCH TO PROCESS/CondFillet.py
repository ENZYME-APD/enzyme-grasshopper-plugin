"""
================================================================================
Name: Conditional Fillet
Nickname: CondFillet
Version: 5.5 (Precision Solver + Tree Preservation)

DESCRIPTION:
Standard fillet components apply a single radius to an entire curve, which fails 
if any segment is too short. This "smart" solver evaluates and fillets a curve 
corner by corner. It dynamically shrinks or skips fillets on tight corners to 
prevent geometry self-intersections, while keeping your desired radius on the 
wider corners.

HOW IT WORKS:
1. Tangency Check: If segments are already smooth (G1), it skips the fillet.
2. Threshold Limit: Calculates a safe max radius using the shortest adjacent 
   segment and trigonometric angle limits to prevent overshooting.
3. Logic Application: Applies the ideal radius, clamps it to the limit, or 
   skips the corner entirely based on your boolean toggle.
4. Rounding: Always rounds DOWN (floors) the final applied radius to the 
   specified decimal count for a consistent, safe methodology.

INPUTS:
- curves:        (Tree / Curve) Input geometry (Polylines, Curves, Arcs).
- radius:        (Item / Float) The ideal fillet size you want.
- threshold_pct: (Item / Float) Max % of a segment the fillet can consume.
- skip:          (Item / Boolean) True to skip tight corners; False to clamp.
- round_dec:     (Item / Integer) [Optional] Decimals to round down to.

OUTPUTS:
- out_curves:    (Tree) The final filleted geometry. (Preserves empty branches)
- radii:         (Tree) The exact radius applied to each specific corner.
================================================================================
"""

import Rhino.Geometry as rg
import Grasshopper as gh
from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path
from Grasshopper.Kernel import GH_RuntimeMessageLevel as Level
import math
import time

# --- Component Metadata & UI ---
ghenv.Component.Name = "Conditional Fillet"
ghenv.Component.NickName = "CondFillet"

# 1. Determine 'skip' mode
is_skipping = skip if 'skip' in globals() and skip is not None else False
status_msg = "SKIP: ON" if is_skipping else "SKIP: OFF (Clamp)"

# 2. Determine 'round' mode
r_dec = round_dec if 'round_dec' in globals() and round_dec is not None else None
if r_dec is not None:
    round_msg = "ROUND: {} dec (Floor)".format(int(r_dec))
else:
    round_msg = "ROUND: OFF"

# 3. Build Base UI Message
base_msg = "{}\n{}".format(status_msg, round_msg)
ghenv.Component.Message = base_msg + "\nRunning..."

def fillet_universal(crv, r_input, pct, skip_mode, r_decimals):
    # Safety Cap: Fillet must never cross the segment midpoint
    pct = min(pct, 0.49)
    
    segments = crv.DuplicateSegments()
    if not segments or len(segments) < 2: 
        return crv, [0.0] * (len(segments) + 1 if segments else 2)
    
    num_segs = len(segments)
    is_closed = crv.IsClosed
    corner_count = num_segs if is_closed else num_segs + 1
    
    calc_radii = []
    arcs = [None] * corner_count
    domains = [[seg.Domain.Min, seg.Domain.Max] for seg in segments]
    
    calc_tol = 0.001
    
    # ISOLATE ARCS & CALCULATE EXACT TRIMS
    for i in range(corner_count):
        if not is_closed and (i == 0 or i == corner_count - 1):
            calc_radii.append(0.0)
            continue
            
        idx_prev = (i - 1) % num_segs
        idx_next = i % num_segs
        
        c1 = segments[idx_prev]
        c2 = segments[idx_next]
        
        # --- G1 TANGENCY CHECK ---
        v1 = c1.TangentAt(c1.Domain.Max)
        v2 = c2.TangentAt(c2.Domain.Min)
        v1.Unitize()
        v2.Unitize()
        
        angle = rg.Vector3d.VectorAngle(v1, v2)
        
        # Skip perfectly smooth joints (less than ~2.8 degrees deviation)
        if angle < 0.05:
            calc_radii.append(0.0)
            continue
            
        # --- MATHEMATICAL LIMIT CALCULATIONS ---
        len_limit = min(c1.GetLength(), c2.GetLength()) * pct
        half_angle = angle / 2.0
        
        try:
            r_max = len_limit / math.tan(half_angle)
            limit = min(len_limit, r_max)
        except:
            limit = len_limit
            
        # Apply Threshold Logic
        r = r_input
        if r > limit:
            r = 0.0 if skip_mode else limit
            
        # --- ROUNDING LOGIC (Always Round Down) ---
        if r_decimals is not None:
            factor = 10.0 ** r_decimals
            r = math.floor(r * factor) / factor
            
        calc_radii.append(r)
        
        # --- GENERATE FILLET ARC ---
        if r > calc_tol:
            dist_t = r * math.tan(half_angle)
            g1_dist = min(dist_t, c1.GetLength() * 0.9)
            g2_dist = min(dist_t, c2.GetLength() * 0.9)
            
            s1, t1 = c1.LengthParameter(c1.GetLength() - g1_dist)
            p1 = c1.PointAt(t1) if s1 else c1.PointAtNormalizedLength(0.9)
            
            s2, t2 = c2.LengthParameter(g2_dist)
            p2 = c2.PointAt(t2) if s2 else c2.PointAtNormalizedLength(0.1)
            
            res = rg.Curve.CreateFilletCurves(c1, p1, c2, p2, r, False, False, True, calc_tol, 0.1)
            
            if res and len(res) > 0:
                arc = res[0]
                A = arc.PointAtStart
                B = arc.PointAtEnd
                
                sA1, tA1 = c1.ClosestPoint(A)
                sB1, tB1 = c1.ClosestPoint(B)
                
                dA1 = A.DistanceTo(c1.PointAt(tA1)) if sA1 else 999
                dB1 = B.DistanceTo(c1.PointAt(tB1)) if sB1 else 999
                
                if dA1 < dB1:
                    t_cut_c1 = tA1
                    _, t_cut_c2 = c2.ClosestPoint(B)
                    arcs[i] = arc
                else:
                    t_cut_c1 = tB1
                    _, t_cut_c2 = c2.ClosestPoint(A)
                    arc.Reverse()
                    arcs[i] = arc
                    
                domains[idx_prev][1] = t_cut_c1
                domains[idx_next][0] = t_cut_c2

    # RECONSTRUCT DYNAMICALLY
    parts = []
    for i in range(num_segs):
        d_start, d_end = domains[i]
        
        if d_end - d_start > 1e-5:
            trimmed = segments[i].Trim(rg.Interval(d_start, d_end))
            if trimmed and trimmed.GetLength() > calc_tol:
                parts.append(trimmed)
                
        if is_closed:
            next_corner = (i + 1) % num_segs
            if arcs[next_corner]: parts.append(arcs[next_corner])
        else:
            if i < num_segs - 1:
                if arcs[i+1]: parts.append(arcs[i+1])

    # AIRTIGHT JOINING
    if len(parts) > 0:
        joined = rg.Curve.JoinCurves(parts, 0.01)
        if joined and len(joined) > 0:
            if len(joined) == 1:
                return joined[0], calc_radii
            else:
                pc = rg.PolyCurve()
                for p in parts: pc.Append(p)
                if pc.IsValid: return pc, calc_radii
                return joined[0], calc_radii
            
    return crv, calc_radii

# --- Main Execution & Profiler ---
start_time = time.time()

tree_curves = DataTree[object]() 
tree_radii = DataTree[object]() 

if 'curves' in globals() and curves is not None:
    for i in range(curves.BranchCount):
        branch = curves.Branch(i)
        path = curves.Paths[i]
        
        # PRESERVATION 1: Ensure completely empty branches are maintained
        if branch.Count == 0:
            tree_curves.EnsurePath(path)
            tree_radii.EnsurePath(path)
            continue
        
        for j, crv in enumerate(branch):
            radii_path = path.AppendElement(j)
            
            # PRESERVATION 2: Ensure Null items hold their list index
            if crv is None:
                tree_curves.Add(None, path)
                tree_radii.EnsurePath(radii_path) # Sub-path exists but is empty
            else:
                # Normal Execution
                new_crv, radii_list = fillet_universal(crv, radius, threshold_pct, is_skipping, r_dec)
                tree_curves.Add(new_crv, path)
                
                for r_val in radii_list:
                    tree_radii.Add(r_val, radii_path)

# Calculate elapsed time in milliseconds
elapsed_ms = (time.time() - start_time) * 1000.0

# Final UI Update: Combine Base Message + Profiler Time
ghenv.Component.Message = "{}\nTime: {:.1f} ms".format(base_msg, elapsed_ms)

# Assign to component outputs
out_curves = tree_curves
radii = tree_radii