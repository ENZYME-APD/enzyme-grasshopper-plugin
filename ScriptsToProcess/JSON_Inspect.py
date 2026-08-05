"""
JSON METADATA INSPECTOR
================================================================================
A generic utility node that acts as an "X-Ray" for the JSON-Bus.
Reads any Masterplan Engine JSON payload and extracts all unique, filterable 
metadata tags. Used to inform the user exactly what options are available for 
downstream filtering.

INPUTS:
    MP_JSON (str) : Any JSON stream (Masses, Slabs, Roofs, Railings, Facades).

OUTPUTS:
    Buildings (str) : Unique Building IDs.
    Towers    (str) : Unique Tower IDs.
    Programs  (str) : Unique Program names.
    Types     (str) : Unique element types (e.g., Roof classifications).
    Levels    (int) : Unique floor indices, sorted numerically.
================================================================================
"""
import json
import time

# --- UI WRAPPER ---
def update_ui(msg):
    _ghenv = globals().get('ghenv')
    _comp = globals().get('component')
    if _ghenv: _ghenv.Component.Message = msg
    elif _comp: _comp.Message = msg
    else: print(msg)

_ghenv = globals().get('ghenv')
if _ghenv:
    _ghenv.Component.Name = "JSON Metadata Inspector"
    _ghenv.Component.NickName = "JSON_Inspect"
    _ghenv.Component.Description = "Extracts all unique filter keys from any MP Engine JSON."

def inspect_json():
    exec_start = time.perf_counter()
    json_in = globals().get('MP_JSON', "")
    
    # Use sets to automatically guarantee uniqueness
    bldgs = set()
    towers = set()
    progs = set()
    types = set()
    levels = set()
    
    if not json_in:
        update_ui("INSPECTOR\nTime: 0.0 ms\n---\nAwaiting Data")
        return [], [], [], [], []
        
    try:
        data = json.loads(json_in)
        
        # The top-level dictionary keys are always Building IDs
        for bldg_name, content in data.items():
            bldgs.add(str(bldg_name))
            
            # ROUTE A: Dictionary-based hierarchy (Facade JSON)
            if isinstance(content, dict):
                for prog_name, floors in content.items():
                    progs.add(str(prog_name))
                    if isinstance(floors, list):
                        for item in floors:
                            if isinstance(item, dict):
                                if "tower_id" in item: towers.add(str(item["tower_id"]))
                                if "floor_index" in item: levels.add(int(item["floor_index"]))
                                if "type" in item: types.add(str(item["type"]))
                                
            # ROUTE B: List-based hierarchy (Masses, Slabs, Roofs, Railings)
            elif isinstance(content, list):
                for item in content:
                    if isinstance(item, dict):
                        if "tower_id" in item: towers.add(str(item["tower_id"]))
                        if "program" in item: progs.add(str(item["program"]))
                        if "type" in item: types.add(str(item["type"]))
                        if "floor_index" in item: levels.add(int(item["floor_index"]))
                        
                        # Special catch for Roof JSON which uses a list of programs
                        if "programs_above" in item:
                            for p in item["programs_above"]: 
                                progs.add(str(p))
                                
        # Sort alphabetical text
        out_bldgs = sorted(list(bldgs))
        out_towers = sorted(list(towers))
        out_progs = sorted(list(progs))
        out_types = sorted(list(types))
        
        # Sort numerical levels correctly (so 2 comes before 10)
        out_levels = sorted(list(levels))
        
        exec_time = (time.perf_counter() - exec_start) * 1000
        
        # Calculate total unique tags found
        total_tags = len(out_bldgs) + len(out_towers) + len(out_progs) + len(out_types) + len(out_levels)
        update_ui(f"INSPECTOR\nTime: {exec_time:.1f} ms\n---\nTags Found: {total_tags}")
        
        return out_bldgs, out_towers, out_progs, out_types, out_levels
        
    except Exception as e:
        update_ui(f"JSON Parse Error:\n{str(e)}")
        return [], [], [], [], []

# Execute
b_list, t_list, p_list, type_list, l_list = inspect_json()

# Outputs
Buildings = b_list
Towers = t_list
Programs = p_list
Types = type_list
Levels = l_list