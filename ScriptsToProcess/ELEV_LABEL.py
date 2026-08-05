#! python 3
"""
Elevation Labeler Pro
Generates leader lines and text labels from a tree of points.
Defaults to Z-elevation unless a custom LabelText is provided.

Inputs:
    Points (DataTree): Tree of Rhino.Geometry.Point3d to label.
    LabelText (DataTree): Optional. Custom text to override elevation.
    Length (DataTree): Tree representing leader line length.
    Gap (DataTree): Tree representing gap between line and text.
    Style (DataTree): Tree containing the Rhino Text Style name.
    TextPlane (DataTree): Tree containing Plane ID (0=XY, 1=XZ, 2=YZ).
    Orientation (DataTree): Tree containing local rotation in degrees.
    Anchor (DataTree): Tree containing Anchor ID (1-9 NumPad layout).
    Bake (DataTree): Tree containing a Boolean button to bake geometry.
    BakeLayer (DataTree): Tree containing the target layer name.
    BakeName (DataTree): Tree containing the substitution identifier.
    
Outputs:
    LeaderLine (DataTree): Generated leader lines.
    Text (DataTree): Generated text entities.
    Instructions_Out (str): Contract constraints.
"""

import Rhino
import Rhino.Geometry as rg
import Grasshopper as gh
import math
import time
from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path

# --- 2. ASSISTANT-GENERATED COMPONENT METADATA ---
ghenv.Component.Name = "Elevation Labeler Pro"
ghenv.Component.NickName = "ELEV_LABEL"
ghenv.Component.Description = "Custom text/elevation labels with radial rotation and auto-sync."

# --- 1. MANDATORY INSTRUCTIONS_OUT PARAMETER ---
Instructions_Out = """
INTERFACE CONTRACT:
Inputs:
- Points      : Point3d (DataTree) -> Coordinates to label
- LabelText   : String (DataTree) -> Optional text override
- Length      : Float (DataTree) -> Leader line length
- Gap         : Float (DataTree) -> Gap between line and text
- Style       : String (DataTree) -> Rhino Text Style name
- TextPlane   : Integer (DataTree) -> 0=XY, 1=XZ, 2=YZ 
- Orientation : Float (DataTree) -> Rotation in degrees 
- Anchor      : Integer (DataTree) -> 1-9 Justification 
- Bake        : Boolean (DataTree) -> Bake toggle/button
- BakeLayer   : String (DataTree) -> Target bake layer
- BakeName    : String (DataTree) -> Substitution group ID

Outputs:
- LeaderLine  : LineCurve (DataTree)
- Text        : TextEntity (DataTree)
- Instructions_Out : String
"""

start_time = time.perf_counter()
doc = Rhino.RhinoDoc.ActiveDoc

# --- VALUE LIST AUTO-POPULATION LOGIC ---
def sync_dynamic_style_list():
    style_param = next((p for p in ghenv.Component.Params.Input if p.Name == "Style"), None)
    if not style_param: return
    doc_styles = [s.Name for s in doc.DimStyles]
    for source in style_param.Sources:
        if isinstance(source, gh.Kernel.Special.GH_ValueList):
            current_keys = [item.Name for item in source.ListItems]
            if current_keys != doc_styles:
                source.ListItems.Clear()
                for s_name in doc_styles:
                    source.ListItems.Add(gh.Kernel.Special.GH_ValueListItem(s_name, f'"{s_name}"'))
                source.ExpireSolution(True)

def sync_static_value_list(param_name, data_dict):
    param = next((p for p in ghenv.Component.Params.Input if p.Name == param_name), None)
    if not param: return
    target_keys = list(data_dict.keys())
    for source in param.Sources:
        if isinstance(source, gh.Kernel.Special.GH_ValueList):
            current_keys = [item.Name for item in source.ListItems]
            if current_keys != target_keys:
                source.ListItems.Clear()
                for k, v in data_dict.items():
                    source.ListItems.Add(gh.Kernel.Special.GH_ValueListItem(k, str(v)))
                source.ExpireSolution(True)

# Run UI syncs
sync_dynamic_style_list()
sync_static_value_list("TextPlane", {"XY Plane (Top)": 0, "XZ Plane (Front)": 1, "YZ Plane (Right)": 2})
sync_static_value_list("Orientation", {
    "0°": 0, "45°": 45, "90°": 90, "135°": 135, 
    "180°": 180, "225°": 225, "270°": 270, "315°": 315
})
sync_static_value_list("Anchor", {
    "Top Left": 7, "Top Center": 8, "Top Right": 9,
    "Middle Left": 4, "Middle Center": 5, "Middle Right": 6,
    "Bottom Left": 1, "Bottom Center": 2, "Bottom Right": 3
})

# --- HELPER: Tree Parameter Extraction ---
def get_item(tree, default_val):
    if tree and tree.BranchCount > 0 and tree.Branch(0).Count > 0:
        return tree.Branch(0)[0]
    return default_val

