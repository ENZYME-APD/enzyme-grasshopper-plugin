"""
JSON PALETTE BUILDER
================================================================================
Converts a list of program strings and visual Grasshopper Color Swatches 
into a formatted JSON dictionary.

INPUTS:
    Programs (str)   [List Access] : Names of the architectural programs.
    Colors   (Color) [List Access] : System.Drawing.Color objects from GH Swatches.

OUTPUTS:
    JSON_Palette (str) : Formatted JSON string ready for the OOP Engine.
================================================================================
"""

import json
import System.Drawing as sd
import Grasshopper as gh

# UI Metadata
ghenv.Component.Name = "JSON Palette Builder"
ghenv.Component.NickName = "MakePalette"
ghenv.Component.Description = "Zips strings and GH Colors into a JSON dictionary."

def build_palette():
    progs = globals().get('Programs', [])
    colors = globals().get('Colors', [])
    
    if not progs or not colors:
        return "{\n}", "Awaiting Data"
        
    palette_dict = {}
    
    # Safely zip the lists (stops at the shortest list to prevent crashes)
    limit = min(len(progs), len(colors))
    
    for i in range(limit):
        p_name = str(progs[i]).strip()
        c = colors[i]
        
        # Ensure we are extracting the RGB channels correctly
        if hasattr(c, 'R') and hasattr(c, 'G') and hasattr(c, 'B'):
            palette_dict[p_name] = [int(c.R), int(c.G), int(c.B)]
            
    # Format the dictionary into a clean JSON string
    json_str = json.dumps(palette_dict, indent=2)
    
    # UI Feedback
    msg = "Mapped {} Programs".format(limit)
    if len(progs) != len(colors):
        msg += "\nWarning: List length mismatch!"
        
    return json_str, msg

# Execute
json_out, ui_msg = build_palette()

# Outputs
JSON_Palette = json_out
ghenv.Component.Message = ui_msg