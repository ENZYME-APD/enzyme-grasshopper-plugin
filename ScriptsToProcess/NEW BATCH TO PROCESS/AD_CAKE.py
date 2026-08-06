"""
STAGE 1 ADAPTER: THE SLICED CAKE (METHOD 2) - PATCHED
================================================================================
Audits pre-modeled 1-floor-high Breps. Extracts base contours and heights, 
heals small modeling gaps using a tolerance, and outputs the Universal 
Data Format for the MP Engine.

INPUTS:
    FloorBreps    (Brep List) - Individual floor volumes. (MUST BE FLATTENED)
    Programs      (Str List)  - Program assigned to each Brep.
    TowerIDs      (Str List)  - Tower tags for stacking logic.
    BuildingNames (Str List)  - Building grouping tags.
    SnapTolerance (Float)     - Distance to heal gaps (e.g., 0.15m).
================================================================================
"""
import Rhino.Geometry as rg
import json

ghenv.Component.Name = "Adapter: The Sliced Cake"
ghenv.Component.NickName = "AD_CAKE"

# ==============================================================================
# DIAGNOSTICS
# ==============================================================================
print("=== SLICED CAKE DIAGNOSTICS ===")
if FloorBreps is None:
    print("❌ ERROR: FloorBreps input is None.")
else:
    print("✅ FloorBreps Count: {}".format(len(FloorBreps)))
    
tol = SnapTolerance if SnapTolerance is not None else 0.15
print("✅ Snap Tolerance: {}m".format(tol))
print("===============================\n")

# ==============================================================================
# 1. HELPER: CURVE SERIALIZATION (Rationalized)
# ==============================================================================
def serialize_exact_curve(crv):
    segments_data = []
    rationalized = crv.ToArcsAndLines(0.05, 0.1, 0.1, 1000.0)
    if rationalized: crv = rationalized
        
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
# 2. EXTRACTION PHASE
# ==============================================================================
raw_floors = []

if FloorBreps:
    for i, brep in enumerate(FloorBreps):
        if not brep or not isinstance(brep, rg.Brep): continue
        
        prog = Programs[i] if Programs and i < len(Programs) else "Mixed_Use"
        tid = TowerIDs[i] if TowerIDs and i < len(TowerIDs) else "Main_Mass"
        bname = BuildingNames[i] if BuildingNames and i < len(BuildingNames) else "Building_01"
        
        bbox = brep.GetBoundingBox(True)
        min_z = bbox.Min.Z
        max_z = bbox.Max.Z
        
        slice_plane = rg.Plane(rg.Point3d(0, 0, min_z + 0.01), rg.Vector3d.ZAxis)
        rc, intersections, pts = rg.Intersect.Intersection.BrepPlane(brep, slice_plane, 0.01)
        
        if rc and intersections:
            # FIXED: Convert C# Array to Python list before sorting
            intersections = list(intersections)
            intersections.sort(key=lambda c: rg.AreaMassProperties.Compute(c).Area if c.IsClosed else 0, reverse=True)
            base_crv = intersections[0]
            base_crv.Translate(rg.Vector3d(0, 0, -0.01)) 
            
            raw_floors.append({
                "bname": bname,
                "tid": tid,
                "prog": prog,
                "min_z": min_z,
                "max_z": max_z,
                "crv": base_crv
            })

print("Extracted {} valid floors from Breps.".format(len(raw_floors)))

# ==============================================================================
# 3. SORTING & HEALING PHASE
# ==============================================================================
buildings = {}

for f in raw_floors:
    bname = f["bname"]
    tid = f["tid"]
    if bname not in buildings:
        buildings[bname] = {"true_base_elevation": float('inf'), "towers": {}}
    if tid not in buildings[bname]["towers"]:
        buildings[bname]["towers"][tid] = []
        
    buildings[bname]["towers"][tid].append(f)

healed_blocks = 0
total_heals_applied = 0

for bname, bdata in buildings.items():
    print("\n--- HEALING: {} ---".format(bname))
    
    bldg_blocks = []
    
    for tid, floors in bdata["towers"].items():
        floors.sort(key=lambda x: x["min_z"])
        current_top_z = None
        
        for i, f in enumerate(floors):
            healed_min_z = f["min_z"]
            
            if current_top_z is not None:
                gap = f["min_z"] - current_top_z
                
                if abs(gap) <= tol and abs(gap) > 0.001:
                    healed_min_z = current_top_z
                    total_heals_applied += 1
            
            healed_height = f["max_z"] - healed_min_z
            
            if healed_min_z < bdata["true_base_elevation"]:
                bdata["true_base_elevation"] = healed_min_z
                
            f["healed_min_z"] = healed_min_z
            f["healed_height"] = healed_height
            
            current_top_z = f["max_z"]
            
        print("  -> Tower [{}]: Processed {} floors.".format(tid, len(floors)))

print("\nApplied {} microscopic gap heals across all buildings.".format(total_heals_applied))

# ==============================================================================
# 4. JSON SERIALIZATION
# ==============================================================================
output_buildings = []

for bname, bdata in buildings.items():
    true_base = bdata["true_base_elevation"]
    bldg_blocks = []
    
    for tid, floors in bdata["towers"].items():
        for f in floors:
            relative_z = f["healed_min_z"] - true_base
            
            block_dict = {
                "name": f"Floor_{round(relative_z)}_{tid}",
                "tower_id": tid,
                "program": f["prog"],
                "floor_height": f["healed_height"],
                "floors": 1,
                "base_z": round(relative_z, 3),
                "boundary_segments": serialize_exact_curve(f["crv"])
            }
            bldg_blocks.append(block_dict)
            healed_blocks += 1
            
    output_buildings.append({
        "name": bname,
        "true_base_elevation": round(true_base, 3),
        "blocks": bldg_blocks
    })

payload_dict = {"buildings": output_buildings}
JSON_Payload = json.dumps(payload_dict, indent=2)

ghenv.Component.Message = "SLICED CAKE ADAPTER\n---\nBuildings: {}\nFloors: {}\nHeals Applied: {}".format(
    len(output_buildings), healed_blocks, total_heals_applied)