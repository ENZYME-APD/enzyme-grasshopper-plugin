"""
MASSES JSON PARSER & BUILDER
================================================================================
INPUTS:
    Masses_JSON     (str)   [Item Access]
    Filter_Building (str)   [List Access]
    Filter_Tower    (str)   [List Access] : Limit by Tower ID (e.g., "Podium")
    Filter_Program  (str)   [List Access]
    ExactMatch      (bool)  [Item Access]
    Transparency    (float) [Item Access] : 0.0 (Solid) to 1.0 (Invisible)

OUTPUTS:
    Volumes, BaseBounds, Areas, Heights, Colors, Programs, Labels
================================================================================
"""
import json
import Rhino.Geometry as rg
import Grasshopper as gh
from Grasshopper.Kernel.Data import GH_Path
import System
import System.Drawing as sd
import fnmatch
import time

def update_ui(msg):
    _ghenv = globals().get('ghenv')
    _comp = globals().get('component')
    if _ghenv: _ghenv.Component.Message = msg
    elif _comp: _comp.Message = msg

_ghenv = globals().get('ghenv')
if _ghenv:
    _ghenv.Component.Name = "Masses JSON Parser"
    _ghenv.Component.NickName = "Mass_Parser"

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

def query_and_build_masses():
    exec_start = time.perf_counter()
    json_in = globals().get('Masses_JSON', "")
    exact_toggle = globals().get('ExactMatch')
    exact_toggle = bool(exact_toggle) if exact_toggle is not None else False
    
    raw_t = globals().get('Transparency')
    try: t_val = max(0.0, min(1.0, float(raw_t)))
    except: t_val = 0.0
    alpha_channel = int((1.0 - t_val) * 255)
    
    raw_bldg = globals().get('Filter_Building')
    f_bldg = [b for b in raw_bldg if b] if raw_bldg else []
    
    raw_tower = globals().get('Filter_Tower')
    f_tower = [t for t in raw_tower if t] if raw_tower else []
    
    raw_prog = globals().get('Filter_Program')
    f_prog = [p for p in raw_prog if p] if raw_prog else []
    
    out_volumes = gh.DataTree[System.Object]()
    out_bounds = gh.DataTree[System.Object]()
    out_areas = gh.DataTree[System.Object]()
    out_heights = gh.DataTree[System.Object]()
    out_colors = gh.DataTree[System.Object]()
    out_programs = gh.DataTree[System.Object]()
    out_labels = gh.DataTree[System.Object]()
    
    if not json_in:
        update_ui("MASS PARSER\nTime: 0.0 ms\n---\nAwaiting Data")
        return out_volumes, out_bounds, out_areas, out_heights, out_colors, out_programs, out_labels
        
    try:
        data = json.loads(json_in)
        bldg_index = 0
        match_count = 0
        for bldg_name, blocks in data.items():
            if not is_match(bldg_name, f_bldg, exact_toggle): continue
            for block_index, block in enumerate(blocks):
                prog_name = block.get("program", "Unknown")
                tower_id = block.get("tower_id", "Unknown")
                if not is_match(prog_name, f_prog, exact_toggle): continue
                if not is_match(tower_id, f_tower, exact_toggle): continue
                    
                path = GH_Path(bldg_index, block_index)
                label = f"{bldg_name} | {tower_id} | {prog_name}"
                rgb = block.get("color", [200, 200, 200])
                color = sd.Color.FromArgb(alpha_channel, rgb[0], rgb[1], rgb[2])
                height = block.get("total_height", 0.0)
                
                out_heights.Add(height, path)
                out_colors.Add(color, path)
                out_programs.Add(prog_name, path)
                out_labels.Add(label, path)
                
                c = deserialize_curve(block.get("boundary", []))
                if c: 
                    amp = rg.AreaMassProperties.Compute(c)
                    out_areas.Add(amp.Area if amp else 0.0, path)
                    c.Transform(rg.Transform.Translation(0, 0, block.get("true_z", 0.0)))
                    out_bounds.Add(c, path)
                    extrusion = rg.Extrusion.Create(c, height, True)
                    if extrusion: out_volumes.Add(extrusion.ToBrep(), path)
                match_count += 1
            bldg_index += 1
            
        exec_time = (time.perf_counter() - exec_start) * 1000
        search_mode = "Exact" if exact_toggle else "Flexible"
        update_ui(f"MASS PARSER\nTime: {exec_time:.0f} ms\n---\nVolumes: {match_count}\nMode: {search_mode}")
        return out_volumes, out_bounds, out_areas, out_heights, out_colors, out_programs, out_labels
    except Exception as e:
        update_ui("JSON Parse Error:\n" + str(e))
        return out_volumes, out_bounds, out_areas, out_heights, out_colors, out_programs, out_labels

vols, bnds, areas, hts, cols, progs, lbls = query_and_build_masses()
Volumes = vols
BaseBounds = bnds
Areas = areas
Heights = hts
Colors = cols
Programs = progs
Labels = lbls