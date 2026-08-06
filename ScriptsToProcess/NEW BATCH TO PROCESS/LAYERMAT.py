"""
Grasshopper Python 3 Component — Assign Layer Materials
======================================================
DESCRIPTION:
  Assigns render materials to Rhino layers based on their display color.
  It safely persists data between execution cycles and features an auto-fill 
  routine: connect a native Value List to the LayerNames input, and it will 
  automatically populate with all existing Rhino layers.

INPUTS:
  LayerNames  : List [str] - Layer names or paths. Connect a Value List here!
  UseFullPath : Item [bool] - If True, material name = 'Parent::Child'. If False, just 'Child'.
  RunScript   : Item [bool] - Connect a Button/Toggle to execute.

OUTPUTS:
  Instructions_Out : [str] - Component interface documentation.
  LogOutput        : [str] - Persistent execution log.
  UpdatedCount     : [int] - Number of layers that received a new material.
  SkippedCount     : [int] - Number of layers that already had a material.
"""

import scriptcontext as sc
import Rhino
import System
import time
from Grasshopper import DataTree
from Grasshopper.Kernel.Special import GH_ValueList, GH_ValueListItem

# ─── COMPONENT METADATA ───────────────────────────────────────────────────────
ghenv.Component.Name = "Assign Layer Materials"
ghenv.Component.NickName = "LAYERMAT"
ghenv.Component.Description = "Assigns render materials to layers and auto-populates connected Value Lists."

Instructions_Out = __doc__

# ─── STATE PERSISTENCE SETUP ──────────────────────────────────────────────────
comp_id = str(ghenv.Component.InstanceGuid)
k_msg = comp_id + "_msg"
k_out = comp_id + "_out"
k_upd = comp_id + "_updated"
k_skp = comp_id + "_skipped"

if k_msg not in sc.sticky:
    sc.sticky[k_msg] = "LAYERMAT\nMode: --\n-- WAITING --\n---\n● Assigned: 0\n○ Skipped: 0"
    sc.sticky[k_out] = "Connect a boolean to RunScript."
    sc.sticky[k_upd] = 0
    sc.sticky[k_skp] = 0

# ─── HELPER FUNCTIONS ─────────────────────────────────────────────────────────
def auto_populate_value_lists(component, doc):
    """
    Scans the LayerNames input (Index 0) for connected Value Lists.
    Injects current Rhino layers into the list if out of sync.
    """
    if component.Params.Input.Count < 1: return
    layer_input = component.Params.Input[0]
    if not layer_input.Sources: return
    
    valid_layers = [L for L in doc.Layers if not L.IsDeleted]
    target_names = [L.Name for L in valid_layers]
    
    for source in layer_input.Sources:
        # Bypass Python 'is' identity checks by reading the .NET type directly
        if source.GetType().Name == "GH_ValueList":
            current_names = [item.Name for item in source.ListItems]
            
            if current_names != target_names:
                source.ListItems.Clear()
                for L in valid_layers:
                    # Wrap FullPath in quotes so GH parses it as a string
                    item = GH_ValueListItem(L.Name, f'"{L.FullPath}"')
                    source.ListItems.Add(item)
                
                source.Attributes.ExpireLayout()

def layer_uses_default_material(layer):
    return layer.RenderMaterialIndex < 0

def find_layer(doc, name):
    idx = doc.Layers.FindByFullPath(name, True)
    if idx >= 0: return doc.Layers[idx]
    for i in range(doc.Layers.Count):
        layer = doc.Layers[i]
        if not layer.IsDeleted and layer.Name == name: return layer
    return None

def get_or_create_material(layer, doc, use_full_path):
    mat_name = layer.FullPath if use_full_path else layer.Name
    existing = doc.Materials.Find(mat_name, True)
    if existing >= 0: return existing

    mat = Rhino.DocObjects.Material()
    mat.Name = mat_name
    layer_color = layer.Color
    mat.DiffuseColor = layer_color

    alpha = layer_color.A
    transparency = 1.0 - (alpha / 255.0)
    mat.Transparency = max(0.0, min(1.0, transparency))
    mat.SpecularColor = System.Drawing.Color.White
    mat.Shine = 30.0

    if transparency > 0.0:
        mat.IndexOfRefraction = 1.0
        mat.ReflectionColor = System.Drawing.Color.White

    mat.CommitChanges()
    return doc.Materials.Add(mat)

# ─── PRE-FLIGHT & VALUE LIST SYNC ─────────────────────────────────────────────
active_doc = Rhino.RhinoDoc.ActiveDoc
if active_doc:
    auto_populate_value_lists(ghenv.Component, active_doc)

# ─── MAIN EXECUTION BLOCK ─────────────────────────────────────────────────────
if RunScript:
    t_start = time.perf_counter()
    
    ghdoc_backup = sc.doc
    sc.doc = active_doc
    doc = sc.doc
    
    messages = ["=== Assign Layer Materials ==="]
    updated = 0
    skipped = 0
    
    try:
        # Default to True if the UseFullPath input is disconnected
        use_full = True if UseFullPath is None else UseFullPath
        mode_str = "Full Path" if use_full else "Short Name"
        
        names = [n for n in (LayerNames or []) if n and n.strip()]
        
        if names:
            messages.append(f"Target: {len(names)} layer name(s) provided")
            layers_to_process = []
            for name in names:
                layer = find_layer(doc, name.strip())
                if layer:
                    layers_to_process.append(layer)
                else:
                    messages.append(f"  WARN  Layer not found: '{name}'")
        else:
            layers_to_process = [doc.Layers[i] for i in range(doc.Layers.Count) if not doc.Layers[i].IsDeleted]
            messages.append(f"Target: Processing ALL {len(layers_to_process)} layer(s)")

        for layer in layers_to_process:
            if not layer_uses_default_material(layer):
                messages.append(f"  SKIP  {layer.FullPath} (Has Material)")
                skipped += 1
            else:
                mat_index = get_or_create_material(layer, doc, use_full)
                layer.RenderMaterialIndex = mat_index
                doc.Layers.Modify(layer, layer.LayerIndex, False)
                messages.append(f"  OK    {layer.FullPath} (Assigned)")
                updated += 1
                
        doc.Views.Redraw()
        
        messages.append("")
        messages.append(f"Done. Assigned: {updated} | Skipped: {skipped}")
        
        t_end = time.perf_counter()
        elapsed_ms = round((t_end - t_start) * 1000, 2)
        
        hud_msg = f"""LAYERMAT
Mode: {mode_str}
Time: {elapsed_ms} ms
---
● Assigned: {updated}
○ Skipped: {skipped}"""

        sc.sticky[k_msg] = hud_msg
        sc.sticky[k_out] = "\n".join(messages)
        sc.sticky[k_upd] = updated
        sc.sticky[k_skp] = skipped

    finally:
        sc.doc = ghdoc_backup

# ─── PUSH STATE TO CANVAS ─────────────────────────────────────────────────────
ghenv.Component.Message = sc.sticky[k_msg]
LogOutput = sc.sticky[k_out]
UpdatedCount = sc.sticky[k_upd]
SkippedCount = sc.sticky[k_skp]