# Parameter defaults and assignments
length_val = float(get_item(Length, 10.0))
gap_val = float(get_item(Gap, 2.0))
style_val = str(get_item(Style, "Default"))
plane_val = int(get_item(TextPlane, 1))    
orient_val = float(get_item(Orientation, 0.0)) 
anchor_val = int(get_item(Anchor, 2))      
bake_val = bool(get_item(Bake, False))
layer_val = str(get_item(BakeLayer, "Elevations"))
bake_name_val = str(get_item(BakeName, ""))

# Justification Mapping (NumPad Layout)
justifications = {
    1: rg.TextJustification.BottomLeft,
    2: rg.TextJustification.BottomCenter,
    3: rg.TextJustification.BottomRight,
    4: rg.TextJustification.MiddleLeft,
    5: rg.TextJustification.MiddleCenter,
    6: rg.TextJustification.MiddleRight,
    7: rg.TextJustification.TopLeft,
    8: rg.TextJustification.TopCenter,
    9: rg.TextJustification.TopRight
}

# Initialize strictly-typed output trees
LeaderLine = DataTree[object]()
Text = DataTree[object]()

total_items = 0
bake_count = 0

# --- LAYER & SUBSTITUTION BAKE SETUP ---
layer_idx = -1
if bake_val:
    layer_idx = doc.Layers.FindByFullPath(layer_val, -1)
    if layer_idx < 0:
        new_layer = Rhino.DocObjects.Layer()
        new_layer.Name = layer_val
        layer_idx = doc.Layers.Add(new_layer)
        
    if bake_name_val:
        existing_objs = doc.Objects.FindByUserString("BakeName", bake_name_val, True)
        if existing_objs:
            for obj in existing_objs:
                doc.Objects.Delete(obj, True)

dim_style = doc.DimStyles.FindName(style_val)

# --- 7. DATA TREE ARCHITECTURE & GEOMETRY GENERATION ---
for i in range(Points.BranchCount):
    path = Points.Path(i)
    branch = Points.Branch(path)
    
    # We will try to get a matching custom text branch if one exists
    text_branch = None
    if 'LabelText' in globals() and LabelText.BranchCount > 0:
        if i < LabelText.BranchCount:
            text_branch = LabelText.Branch(i)
        else:
            text_branch = LabelText.Branch(LabelText.BranchCount - 1)
    
    LeaderLine.EnsurePath(path)
    Text.EnsurePath(path)
    
    if branch.Count == 0:
        continue
        
    for j, pt in enumerate(branch):
        if pt is None:
            continue
            
        # Line generation
        p1 = pt
        p2 = rg.Point3d(pt.X, pt.Y, pt.Z + length_val)
        l_crv = rg.LineCurve(p1, p2)
        
        # Text generation
        p3 = rg.Point3d(pt.X, pt.Y, pt.Z + length_val + gap_val)
        te = rg.TextEntity()
        
        # Base Plane Assignment
        if plane_val == 0:
            plane = rg.Plane.WorldXY
        elif plane_val == 2:
            plane = rg.Plane.WorldYZ
        else:
            plane = rg.Plane.WorldZX
            
        plane.Origin = p3
        
        # Local Plane Rotation
        if orient_val != 0.0:
            plane.Rotate(math.radians(orient_val), plane.ZAxis, plane.Origin)
            
        te.Plane = plane
        
        # --- TEXT ASSIGNMENT LOGIC ---
        custom_text = None
        if text_branch and j < text_branch.Count:
            custom_text = str(text_branch[j])
        elif text_branch and text_branch.Count > 0:
            custom_text = str(text_branch[text_branch.Count - 1])
            
        if custom_text:
            te.Text = custom_text
        else:
            te.Text = f"{pt.Z:.2f}"
            
        te.Justification = justifications.get(anchor_val, rg.TextJustification.BottomCenter)
        
        if dim_style:
            te.DimensionStyleId = dim_style.Id
        else:
            te.TextHeight = 2.0 
            
        LeaderLine.Add(l_crv, path)
        Text.Add(te, path)
        total_items += 1
        
        # Immediate bake operation utilizing current attribute states
        if bake_val and layer_idx >= 0:
            attr = Rhino.DocObjects.ObjectAttributes()
            attr.LayerIndex = layer_idx
            
            if bake_name_val:
                attr.SetUserString("BakeName", bake_name_val)
                
            doc.Objects.AddCurve(l_crv, attr)
            doc.Objects.AddText(te, attr)
            bake_count += 2

# --- 10. TIME PROFILER & ALWAYS-ON CANVAS HUD MESSAGES ---
end_time = time.perf_counter()
elapsed = (end_time - start_time) * 1000.0

msg = (
    f"{ghenv.Component.NickName}\n"
    f"Time: {elapsed:.2f} ms\n"
    f"---\n"
    f"Branches: {Points.BranchCount}\n"
    f"Total Items: {total_items}\n"
    f"● Baked Geo: {bake_count}"
)
ghenv.Component.Message = msg