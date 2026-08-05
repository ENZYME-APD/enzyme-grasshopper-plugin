"""
BIM ATTRIBUTE JSON SERIALIZER (JSON-BUS EDITION)
================================================================================
Eliminates complex DataTrees. Reads metadata directly from referenced Rhino 
geometry via Attribute User Text. Groups, sorts, and serializes into OOP JSON.
* ADDED: Push-To-Rhino modular function to physically move curves to true Z.
* ADDED: Parametric Corner Filleting via JSON Configurator with auto-clamping.
* ADDED: Exact vs "Contains" matching is now evaluated per-rule dynamically.
* FIXED: Enforces strict Counter-Clockwise (CCW) curve orientation after flattening.

INPUTS:
    Guids         (Guid) [List Access] : Referenced Rhino geometry IDs.
    Refresh       (bool) [Item Access] : Wire a Button to force re-read of attributes.
    Fillet_Config (str)  [Item Access] : JSON string defining fillet rules.
    PushToRhino   (bool) [Item Access] : Wire a Button to push elevations to Rhino.

OUTPUTS:
    JSON_Payload  (str)
================================================================================
"""

import json
import Rhino
import Rhino.Geometry as rg
import Grasshopper as gh
import System
import time

# --- METADATA INITIALIZATION & UI WRAPPER ---
def update_ui(msg):
    _ghenv = globals().get('ghenv')
    _comp = globals().get('component')
    if _ghenv: _ghenv.Component.Message = msg
    elif _comp: _comp.Message = msg
    else: print(msg)

_ghenv = globals().get('ghenv')
if _ghenv:
    _ghenv.Component.Name = "BIM Attribute Serializer"
    _ghenv.Component.NickName = "BIM_JSON"
    _ghenv.Component.Description = "Serializes referenced Rhino curves via Attribute User Text."

# ==============================================================================
# SERIALIZATION & FILLET HELPERS
# ==============================================================================
def serialize_exact_curve(crv):
    segments_data = []
    segments = crv.DuplicateSegments()
    if not segments or len(segments) == 0: segments = [crv]
        
    for seg in segments:
        if seg.IsLinear(0.001):
            segments_data.append({
                "type": "Line",
                "start": [round(seg.PointAtStart.X, 4), round(seg.PointAtStart.Y, 4), round(seg.PointAtStart.Z, 4)],
                "end": [round(seg.PointAtEnd.X, 4), round(seg.PointAtEnd.Y, 4), round(seg.PointAtEnd.Z, 4)]
            })
        elif seg.IsArc(0.001):
            rc, arc = seg.TryGetArc()
            if rc:
                segments_data.append({
                    "type": "Arc",
                    "start": [round(arc.StartPoint.X, 4), round(arc.StartPoint.Y, 4), round(arc.StartPoint.Z, 4)],
                    "mid": [round(arc.MidPoint.X, 4), round(arc.MidPoint.Y, 4), round(arc.MidPoint.Z, 4)],
                    "end": [round(arc.EndPoint.X, 4), round(arc.EndPoint.Y, 4), round(arc.EndPoint.Z, 4)]
                })
        else:
            poly_crv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0)
            if poly_crv:
                rc, poly = poly_crv.TryGetPolyline()
                if rc:
                    pts = [[round(pt.X, 4), round(pt.Y, 4), round(pt.Z, 4)] for pt in poly]
                    segments_data.append({"type": "Polyline", "points": pts})
    return segments_data

def get_target_radius(b_id, t_id, prog, rules, fallback):
    def is_match(target, pattern, exact_match):
        if pattern == "*": return True
        if not target: return False
        t_str = str(target).strip().upper()
        p_str = str(pattern).strip().upper()
        if exact_match: return t_str == p_str
        else: return p_str in t_str

    for rule in rules:
        rtype = rule.get("type", "")
        rmatch = rule.get("match", "")
        rrad = float(rule.get("radius", 0.0))
        rexact = bool(rule.get("exact", True)) # Reads the specific rule flag
        
        if rtype == "Tower" and is_match(t_id, rmatch, rexact): return rrad
        if rtype == "Program" and is_match(prog, rmatch, rexact): return rrad
        if rtype == "Building" and is_match(b_id, rmatch, rexact): return rrad
        
    return fallback

def apply_safe_fillet(crv, requested_radius):
    if requested_radius <= 0.001: return crv
    segments = crv.DuplicateSegments()
    if not segments or len(segments) < 2: return crv
    
    min_len = min([seg.GetLength() for seg in segments])
    safe_radius = min(requested_radius, min_len * 0.49)
    
    if safe_radius <= 0.001: return crv
    filleted_crv = rg.Curve.CreateFilletCornersCurve(crv, safe_radius, 0.01, 0.1)
    return filleted_crv if filleted_crv else crv

# ==============================================================================
# MODULAR READ-WRITE HELPER (PUSH TO RHINO)
# ==============================================================================
def push_elevations_to_rhino(guid_to_z_map, execute_push):
    if not execute_push: return 0
    doc = Rhino.RhinoDoc.ActiveDoc
    moved_count = 0
    
    for gid, target_z in guid_to_z_map.items():
        obj = doc.Objects.FindId(gid)
        if not obj: continue
        crv = obj.Geometry
        if not crv: continue
            
        current_z = crv.GetBoundingBox(True).Min.Z
        z_diff = target_z - current_z
        
        if abs(z_diff) > 0.001:
            xform = rg.Transform.Translation(0, 0, z_diff)
            doc.Objects.Transform(gid, xform, True)
            moved_count += 1
            
    return moved_count

