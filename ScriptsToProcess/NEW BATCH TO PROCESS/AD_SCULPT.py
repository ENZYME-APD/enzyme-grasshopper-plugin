"""
STAGE 1 ADAPTER: THE SCULPTOR (METHOD 1) - V2 (RATIONALIZED)
================================================================================
INPUTS (Ensure all are set to List Access except RecipeJSON/RepeatLast):
    Massing (Brep List)      - Set Type Hint to 'Brep'
    TowerIDs (Str List)      - Set Type Hint to 'str'
    BuildingNames (Str List) - Set Type Hint to 'str'
    RecipeJSON (Str)         - Item Access
    RepeatLast (Bool)        - Item Access
================================================================================
"""
import Rhino.Geometry as rg
import json

ghenv.Component.Name = "Adapter: The Sculptor"
ghenv.Component.NickName = "AD_SCULPT"

# ==============================================================================
# 1. HELPER: CURVE SERIALIZATION
# ==============================================================================
def serialize_exact_curve(crv):
    segments_data = []
    segments = crv.DuplicateSegments()
    if not segments: segments = [crv]
    for seg in segments:
        if seg.IsLinear(0.001):
            segments_data.append({"type": "Line", "start": [round(seg.PointAtStart.X, 4), round(seg.PointAtStart.Y, 4), round(seg.PointAtStart.Z, 4)], "end": [round(seg.PointAtEnd.X, 4), round(seg.PointAtEnd.Y, 4), round(seg.PointAtEnd.Z, 4)]})
        elif seg.IsArc(0.001):
            rc, arc = seg.TryGetArc()
            if rc: segments_data.append({"type": "Arc", "start": [round(arc.StartPoint.X, 4), round(arc.StartPoint.Y, 4), round(arc.StartPoint.Z, 4)], "mid": [round(arc.MidPoint.X, 4), round(arc.MidPoint.Y, 4), round(arc.MidPoint.Z, 4)], "end": [round(arc.EndPoint.X, 4), round(arc.EndPoint.Y, 4), round(arc.EndPoint.Z, 4)]})
        else:
            poly_crv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0)
            if poly_crv and (rc := poly_crv.TryGetPolyline())[0]:
                segments_data.append({"type": "Polyline", "points": [[round(pt.X, 4), round(pt.Y, 4), round(pt.Z, 4)] for pt in rc[1]]})
    return segments_data

# ==============================================================================
# 2. PARSE RECIPE
# ==============================================================================
raw_recipe = []
try:
    if RecipeJSON:
        parsed = json.loads(RecipeJSON)
        for r in parsed:
            for _ in range(r.get("floors", 1)):
                raw_recipe.append({
                    "program": r.get("program", "Office"), 
                    "height": r.get("height", 4.0)
                })
except Exception as e:
    pass

if not raw_recipe:
    raw_recipe = [{"program": "Mixed_Use", "height": 4.0}]

# ==============================================================================
# 3. INITIALIZE BUILDINGS & FIND BASE ELEVATIONS
# ==============================================================================
buildings = {}

if Massing is not None and isinstance(Massing, list):
    for i, brep in enumerate(Massing):
        if not brep or not isinstance(brep, rg.Brep): continue
        
        tid = TowerIDs[i] if TowerIDs and i < len(TowerIDs) else "Main_Mass"
        bname = BuildingNames[i] if BuildingNames and i < len(BuildingNames) else "Building_01"
        
        bbox = brep.GetBoundingBox(True)
        if bname not in buildings:
            buildings[bname] = {"name": bname, "true_base_elevation": bbox.Min.Z, "max_z": bbox.Max.Z, "breps": [], "blocks": []}
        else:
            buildings[bname]["true_base_elevation"] = min(buildings[bname]["true_base_elevation"], bbox.Min.Z)
            buildings[bname]["max_z"] = max(buildings[bname]["max_z"], bbox.Max.Z)
            
        buildings[bname]["breps"].append((brep, tid))

# ==============================================================================
# 4. GENERATE GLOBAL Z-RULER & SLICE
# ==============================================================================
total_sliced_floors = 0

for bname, bdata in buildings.items():
    true_base = bdata["true_base_elevation"]
    max_bldg_z = bdata["max_z"]
    
    z_grid = []
    curr_z = true_base
    f_idx = 0
    
    while curr_z + 0.1 < max_bldg_z:
        if f_idx < len(raw_recipe):
            prog = raw_recipe[f_idx]["program"]
            fh = raw_recipe[f_idx]["height"]
        else:
            if RepeatLast:
                prog = raw_recipe[-1]["program"]
                fh = raw_recipe[-1]["height"]
            else:
                break 
                
        z_grid.append({"true_z": curr_z, "prog": prog, "height": fh})
        curr_z += fh
        f_idx += 1
        
    for brep, tid in bdata["breps"]:
        bbox = brep.GetBoundingBox(True)
        
        for floor_data in z_grid:
            z_plane = floor_data["true_z"]
            
            if z_plane >= bbox.Min.Z - 0.05 and z_plane <= bbox.Max.Z - 0.05:
                slice_plane = rg.Plane(rg.Point3d(0, 0, z_plane + 0.01), rg.Vector3d.ZAxis)
                
                rc, intersections, pts = rg.Intersect.Intersection.BrepPlane(brep, slice_plane, 0.01)
                
                if rc and intersections:
                    for crv in intersections:
                        crv.Translate(rg.Vector3d(0, 0, -0.01))
                        
                        # --- THE RATIONALIZATION ENGINE ---
                        # Converts complex organic splines into buildable Arcs & Lines
                        # tolerance=0.05m (5cm accuracy), angle_tolerance=0.1 radians, min_length=0.1m
                        rationalized = crv.ToArcsAndLines(0.05, 0.1, 0.1, 1000.0)
                        if rationalized:
                            crv = rationalized
                        # ----------------------------------

                        relative_z = z_plane - true_base
                        
                        block_dict = {
                            "name": f"Floor_{round(relative_z)}_{tid}",
                            "tower_id": tid,
                            "program": floor_data["prog"],
                            "floor_height": floor_data["height"],
                            "floors": 1,
                            "base_z": round(relative_z, 3),
                            "boundary_segments": serialize_exact_curve(crv)
                        }
                        bdata["blocks"].append(block_dict)
                        total_sliced_floors += 1

# ==============================================================================
# 5. SERIALIZE TO MP ENGINE FORMAT
# ==============================================================================
output_buildings = []
for bname, bdata in buildings.items():
    output_buildings.append({
        "name": bname,
        "true_base_elevation": round(bdata["true_base_elevation"], 3),
        "blocks": bdata["blocks"]
    })

payload_dict = {"buildings": output_buildings}
JSON_Payload = json.dumps(payload_dict, indent=2)

ghenv.Component.Message = "SCULPTOR ADAPTER\n---\nBuildings: {}\nFloors Sliced: {}".format(len(output_buildings), total_sliced_floors)