"""
BIM KEY INITIALIZER (RESTORED & CORE-FREE)
================================================================================
Injects the required Attribute User Text keys into referenced Rhino objects.
* FIXED: Restored Rhino API injection logic and GuidsOut pass-through.
* UPDATED: Core mechanics (Type, CoreHeight) completely removed.
* UPDATED: Added <null> data stream interceptors.

INPUTS:
    Guids (Guid) [List Access] : Referenced Rhino geometry IDs.
    Run   (bool) [Item Access] : Wire a Button here to execute the injection.

OUTPUTS:
    GuidsOut (Guid) : Pass-through for the Serializer.
================================================================================
"""

import Rhino
import System
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
    _ghenv.Component.Name = "BIM Key Initializer"
    _ghenv.Component.NickName = "INITKEYS"
    _ghenv.Component.Description = "Safely injects default BIM attributes into referenced Rhino curves."

# ==============================================================================
# MAIN EXECUTION
# ==============================================================================
def initialize_bim_keys():
    exec_start = time.perf_counter()
    
    guids_in = globals().get('Guids', [])
    run_btn = globals().get('Run', False)
    
    status_msg = []
    
    # Intercept nulls from empty branches or dead wires
    clean_guids = [g for g in guids_in if g is not None]
    
    if not clean_guids:
        status_msg = ["Error: No Curves Connected"]
        clean_guids = []
    elif not run_btn:
        status_msg = ["Ready.", "Press Button."]
    else:
        doc = Rhino.RhinoDoc.ActiveDoc
        objects_modified = 0
        invalid_objects = 0
        
        # The new stripped-down schema (No Core logic)
        default_schema = {
            "BuildingID": "Building_01",
            "TowerID": "Main_Tower", 
            "Program": "Residential",
            "Phase": "0",
            "Floors": "1",
            "FloorHeight": "4.0"
        }
        
        for item in clean_guids:
            if not isinstance(item, System.Guid):
                invalid_objects += 1
                continue
                
            try:
                obj = doc.Objects.FindId(item)
                if not obj:
                    invalid_objects += 1
                    continue
            except TypeError:
                invalid_objects += 1
                continue
                
            modified = False
            
            for key, default_val in default_schema.items():
                existing_val = obj.Attributes.GetUserString(key)
                if existing_val is None:
                    obj.Attributes.SetUserString(key, default_val)
                    modified = True
                    
            if modified:
                obj.CommitChanges()
                objects_modified += 1
                
        if invalid_objects > 0 and objects_modified == 0:
            status_msg = [f"Failed: {invalid_objects} unreferenced", "(Check Type Hint = Guid)"]
        elif invalid_objects > 0:
            status_msg = [f"Injected: {objects_modified}", f"Skipped: {invalid_objects} (Not ref'd)"]
        else:
            status_msg = [f"Success: {objects_modified} objects"]
            
    exec_time = (time.perf_counter() - exec_start) * 1000
    
    # Standardized UI layout
    msg_lines = [
        "INITKEYS",
        f"Time: {exec_time:.0f} ms",
        "---"
    ] + status_msg
    
    update_ui("\n".join(msg_lines))
    return clean_guids

# Outputs
GuidsOut = initialize_bim_keys()