# ==============================================================================
# MAIN EXECUTION
# ==============================================================================
def process_bim_attributes():
    exec_start = time.perf_counter()
    
    guids_in = globals().get('Guids', [])
    fillet_json_in = globals().get('Fillet_Config', "")
    push_btn = globals().get('PushToRhino', False)
    
    fillet_rules = []
    default_rad = 0.0
    
    if fillet_json_in:
        try:
            config = json.loads(fillet_json_in)
            default_rad = float(config.get("default_radius", 0.0))
            fillet_rules = config.get("rules", [])
        except Exception as e:
            print("Configurator JSON Error:", e)
    
    clean_guids = [g for g in guids_in if g is not None]
    if not clean_guids: 
        update_ui("BIM_JSON\nTime: 0.0 ms\n---\nAwaiting Data")
        return None
    
    doc = Rhino.RhinoDoc.ActiveDoc
    bldg_data_map = {}
    total_blocks = 0
    
    for gid in clean_guids:
        obj = doc.Objects.FindId(gid)
        if not obj: continue
        crv = obj.Geometry
        if not isinstance(crv, rg.Curve): continue
            
        obj_type = obj.Attributes.GetUserString("Type")
        if obj_type and obj_type.lower() == "core": continue

        bldg_id = obj.Attributes.GetUserString("BuildingID") or "Building_01"
        prog = obj.Attributes.GetUserString("Program") or "Default"
        t_id = obj.Attributes.GetUserString("TowerID") or "Main_Tower"
        
        try: fh = float(obj.Attributes.GetUserString("FloorHeight") or 3.5)
        except: fh = 3.5
        try: flrs = int(obj.Attributes.GetUserString("Floors") or 1)
        except: flrs = 1
        try: phase = int(obj.Attributes.GetUserString("Phase") or 0)
        except: phase = 0
        
        if bldg_id not in bldg_data_map:
            bldg_data_map[bldg_id] = {"blocks": [], "raw_z_coords": []}
            
        crv_min_z = crv.GetBoundingBox(True).Min.Z
        bldg_data_map[bldg_id]["raw_z_coords"].append(crv_min_z)
        
        crv_flat = crv.Duplicate()
        crv_flat.Transform(rg.Transform.PlanarProjection(rg.Plane.WorldXY))
        
        if crv_flat.IsClosed:
            orientation = crv_flat.ClosedCurveOrientation(rg.Plane.WorldXY)
            if orientation == rg.CurveOrientation.Clockwise:
                crv_flat.Reverse()
                
        # Now passing the rules list natively, exactness evaluated inside
        target_radius = get_target_radius(bldg_id, t_id, prog, fillet_rules, default_rad)
        if target_radius > 0:
            crv_flat = apply_safe_fillet(crv_flat, target_radius)
        
        bldg_data_map[bldg_id]["blocks"].append({
            "guid": gid, 
            "program": prog,
            "tower_id": t_id,
            "floor_height": fh,
            "floors": flrs,
            "phase": phase,
            "curve_flat": crv_flat
        })
        total_blocks += 1

    masterplan_dict = {"buildings": []}
    guid_z_map = {} 
    
    for b_id, b_data in bldg_data_map.items():
        min_z = min(b_data["raw_z_coords"]) if b_data["raw_z_coords"] else 0.0
        
        building_dict = {
            "name": b_id, 
            "true_base_elevation": round(min_z, 3),
            "blocks": []
        }
        
        sorted_blocks = sorted(b_data["blocks"], key=lambda x: x["phase"])
        phase_max_z = {-1: 0.0}
        tower_current_z = {}
        
        for i, blk in enumerate(sorted_blocks):
            phase = blk["phase"]
            t_id = blk["tower_id"]
            fh = blk["floor_height"]
            flrs = blk["floors"]
            crv_flat = blk["curve_flat"]
            gid = blk["guid"]
            
            if t_id in tower_current_z:
                current_base_z = tower_current_z[t_id]
            else:
                prev_phases = [p for p in phase_max_z.keys() if p < phase]
                current_base_z = max([phase_max_z[p] for p in prev_phases]) if prev_phases else 0.0
            
            guid_z_map[gid] = current_base_z + min_z
            
            top_z = current_base_z + (fh * flrs)
            tower_current_z[t_id] = top_z
            phase_max_z[phase] = max(phase_max_z.get(phase, 0.0), top_z)
                
            block_dict = {
                "name": "{}_{}".format(blk["program"], i),
                "tower_id": t_id,
                "program": blk["program"],
                "floor_height": fh,
                "floors": flrs,
                "base_z": round(current_base_z, 3), 
                "boundary_segments": serialize_exact_curve(crv_flat)
            }
            building_dict["blocks"].append(block_dict)
            
        masterplan_dict["buildings"].append(building_dict)
        
    moved_count = push_elevations_to_rhino(guid_z_map, push_btn)
        
    json_payload = json.dumps(masterplan_dict, indent=2)
    exec_time = (time.perf_counter() - exec_start) * 1000
    
    ui_msg = f"BIM_JSON\nTime: {exec_time:.1f} ms\n---\nBldgs: {len(bldg_data_map)} | Blocks: {total_blocks}"
    if moved_count > 0: ui_msg += f"\nMoved to Z: {moved_count}"
    update_ui(ui_msg)
    
    return json_payload

# Assign Outputs Safely
JSON_Payload = process_bim_attributes()