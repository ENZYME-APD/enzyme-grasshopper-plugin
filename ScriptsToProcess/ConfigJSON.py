"""
MASTER JSON CONFIG BUILDER (TARGETS & PALETTE)
================================================================================
Combines Program names, Targets, and Colors into two separate JSON payloads
for the OOP Engine and the Area Dashboard.

INPUTS:
    Programs (str)   [List Access] : Names of the architectural programs.
    Targets  (float) [List Access] : Target area numbers (0 will be ignored).
    Colors   (Color) [List Access] : System.Drawing.Color from GH Swatches.

OUTPUTS:
    JSON_Targets (str) : Connect to the Dashboard's TargetJSON input.
    JSON_Palette (str) : Connect to the OOP Engine's ColorPalette input.
================================================================================
"""

import json
import System.Drawing as sd
import Grasshopper as gh

# UI Metadata
ghenv.Component.Name = "Master JSON Config Builder"
ghenv.Component.NickName = "ConfigJSON"
ghenv.Component.Description = "Generates Target and Palette JSONs from 3 parallel lists."

def build_configs():
    progs = globals().get('Programs', [])
    targets = globals().get('Targets', [])
    colors = globals().get('Colors', [])
    
    if not progs:
        return "{\n}", "{\n}", "Awaiting Programs Data"
        
    target_dict = {}
    palette_dict = {}
    
    # Iterate based on the length of the Programs list
    for i in range(len(progs)):
        p_name = str(progs[i]).strip()
        
        # 1. Safely map Targets (Ignoring 0 values or empty slots)
        if i < len(targets):
            try:
                t_val = float(targets[i])
                if t_val > 0: 
                    target_dict[p_name] = t_val
            except: pass
            
        # 2. Safely map Colors (Extracting RGB channels)
        if i < len(colors):
            c = colors[i]
            if hasattr(c, 'R') and hasattr(c, 'G') and hasattr(c, 'B'):
                palette_dict[p_name] = [int(c.R), int(c.G), int(c.B)]
                
    # Format the dictionaries into clean JSON strings
    json_targets = json.dumps(target_dict, indent=2)
    json_palette = json.dumps(palette_dict, indent=2)
    
    # UI Feedback
    msg = "Mapped:\n{} Targets\n{} Colors".format(len(target_dict), len(palette_dict))
    
    # Warn the user if they forgot to plug in a wire or have mismatched lists
    if len(progs) != len(targets) or len(progs) != len(colors):
        msg += "\n(Warning: List lengths differ)"
        
    return json_targets, json_palette, msg

# Execute
j_targets, j_palette, ui_msg = build_configs()

# Outputs
JSON_Targets = j_targets
JSON_Palette = j_palette
ghenv.Component.Message = ui_msg