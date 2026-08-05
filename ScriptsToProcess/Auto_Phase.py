"""
AUTO-PHASE ASSIGNER (V5 - STRICT SEMANTIC SEQUENCING)
================================================================================
Reads referenced Rhino curves and assigns independent Phase sequences.
* FIXED: Drops physical Z-proximity checks. Now strictly assigns the podium 
  first (0, 1, 2...), finds the highest podium phase, and starts ALL individual 
  tower stacks sequentially from that exact number.
* MAINTAINS: Persistent logging and UI timing.

INPUTS:
    Guids (Guid) [List Access] : Referenced Rhino geometry IDs.
    Run   (bool) [Item Access] : Wire a Button here to execute.

OUTPUTS:
    Log (str)
================================================================================
"""

import Rhino
import Grasshopper as gh
import System
import time
import scriptcontext as sc

ghenv.Component.Name = "Auto-Phase Assigner by Z"
ghenv.Component.NickName = "Auto_Phase"
ghenv.Component.Description = "Strictly sequences the podium first, then cascades that count to independent towers."

def assign_phases():
    exec_start = time.time()
    
    # Setup unique cache keys for this specific component instance
    uid = str(ghenv.Component.InstanceGuid)
    log_key = "autophase_log_" + uid
    msg_key = "autophase_msg_" + uid
    
    guids = globals().get('Guids')
    run = globals().get('Run')
    
    # Return cached data if the button is not actively being pressed
    if not run:
        cached_log = sc.sticky.get(log_key, ["Awaiting execution. Click the 'Run' button."])
        cached_msg = sc.sticky.get(msg_key, "{}\nAwaiting Run".format(ghenv.Component.NickName))
        return cached_log, cached_msg
        
    if not guids:
        msg = "{}\nNo Guids".format(ghenv.Component.NickName)
        return ["No Guids provided."], msg

    doc = Rhino.RhinoDoc.ActiveDoc
    buildings = {}
    log = []
    
    # 1. Gather and Group the Geometry by BuildingID
    for gid in guids:
        obj = doc.Objects.FindId(gid)
        if not obj: continue
            
        b_id = obj.Attributes.GetUserString("BuildingID") or "Building_01"
        obj_type = obj.Attributes.GetUserString("Type") or "Block"
        
        if obj_type.lower() == "core":
            continue
            
        bbox = obj.Geometry.GetBoundingBox(True)
        z_height = round(bbox.Min.Z, 3)
        t_id = obj.Attributes.GetUserString("TowerID") or "Main"
        
        if b_id not in buildings:
            buildings[b_id] = {'podium_blocks': [], 'tower_blocks': {}}
            
        item = {'obj': obj, 't_id': t_id, 'z': z_height}
        
        # Semantically sort Podium vs Towers
        if "podium" in t_id.lower():
            buildings[b_id]['podium_blocks'].append(item)
        else:
            if t_id not in buildings[b_id]['tower_blocks']:
                buildings[b_id]['tower_blocks'][t_id] = []
            buildings[b_id]['tower_blocks'][t_id].append(item)

    modify_count = 0
    
    # 2. Assign Phases Logically (Podium First, then Towers)
    for b_id, data in buildings.items():
        
        max_podium_phase = -1 # Defaults to -1 so if there is no podium, towers start at 0
        
        # A. Process the Podium First
        podiums = sorted(data['podium_blocks'], key=lambda x: x['z'])
        for i, p_item in enumerate(podiums):
            current_phase = i
            max_podium_phase = current_phase # Store the highest phase reached
            
            p_item['obj'].Attributes.SetUserString("Phase", str(current_phase))
            p_item['obj'].CommitChanges()
            modify_count += 1
            log.append("-> {} ({}): Assigned Phase {}".format(b_id, p_item['t_id'], current_phase))
            
        # B. Process Individual Towers (Cascading from max_podium_phase)
        for t_id, t_items in data['tower_blocks'].items():
            t_items_sorted = sorted(t_items, key=lambda x: x['z'])
            
            for i, t_item in enumerate(t_items_sorted):
                # Start from the podium's max phase + 1, and increment up
                new_phase = max_podium_phase + 1 + i
                new_phase_str = str(new_phase)
                
                t_item['obj'].Attributes.SetUserString("Phase", new_phase_str)
                t_item['obj'].CommitChanges()
                modify_count += 1
                log.append("-> {} (Tower: {}): Assigned Phase {}".format(b_id, t_id, new_phase_str))

    # 3. Format Output Status and UI Message
    if modify_count > 0:
        status_line = "Updated {} phases".format(modify_count)
        log.insert(0, "SUCCESS: " + status_line)
    else:
        status_line = "No valid blocks found"
        log.insert(0, "WARNING: Found no blocks to phase.")
        
    exec_time = (time.time() - exec_start) * 1000
    
    ui_msg = "{}\nTime: {:.0f} ms\n{}".format(
        ghenv.Component.NickName, 
        exec_time, 
        status_line
    )
    
    # Save to persistent memory
    sc.sticky[log_key] = log
    sc.sticky[msg_key] = ui_msg
        
    return log, ui_msg

# Execute
output_log, ui_message = assign_phases()

# Outputs
Log = output_log
ghenv.Component.Message = ui_message