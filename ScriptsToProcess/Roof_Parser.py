"""
ROOF JSON PARSER
================================================================================
INPUTS:
    Roof_JSON       (str)  [Item Access]
    Filter_Building (str)  [List Access]
    Filter_Tower    (str)  [List Access]
    Filter_Type     (str)  [List Access]
    Filter_Program  (str)  [List Access]
    Filter_Level    (int)  [List Access]
    ExactMatch      (bool) [Item Access]

OUTPUTS:
    SlabBounds, TowerBounds, RoofAreas, TrueZ, Labels
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
    _ghenv.Component.Name = "Roof JSON Parser"
    _ghenv.Component.NickName = "Roof_Parser"

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

def query_roofs():
    exec_start = time.perf_counter()
    json_in = globals().get('Roof_JSON', "")
    exact_toggle = globals().get('ExactMatch')
    exact_toggle = bool(exact_toggle) if exact_toggle is not None else False
    
    raw_bldg = globals().get('Filter_Building')
    f_bldg = [b for b in raw_bldg if b] if raw_bldg else []
    
    raw_tower = globals().get('Filter_Tower')
    f_tower = [t for t in raw_tower if t] if raw_tower else []
    
    raw_type = globals().get('Filter_Type')
    f_type = [t for t in raw_type if t] if raw_type else []
    
    raw_prog = globals().get('Filter_Program')
    f_prog = [p for p in raw_prog if p] if raw_prog else []
    
    raw_level = globals().get('Filter_Level')
    f_level = []
    if raw_level:
        for lvl in raw_level:
            if lvl is not None:
                try: f_level.append(int(lvl))
                except: pass
            
    out_slabs = gh.DataTree[System.Object]()
    out_towers = gh.DataTree[System.Object]()
    out_z = gh.DataTree[System.Object]()
    out_areas = gh.DataTree[System.Object]()
    out_labels = gh.DataTree[System.Object]()
    
    if not json_in:
        update_ui("ROOF PARSER\nTime: 0.0 ms\n---\nAwaiting Data")
        return out_slabs, out_towers, out_z, out_areas, out_labels
        
    try:
        data = json.loads(json_in)
        bldg_index = 0
        match_count = 0
        for bldg_name, roofs in data.items():
            if not is_match(bldg_name, f_bldg, exact_toggle): continue
            for roof_index, roof in enumerate(roofs):
                tower_id = roof.get("tower_id", "Unknown")
                if f_level and roof.get("floor_index", -1) not in f_level: continue
                if not is_match(roof.get("type", ""), f_type, exact_toggle): continue
                if not is_match(tower_id, f_tower, exact_toggle): continue
                if f_prog:
                    prog_match = False
                    for p in roof.get("programs_above", []):
                        if is_match(p, f_prog, exact_toggle):
                            prog_match = True
                            break
                    if not prog_match: continue
                        
                path = GH_Path(bldg_index, roof_index)
                out_slabs.EnsurePath(path)
                out_towers.EnsurePath(path)
                
                label = f"{bldg_name} | {tower_id} | {roof.get('type', 'Roof')} - Lvl {roof.get('floor_index', '?')}"
                out_z.Add(roof.get("true_z", 0.0), path)
                out_areas.Add(roof.get("roof_area", 0.0), path)
                out_labels.Add(label, path)
                
                z_translation = rg.Transform.Translation(0, 0, roof.get("true_z", 0.0))
                for crv_data in roof.get("slab_bounds", []):
                    c = deserialize_curve(crv_data)
                    if c: 
                        c.Transform(z_translation)
                        out_slabs.Add(c, path)
                for crv_data in roof.get("tower_bounds", []):
                    c = deserialize_curve(crv_data)
                    if c: 
                        c.Transform(z_translation)
                        out_towers.Add(c, path)
                match_count += 1
            bldg_index += 1
            
        exec_time = (time.perf_counter() - exec_start) * 1000
        search_mode = "Exact" if exact_toggle else "Flexible"
        update_ui(f"ROOF PARSER\nTime: {exec_time:.0f} ms\n---\nReturned: {match_count}\nMode: {search_mode}")
        return out_slabs, out_towers, out_z, out_areas, out_labels
    except Exception as e:
        update_ui("JSON Parse Error:\n" + str(e))
        return out_slabs, out_towers, out_z, out_areas, out_labels

bnds, twrs, z_vals, areas, lbls = query_roofs()
SlabBounds = bnds
TowerBounds = twrs
TrueZ = z_vals
RoofAreas = areas
Labels = lbls