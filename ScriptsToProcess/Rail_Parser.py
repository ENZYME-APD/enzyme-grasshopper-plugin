"""
RAILING JSON PARSER
================================================================================
INPUTS:
    Railings_JSON   (str)  [Item Access]
    Filter_Building (str)  [List Access]
    Filter_Tower    (str)  [List Access]
    Filter_Level    (int)  [List Access]
    ExactMatch      (bool) [Item Access]

OUTPUTS:
    RailingCurves, Labels
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
    _ghenv.Component.Name = "Railing JSON Parser"
    _ghenv.Component.NickName = "Rail_Parser"

def deserialize_curve(segments_data):
    if not segments_data: return None
    crvs = []
    for seg in segments_data:
        stype = seg.get("type")
        if stype == "Line": crvs.append(rg.LineCurve(rg.Point3d(*seg["start"]), rg.Point3d(*seg["end"])))
        elif stype == "Arc": crvs.append(rg.ArcCurve(rg.Arc(rg.Point3d(*seg["start"]), rg.Point3d(*seg["mid"]), rg.Point3d(*seg["end"]))))
        elif stype == "Polyline": crvs.append(rg.PolylineCurve([rg.Point3d(*p) for p in seg["points"]]))
    if not crvs: return None
    if len(crvs) == 1: return crvs[0]
    joined = rg.Curve.JoinCurves(crvs, 0.01)
    if joined and len(joined) > 0:
        crv = joined[0]
        if not crv.IsClosed: crv.MakeClosed(0.01)
        return crv
    return None

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

def query_railings():
    exec_start = time.perf_counter()
    json_in = globals().get('Railings_JSON', "")
    exact_toggle = globals().get('ExactMatch')
    exact_toggle = bool(exact_toggle) if exact_toggle is not None else False
    
    raw_bldg = globals().get('Filter_Building')
    f_bldg = [b for b in raw_bldg if b] if raw_bldg else []
    
    raw_tower = globals().get('Filter_Tower')
    f_tower = [t for t in raw_tower if t] if raw_tower else []
    
    raw_level = globals().get('Filter_Level')
    f_level = []
    if raw_level:
        for lvl in raw_level:
            if lvl is not None:
                try: f_level.append(int(lvl))
                except: pass
            
    out_curves = gh.DataTree[System.Object]()
    out_labels = gh.DataTree[System.Object]()
    
    if not json_in:
        update_ui("RAILING PARSER\nTime: 0.0 ms\n---\nAwaiting Data")
        return out_curves, out_labels
        
    try:
        data = json.loads(json_in)
        bldg_index = 0
        match_count = 0
        for bldg_name, rails in data.items():
            if not is_match(bldg_name, f_bldg, exact_toggle): continue
            for rail_index, rail in enumerate(rails):
                lvl = rail.get("floor_index", -1)
                tower_id = rail.get("tower_id", "Unknown")
                if f_level and lvl not in f_level: continue
                if not is_match(tower_id, f_tower, exact_toggle): continue
                    
                path = GH_Path(bldg_index, rail_index)
                label = f"{bldg_name} | {tower_id} - Lvl {lvl} Railing"
                out_labels.Add(label, path)
                
                z_translation = rg.Transform.Translation(0, 0, rail.get("true_z", 0.0))
                for crv_data in rail.get("curves", []):
                    c = deserialize_curve(crv_data)
                    if c: 
                        c.Transform(z_translation)
                        out_curves.Add(c, path)
                match_count += 1
            bldg_index += 1
            
        exec_time = (time.perf_counter() - exec_start) * 1000
        search_mode = "Exact" if exact_toggle else "Flexible"
        update_ui(f"RAILING PARSER\nTime: {exec_time:.0f} ms\n---\nReturned: {match_count}\nMode: {search_mode}")
        return out_curves, out_labels
    except Exception as e:
        update_ui("JSON Parse Error:\n" + str(e))
        return out_curves, out_labels

crvs, lbls = query_railings()
RailingCurves = crvs
Labels = lbls