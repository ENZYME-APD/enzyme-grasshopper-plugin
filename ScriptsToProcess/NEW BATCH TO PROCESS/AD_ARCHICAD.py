"""
STAGE 1 ADAPTER: ARCHICAD SLICED CAKE (TAPIR WORKFLOW) - PATCHED
"""
import Rhino.Geometry as rg
import json

ghenv.Component.Name = "Adapter: Archicad Slabs (Tapir)"
ghenv.Component.NickName = "AD_ARCHICAD"

print("=== ARCHICAD ADAPTER DIAGNOSTICS ===")
if FloorContours is None or FloorHeights is None:
    print("❌ ERROR: FloorContours or FloorHeights input is missing.")
else:
    print("✅ Floor Contours Count: {}".format(len(FloorContours)))
    print("✅ Floor Heights Count: {}".format(len(FloorHeights)))
tol = SnapTolerance if SnapTolerance is not None else 0.15
print("====================================\n")

def serialize_exact_curve(crv):
    # NEW: Polyline preservation logic!
    rc, poly = crv.TryGetPolyline()
    if rc and poly.Count > 0:
        return [{"type": "Polyline", "points": [[round(pt.X, 4), round(pt.Y, 4), round(pt.Z, 4)] for pt in poly]}]
        
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
            if poly_crv and (rc2 := poly_crv.TryGetPolyline())[0]:
                segments_data.append({"type": "Polyline", "points": [[round(pt.X, 4), round(pt.Y, 4), round(pt.Z, 4)] for pt in rc2[1]]})
    return segments_data

raw_floors = []
if FloorContours and FloorHeights:
    limit = min(len(FloorContours), len(FloorHeights))
    for i in range(limit):
        crv = FloorContours[i]
        if not crv: continue
        try: h = float(str(FloorHeights[i]))
        except: h = 4.0 
            
        prog = str(Programs[i]) if Programs and i < len(Programs) else "Mixed_Use"
        tid = str(TowerIDs[i]) if TowerIDs and i < len(TowerIDs) else "Main_Mass"
        bname = str(BuildingNames[i]) if BuildingNames and i < len(BuildingNames) else "Building_01"
        
        bbox = crv.GetBoundingBox(True)
        min_z = bbox.Min.Z
        max_z = min_z + h
        
        raw_floors.append({
            "bname": bname, "tid": tid, "prog": prog,
            "min_z": min_z, "max_z": max_z, "crv": crv.DuplicateCurve()
        })

buildings = {}
for f in raw_floors:
    bname = f["bname"]
    tid = f["tid"]
    if bname not in buildings: buildings[bname] = {"true_base_elevation": float('inf'), "towers": {}}
    if tid not in buildings[bname]["towers"]: buildings[bname]["towers"][tid] = []
    buildings[bname]["towers"][tid].append(f)

healed_blocks, total_heals_applied = 0, 0
for bname, bdata in buildings.items():
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
            if healed_min_z < bdata["true_base_elevation"]: bdata["true_base_elevation"] = healed_min_z
            f["healed_min_z"] = healed_min_z
            f["healed_height"] = healed_height
            current_top_z = f["max_z"]

output_buildings = []
for bname, bdata in buildings.items():
    true_base = bdata["true_base_elevation"]
    bldg_blocks = []
    for tid, floors in bdata["towers"].items():
        for f in floors:
            relative_z = f["healed_min_z"] - true_base
            block_dict = {
                "name": f"Floor_{round(relative_z)}_{tid}",
                "tower_id": tid, "program": f["prog"], "floor_height": f["healed_height"],
                "floors": 1, "base_z": round(relative_z, 3),
                "boundary_segments": serialize_exact_curve(f["crv"])
            }
            bldg_blocks.append(block_dict)
            healed_blocks += 1
    output_buildings.append({"name": bname, "true_base_elevation": round(true_base, 3), "blocks": bldg_blocks})

JSON_Payload = json.dumps({"buildings": output_buildings}, indent=2)
ghenv.Component.Message = "ARCHICAD ADAPTER\n---\nBuildings: {}\nFloors: {}\nHeals Applied: {}".format(len(output_buildings), healed_blocks, total_heals_applied)