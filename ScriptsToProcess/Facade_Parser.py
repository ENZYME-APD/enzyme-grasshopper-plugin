"""
FACADE JSON PARSER (V2.1 - DUAL LAYER EDITION)
================================================================================
INPUTS:
    Facade_JSON     (str)  [Item Access]
    Filter_Building (str)  [List Access]
    Filter_Tower    (str)  [List Access]
    Filter_Program  (str)  [List Access]
    Filter_Level    (int)  [List Access]
    ExactMatch      (bool) [Item Access]

OUTPUTS:
    BoundsExt       (Curve) [Open Exterior Physical Lines]
    BoundsClosed    (Curve) [Closed Master Polygon for orientation]
    Heights         (float) [Floor-to-floor height]
    Programs        (str)   [Tags/Labels]
================================================================================
"""
import json
import Rhino.Geometry as rg
import Grasshopper as gh
from Grasshopper.Kernel.Data import GH_Path
import System
import fnmatch
import time

def update_ui(msg):
    _ghenv = globals().get('ghenv')
    _comp = globals().get('component')
    if _ghenv: _ghenv.Component.Message = msg
    elif _comp: _comp.Message = msg

_ghenv = globals().get('ghenv')
if _ghenv:
    _ghenv.Component.Name = "Facade JSON Parser"
    _ghenv.Component.NickName = "Facade_Parser"

# NEW: Deserializes without forcing the curves closed!
def deserialize_open_curves(segments_data):
    if not segments_data: return []
    crvs = []
    for seg in segments_data:
        stype = seg.get("type")
        if stype == "Line": crvs.append(rg.LineCurve(rg.Point3d(*seg["start"]), rg.Point3d(*seg["end"])))
        elif stype == "Arc": crvs.append(rg.ArcCurve(rg.Arc(rg.Point3d(*seg["start"]), rg.Point3d(*seg["mid"]), rg.Point3d(*seg["end"]))))
        elif stype == "Polyline": crvs.append(rg.PolylineCurve([rg.Point3d(*p) for p in seg["points"]]))
    if not crvs: return []
    if len(crvs) == 1: return [crvs[0]]
    
    joined = rg.Curve.JoinCurves(crvs, 0.01)
    return list(joined) if joined else crvs

def is_match(target_name, filter_list, exact_mode):
    if not filter_list: return True 
    target = str(target_name).strip().upper()
    for f in filter_list:
        pattern = str(f).strip().upper()
        if exact_mode:
            if target == pattern: return True
        else:
            if '*' in pattern or '?' in pattern:
                if fnmatch.fnmatch(target, pattern): return True
            elif pattern in target:
                return True
    return False

def query_facades():
    exec_start = time.perf_counter()
    json_in = globals().get('Facade_JSON', "")
    exact_toggle = globals().get('ExactMatch')
    exact_toggle = bool(exact_toggle) if exact_toggle is not None else False
    
    raw_bldg = globals().get('Filter_Building')
    f_bldg = [b for b in raw_bldg if b] if raw_bldg else []
    
    raw_tower = globals().get('Filter_Tower')
    f_tower = [t for t in raw_tower if t] if raw_tower else []
    
    raw_prog = globals().get('Filter_Program')
    f_prog = [p for p in raw_prog if p] if raw_prog else []
    
    raw_level = globals().get('Filter_Level')
    f_level = []
    if raw_level:
        for lvl in raw_level:
            if lvl is not None:
                try: f_level.append(int(lvl))
                except: pass
    
    # Initialize output DataTrees
    out_bounds_ext = gh.DataTree[System.Object]()
    out_bounds_closed = gh.DataTree[System.Object]()
    out_heights = gh.DataTree[System.Object]()
    out_programs = gh.DataTree[System.Object]()
    
    if not json_in:
        update_ui("FACADE PARSER\nTime: 0.0 ms\n---\nAwaiting Data")
        return out_bounds_ext, out_bounds_closed, out_heights, out_programs
        
    try:
        data = json.loads(json_in)
        bldg_index = 0
        match_count = 0
        
        for bldg_name, prog_dict in data.items():
            if not is_match(bldg_name, f_bldg, exact_toggle): continue
            prog_index = 0
            
            for prog_name, floors in prog_dict.items():
                if not is_match(prog_name, f_prog, exact_toggle): continue
                path = GH_Path(bldg_index, prog_index)
                
                for floor in floors:
                    tower_id = floor.get("tower_id", "Unknown")
                    if f_level and floor.get("floor_index", -1) not in f_level: continue
                    if not is_match(tower_id, f_tower, exact_toggle): continue
                    
                    # Extract True Z Elevation
                    z_offset = rg.Transform.Translation(0, 0, floor.get("true_z", 0.0))
                    
                    # 1. Evaluate Closed Master Polygon
                    closed_crvs = deserialize_open_curves(floor.get("BoundsClosed", []))
                    closed_crv = None
                    if closed_crvs:
                        closed_crv = closed_crvs[0]
                        if not closed_crv.IsClosed: closed_crv.MakeClosed(0.01)
                        closed_crv.Transform(z_offset)
                    
                    # 2. Evaluate Open Exterior Line Segments
                    ext_crvs = deserialize_open_curves(floor.get("BoundsExt", []))
                    
                    label = f"{bldg_name} | {tower_id} | {prog_name} - Lvl {floor.get('floor_index', '?')}"
                    
                    # 3. Parallel Assignment (matches 1 closed poly to multiple open lines)
                    for ext_crv in ext_crvs:
                        ext_crv.Transform(z_offset)
                        
                        out_bounds_ext.Add(ext_crv, path)
                        out_bounds_closed.Add(closed_crv, path)
                        out_heights.Add(floor["height"], path)
                        out_programs.Add(label, path)
                        match_count += 1
                        
                prog_index += 1
            bldg_index += 1
            
        exec_time = (time.perf_counter() - exec_start) * 1000
        search_mode = "Exact" if exact_toggle else "Flexible"
        update_ui(f"FACADE PARSER\nTime: {exec_time:.0f} ms\n---\nReturned: {match_count}\nMode: {search_mode}")
        return out_bounds_ext, out_bounds_closed, out_heights, out_programs
        
    except Exception as e:
        update_ui("JSON Parse Error:\n" + str(e))
        return out_bounds_ext, out_bounds_closed, out_heights, out_programs

# Direct the function outputs to the Grasshopper component output ports
bnds_ext, bnds_closed, hts, progs = query_facades()

BoundsExt = bnds_ext
BoundsClosed = bnds_closed
Heights = hts
Programs = progs