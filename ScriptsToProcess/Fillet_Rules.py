"""
FILLET RULE CONFIGURATOR
================================================================================
A UI helper node that compiles standard Grasshopper lists into a strict JSON 
schema for parametric corner rounding. Prevents syntax errors from manual typing.

HOW TO USE NEGATIVE RULES (e.g., "Everything except Office"):
Because the Engine reads top-down, put your exception first, then a wildcard:
1. Type: Program | Match: Office | Radius: 0.0 (Exception)
2. Type: Program | Match: * | Radius: 3.0 (Everything Else)

INPUTS:
    DefaultRadius (float) : The base radius if no rules match.
    RuleTypes     (str)   : List of target types ('Tower', 'Program', 'Building').
    RuleMatches   (str)   : List of target names ('Main_Tower', 'Retail', '*').
    RuleRadii     (float) : List of radii to apply to the matched targets.
    ExactMatch    (bool)  [List Access] : True = Exact, False = Contains. Defaults to True.

OUTPUTS:
    Fillet_JSON   (str)   : The compiled JSON string to feed into BIM_JSON.
    Instructions  (str)   : Detailed usage instructions.
================================================================================
"""

import json

def update_ui(msg):
    _ghenv = globals().get('ghenv')
    _comp = globals().get('component')
    if _ghenv: _ghenv.Component.Message = msg
    elif _comp: _comp.Message = msg

_ghenv = globals().get('ghenv')
if _ghenv:
    _ghenv.Component.Name = "Fillet Rule Configurator"
    _ghenv.Component.NickName = "Fillet_Rules"
    _ghenv.Component.Description = "Compiles Grasshopper lists into a Fillet Rules JSON."

def build_config():
    # 1. Grab inputs
    default_rad = globals().get('DefaultRadius')
    try: default_rad = float(default_rad)
    except: default_rad = 0.0
        
    raw_types = globals().get('RuleTypes') or []
    raw_matches = globals().get('RuleMatches') or []
    raw_radii = globals().get('RuleRadii') or []
    
    # Grab ExactMatch as a List
    raw_exacts = globals().get('ExactMatch')
    if raw_exacts is None: raw_exacts = []
    
    # 2. Clean data
    types = [str(t).strip() for t in raw_types if t is not None]
    matches = [str(m).strip() for m in raw_matches if m is not None]
    radii = []
    for r in raw_radii:
        try: radii.append(float(r))
        except: radii.append(0.0)
            
    rules = []
    
    # 3. Zip lists together securely
    rule_count = min(len(types), len(matches), len(radii))
    
    for i in range(rule_count):
        # Safely get the exact match boolean for this index, defaulting to True
        is_exact = True
        if i < len(raw_exacts) and raw_exacts[i] is not None:
            is_exact = bool(raw_exacts[i])
            
        rules.append({
            "type": types[i],
            "match": matches[i],
            "radius": radii[i],
            "exact": is_exact
        })
        
    # 4. Construct JSON dictionary
    config_dict = {
        "default_radius": default_rad,
        "rules": rules
    }
    
    update_ui(f"RULES BUILT\n---\nValid Rules: {rule_count}\nDefault: {default_rad}m")
    
    # 5. Output formatted JSON string
    return json.dumps(config_dict, indent=2)

# Outputs
Fillet_JSON = build_config()
Instructions = __doc__