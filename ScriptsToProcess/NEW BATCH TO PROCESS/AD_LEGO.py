"""
STAGE 1 ADAPTER: THE LEGO BUILDER (METHOD 3) - VERBOSE DIAGNOSTICS & FIXED STACKING
================================================================================
INPUTS (List Access - MUST BE FLATTENED):
    MassingBlocks  (Brep List)  
    Programs       (Str List)   
    TowerIDs       (Str List)   
    BuildingNames  (Str List)   
    FloorHeights   (Float List) 
INPUTS (Item Access):
    HeightResolution (Int)      - 0 = Strict Cutoff, 1 = Stretch Top Floor
================================================================================
"""
import Rhino.Geometry as rg
import json

ghenv.Component.Name = "Adapter: The Lego Builder"
ghenv.Component.NickName = "AD_LEGO"

print("=== LEGO BUILDER DIAGNOSTICS ===")
if MassingBlocks is None: print("❌ ERROR: MassingBlocks input is None.")
else: print("✅ Massing Blocks Count: {}".format(len(MassingBlocks)))
h_res = HeightResolution if HeightResolution is not None else 0
print("✅ Resolution Mode: {}".format("Stretch Top Floor (1)" if h_res == 1 else "Strict Cutoff (0)"))
print("--------------------------------")

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
# PASS 1: PRE-CALCULATE THE TRUE BASE ELEVATION FOR ALL BUILDINGS
# ==============================================================================
building_min_zs = {}
if MassingBlocks:
    for i, brep in enumerate(MassingBlocks):
        if not brep or not isinstance(brep, rg.Brep): continue
        bname = str(BuildingNames[i]) if BuildingNames and i < len(BuildingNames) else "Building_01"
        min_z = brep.GetBoundingBox(rg.Plane.WorldXY).Min.Z
        
        if bname not in building_min_zs:
            building_min_zs[bname] = min_z
        else:
            building_min_zs[bname] = min(building_min_zs[bname], min_z)

# ==============================================================================
# PASS 2: BUILD THE FLOORS
# ==============================================================================
buildings = {}
total_generated_floors = 0
stretched_floors_created = 0

if MassingBlocks:
    for i, brep in enumerate(MassingBlocks):
        if not brep or not isinstance(brep, rg.Brep): continue
        
        prog = str(Programs[i]) if Programs and i < len(Programs) else "Mixed_Use"
        tid = str(TowerIDs[i]) if TowerIDs and i < len(TowerIDs) else "Main_Mass"
        bname = str(BuildingNames[i]) if BuildingNames and i < len(BuildingNames) else "Building_01"
        
        try: fh = float(str(FloorHeights[i])) if FloorHeights and i < len(FloorHeights) else 4.0
        except: fh = 4.0
            
        bbox = brep.GetBoundingBox(rg.Plane.WorldXY)
        min_z = bbox.Min.Z
        max_z = bbox.Max.Z
        total_height = max_z - min_z
        
        slice_plane = rg.Plane(rg.Point3d(0, 0, min_z + 0.01), rg.Vector3d.ZAxis)
        rc, intersections, pts = rg.Intersect.Intersection.BrepPlane(brep, slice_plane, 0.01)
        
        if rc and intersections:
            intersections = list(intersections)
            intersections.sort(key=lambda c: rg.AreaMassProperties.Compute(c).Area if c.IsClosed else 0, reverse=True)
            base_crv = intersections[0]
            base_crv.Translate(rg.Vector3d(0, 0, -0.01))
            serialized_crv = serialize_exact_curve(base_crv)
            
            num_full_floors = int(total_height // fh)
            remainder = round(total_height % fh, 3)
            
            # THE DIAGNOSTIC PRINTOUT
            print("🧱 BLOCK {} [{}]:".format(i, tid))
            print("   -> Measured Z: {:.1f}m to {:.1f}m (Total: {:.1f}m)".format(min_z, max_z, total_height))
            print("   -> Floor Height: {:.1f}m".format(fh))
            print("   -> Computed: {} full floors (Remainder: {:.2f}m)".format(num_full_floors, remainder))
            
            if bname not in buildings:
                buildings[bname] = {"true_base_elevation": building_min_zs[bname], "blocks": []}
                
            # FIXED Z-MATH: Safe offset from the true building minimum
            relative_base_z = min_z - building_min_zs[bname]
            
            if num_full_floors > 0:
                if h_res == 1 and remainder > 0.1:
                    standard_floors = num_full_floors - 1
                    if standard_floors > 0:
                        buildings[bname]["blocks"].append({
                            "name": f"{prog}_{standard_floors}Fl_{tid}",
                            "tower_id": tid, "program": prog, "floor_height": fh,
                            "floors": standard_floors, "base_z": round(relative_base_z, 3),
                            "boundary_segments": serialized_crv
                        })
                        total_generated_floors += standard_floors
                        
                    top_base_z = relative_base_z + (standard_floors * fh)
                    stretched_height = fh + remainder
                    buildings[bname]["blocks"].append({
                        "name": f"{prog}_TopStretched_{tid}",
                        "tower_id": tid, "program": f"{prog} (Top)", "floor_height": round(stretched_height, 3),
                        "floors": 1, "base_z": round(top_base_z, 3), "boundary_segments": serialized_crv
                    })
                    total_generated_floors += 1
                    stretched_floors_created += 1

                else:
                    buildings[bname]["blocks"].append({
                        "name": f"{prog}_{num_full_floors}Fl_{tid}",
                        "tower_id": tid, "program": prog, "floor_height": fh,
                        "floors": num_full_floors, "base_z": round(relative_base_z, 3),
                        "boundary_segments": serialized_crv
                    })
                    total_generated_floors += num_full_floors

output_buildings = []
for bname, bdata in buildings.items():
    output_buildings.append({
        "name": bname,
        "true_base_elevation": round(bdata["true_base_elevation"], 3),
        "blocks": bdata["blocks"]
    })

payload_dict = {"buildings": output_buildings}
JSON_Payload = json.dumps(payload_dict, indent=2)

print("================================")
ghenv.Component.Message = "LEGO ADAPTER\n---\nTotal Floors: {}\nStretched Tops: {}".format(total_generated_floors, stretched_floors_created)