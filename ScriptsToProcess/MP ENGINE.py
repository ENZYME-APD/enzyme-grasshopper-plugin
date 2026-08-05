"""
OPP MASTERPLAN ENGINE (JSON-BUS EDITION)
================================================================================
A high-performance topological coordinator. Evaluates spatial intersections, 
identifies setbacks/roofs, and broadcasts lightweight JSON architectures.
* FIXED: Added dedicated Railings_JSON output stream.
* FIXED: Restored full Area Summary readouts to the Grasshopper UI.

INPUTS:
    JSON_Payload (str) - The serialized masterplan from BIM_JSON.
    ColorPalette (str) - Custom JSON color dictionary.

OUTPUTS:
    Masses_JSON, Slab_JSON, Roof_JSON, Facade_JSON, Railings_JSON, Dashboard_JSON
================================================================================
"""
import json
import Rhino.Geometry as rg
import System.Drawing as sd
import random
import time

# --- METADATA INITIALIZATION ---
ghenv.Component.Name = "JSON MP Engine"
ghenv.Component.NickName = "MP ENGINE"
ghenv.Component.Description = "Computes topological data and broadcasts JSON geometry streams."

# ==============================================================================
# ENGINE HELPER FUNCTIONS (Pure 2D Boolean & Serialization)
# ==============================================================================
def safe_boolean_op(curves_a, curves_b, op_type="diff", tolerance=0.001):
    if not curves_a: return []
    if not curves_b and op_type == "diff": return [c.Duplicate() for c in curves_a]
    if not curves_b and op_type == "int": return []
        
    result_curves = []
    if op_type == "diff":
        import System
        list_b = System.Collections.Generic.List[rg.Curve]()
        for c in curves_b: 
            if not c.IsClosed: c.MakeClosed(tolerance)
            list_b.Add(c)
            
        for crv_a in curves_a:
            if not crv_a.IsClosed: crv_a.MakeClosed(tolerance)
            res = rg.Curve.CreateBooleanDifference(crv_a, list_b, tolerance)
            if res: result_curves.extend(list(res))
            else:
                res2 = rg.Curve.CreateBooleanDifference(crv_a, list_b, 0.05)
                if res2: result_curves.extend(list(res2))
                else: result_curves.append(crv_a.Duplicate())
    else:
        for crv_a in curves_a:
            if not crv_a.IsClosed: crv_a.MakeClosed(tolerance)
            for crv_b in curves_b:
                if not crv_b.IsClosed: crv_b.MakeClosed(tolerance)
                res = rg.Curve.CreateBooleanIntersection(crv_a, crv_b, tolerance)
                if res: result_curves.extend(list(res))
                else:
                    res2 = rg.Curve.CreateBooleanIntersection(crv_a, crv_b, 0.05)
                    if res2: result_curves.extend(list(res2))
    return result_curves

def get_naked_railings(exposed_roof_crvs, tower_crvs, tolerance=0.05):
    if not tower_crvs: return [c.Duplicate() for c in exposed_roof_crvs]
    naked_segments = []
    for crv in exposed_roof_crvs:
        segments = crv.DuplicateSegments()
        if not segments: segments = [crv]
        for seg in segments:
            mid_pt = seg.PointAtNormalizedLength(0.5)
            is_touching_wall = False
            for t_crv in tower_crvs:
                rc, t = t_crv.ClosestPoint(mid_pt)
                if rc and t_crv.PointAt(t).DistanceTo(mid_pt) <= tolerance:
                    is_touching_wall = True
                    break
            if not is_touching_wall: naked_segments.append(seg)
    if naked_segments:
        joined = rg.Curve.JoinCurves(naked_segments, 0.01)
        return list(joined) if joined else naked_segments
    return []

def get_brep_area(crvs):
    if not crvs: return 0.0
    breps = rg.Brep.CreatePlanarBreps(crvs, 0.01)
    if not breps: return 0.0
    return sum([amp.Area for b in breps if (amp := rg.AreaMassProperties.Compute(b))])

def get_program_color(prog, custom_palette):
    clean_prog = prog.strip()
    if clean_prog in custom_palette: return custom_palette[clean_prog]
    random.seed(clean_prog)
    return [random.randint(70, 200), random.randint(70, 200), random.randint(70, 200)]

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

def deserialize_curve(segments_data):
    crvs = []
    for seg in segments_data:
        stype = seg.get("type")
        if stype == "Line": crvs.append(rg.LineCurve(rg.Point3d(*seg["start"]), rg.Point3d(*seg["end"])))
        elif stype == "Arc": crvs.append(rg.ArcCurve(rg.Arc(rg.Point3d(*seg["start"]), rg.Point3d(*seg["mid"]), rg.Point3d(*seg["end"]))))
        elif stype == "Polyline": crvs.append(rg.PolylineCurve([rg.Point3d(*p) for p in seg["points"]]))
    if not crvs: return None
    if len(crvs) == 1: return crvs[0]
    joined = rg.Curve.JoinCurves(crvs, 0.01)
    if joined:
        if not joined[0].IsClosed: joined[0].MakeClosed(0.01)
        return joined[0]
    return None

# ==============================================================================
# ENGINE CLASSES (Topological Logic)
# ==============================================================================
class MassingBlock:
    def __init__(self, data, bldg_elev, custom_palette):
        self.name = data.get("name", "Unknown")
        self.tower_id = data.get("tower_id", "Main_Tower")
        self.program = data.get("program", "Mixed Use")
        self.floor_height = data.get("floor_height", 4.0)
        self.floors = data.get("floors", 1)
        self.base_z = data.get("base_z", 0.0)
        self.bldg_elev = bldg_elev   
        self.base_curve = deserialize_curve(data["boundary_segments"])
        if self.base_curve:
            bbox = self.base_curve.GetBoundingBox(True)
            self.base_curve.Transform(rg.Transform.Translation(rg.Vector3d(0, 0, -bbox.Min.Z)))
            
        self.color = get_program_color(self.program, custom_palette) 
        self.areas, self.floor_indices = [], []
        
        # Populate area metadata early
        for _ in range(self.floors):
            amp = rg.AreaMassProperties.Compute(self.base_curve) if self.base_curve else None
            self.areas.append(amp.Area if amp else 0.0)
            self.floor_indices.append(0)

class Building:
    def __init__(self, data, custom_palette):
        self.name = data.get("name", "Building")
        # Extract the dynamically injected True Base Elevation
        self.elevation = data.get("true_base_elevation", 0.0)
        
        self.tower_groups = {}
        self.blocks = []
        
        for b_data in data.get("blocks", []):
            tid = b_data.get("tower_id", "Main_Tower")
            if tid not in self.tower_groups: self.tower_groups[tid] = []
            block = MassingBlock(b_data, self.elevation, custom_palette)
            self.tower_groups[tid].append(block)
            self.blocks.append(block)
            
        self.blocks.sort(key=lambda x: x.base_z)
        self.roof_json_data = []
        self.slab_json_data = []
        self.railing_json_data = [] # New collection for pure railing output
        
    def generate_topology(self):
        for tid, t_blocks in self.tower_groups.items():
            is_podium = "podium" in tid.lower()
            
            # Establish Floor Indexing across the stack
            z_set = set()
            for block in t_blocks:
                for j in range(block.floors + 1):
                    z_set.add(round(block.base_z + (j * block.floor_height), 3))
            sorted_z = sorted(list(z_set))
            z_to_idx = {z: i for i, z in enumerate(sorted_z)}
            
            for block in t_blocks:
                for j in range(block.floors):
                    rel_z = round(block.base_z + (j * block.floor_height), 3)
                    block.floor_indices[j] = z_to_idx[rel_z]
            
            # Organize geometric boundaries by elevation layer
            z_dict = {}
            for block in t_blocks:
                for j in range(block.floors + 1):
                    rel_z = round(block.base_z + (j * block.floor_height), 3)
                    if not is_podium and j == 0 and rel_z > 0.001: continue 
                    if rel_z not in z_dict: z_dict[rel_z] = []
                    z_dict[rel_z].append(block.base_curve.Duplicate())
                    
            # Compute Slabs and Roof Setbacks
            for rel_z in sorted(z_dict.keys()):
                crvs = z_dict[rel_z]
                f_idx = z_to_idx[rel_z]
                
                for c in crvs: 
                    if not c.IsClosed: c.MakeClosed(0.01)
                unioned_crvs = rg.Curve.CreateBooleanUnion(crvs, 0.01) or crvs
                final_slab_crvs = list(unioned_crvs)
                
                # Check for blocks above to compute roof setbacks
                blocks_above = [b for b in self.blocks if b.base_z <= rel_z + 0.001 and round(b.base_z + (b.floors * b.floor_height), 3) > rel_z + 0.001]
                bounds_above_crvs = [b.base_curve.Duplicate() for b in blocks_above]
                unioned_bounds_above = rg.Curve.CreateBooleanUnion(bounds_above_crvs, 0.01) if bounds_above_crvs else []
                intersect_crvs = safe_boolean_op(unioned_crvs, unioned_bounds_above, "int", 0.001)
                
                slab_area = get_brep_area(unioned_crvs)
                tower_area = get_brep_area(intersect_crvs)
                
                true_z = self.elevation + rel_z
                
                # STORE SLAB DATA
                self.slab_json_data.append({
                    "tower_id": tid,
                    "floor_index": f_idx,
                    "true_z": round(true_z, 3),
                    "area": round(slab_area, 2),
                    "boundary": [serialize_exact_curve(c) for c in final_slab_crvs]
                })
                
                # STORE ROOF & RAILING DATA
                is_roof = False
                if tower_area < 0.1:
                    is_roof = True; roof_type = "Roof Top"
                elif (slab_area - tower_area) > 1.0: 
                    is_roof = True; roof_type = "Podium Roof" if is_podium else "Setback Roof"
                    
                if is_roof:
                    exposed_crvs = safe_boolean_op(final_slab_crvs, intersect_crvs, "diff", 0.001) if intersect_crvs else final_slab_crvs
                    naked_crvs = get_naked_railings(exposed_crvs, intersect_crvs, 0.05)
                    programs_above = list(set([b.program for b in blocks_above]))
                    
                    # Log Main Roof Topology
                    self.roof_json_data.append({
                        "tower_id": tid,
                        "floor_index": f_idx,
                        "true_z": round(true_z, 3),
                        "type": roof_type,
                        "roof_area": round(max(0, slab_area - tower_area), 2),
                        "programs_above": programs_above,
                        "slab_bounds": [serialize_exact_curve(c) for c in unioned_crvs],
                        "tower_bounds": [serialize_exact_curve(c) for c in intersect_crvs]
                    })
                    
                    # Isolate Railings to its own JSON branch
                    if naked_crvs:
                        self.railing_json_data.append({
                            "tower_id": tid,
                            "floor_index": f_idx,
                            "true_z": round(true_z, 3),
                            "curves": [serialize_exact_curve(c) for c in naked_crvs]
                        })

# ==============================================================================
# MAIN EXECUTION
# ==============================================================================
exec_start = time.perf_counter()

json_in = globals().get('JSON_Payload', "")
palette_in = globals().get('ColorPalette', "")

parsed_palette = {}
if palette_in:
    try:
        for prog_key, rgb in json.loads(palette_in).items():
            if isinstance(rgb, list) and len(rgb) >= 3:
                parsed_palette[prog_key.strip()] = [rgb[0], rgb[1], rgb[2]]
    except: pass

# Output Dictionaries
masses_dict = {}
slabs_dict = {}
roofs_dict = {}
facade_dict = {}
railings_dict = {} # New Out Stream

dash_total_area = 0.0
dash_programs = {}
dash_buildings = {}

if not json_in:
    ghenv.Component.Message = "MP ENGINE\nTime: 0.0 ms\n---\nAwaiting Payload"
else:
    data = json.loads(json_in)
    
    for bldg_data in data.get("buildings", []):
        bldg = Building(bldg_data, parsed_palette)
        bldg.generate_topology()
        
        b_name = bldg.name
        masses_dict[b_name] = []
        facade_dict[b_name] = {}
        slabs_dict[b_name] = bldg.slab_json_data
        roofs_dict[b_name] = bldg.roof_json_data
        railings_dict[b_name] = bldg.railing_json_data
        
        dash_buildings[b_name] = {"total_area": 0.0, "programs": {}}
        
        for block in bldg.blocks:
            # Populate Massing JSON
            masses_dict[b_name].append({
                "tower_id": block.tower_id,
                "program": block.program,
                "total_height": block.floors * block.floor_height,
                "true_z": round(bldg.elevation + block.base_z, 3),
                "color": block.color,
                "boundary": serialize_exact_curve(block.base_curve) if block.base_curve else []
            })
            
            # Populate Dashboard Aggregations
            b_area = sum(block.areas)
            dash_total_area += b_area
            dash_programs[block.program] = dash_programs.get(block.program, 0.0) + b_area
            dash_buildings[b_name]["total_area"] += b_area
            dash_buildings[b_name]["programs"][block.program] = dash_buildings[b_name]["programs"].get(block.program, 0.0) + b_area
            
            # Populate Facade JSON
            if block.program not in facade_dict[b_name]:
                facade_dict[b_name][block.program] = []
                
            if block.base_curve:
                for i in range(block.floors):
                    # Calculate true elevation for this specific floor
                    floor_true_z = round(bldg.elevation + block.base_z + (i * block.floor_height), 3)
                    
                    facade_dict[b_name][block.program].append({
                        "tower_id": block.tower_id,
                        "floor_index": block.floor_indices[i],
                        "true_z": floor_true_z, # <--- THE MISSING LINK
                        "height": block.floor_height,
                        "boundary": serialize_exact_curve(block.base_curve)
                    })

    exec_time = (time.perf_counter() - exec_start) * 1000 
    
    # Restored Detailed UI Manifesto Layout
    msg_lines = [
        "MP ENGINE",
        f"Time: {exec_time:.1f} ms",
        "---",
        f"Total Gross Area: {dash_total_area:,.1f} SQM"
    ]
    for prog_name, prog_area in dash_programs.items():
        msg_lines.append(f"  • {prog_name}: {prog_area:,.1f} SQM")
        
    ghenv.Component.Message = "\n".join(msg_lines)

# Push Serialized Outputs
Masses_JSON = json.dumps(masses_dict, indent=2)
Slab_JSON = json.dumps(slabs_dict, indent=2)
Roof_JSON = json.dumps(roofs_dict, indent=2)
Facade_JSON = json.dumps(facade_dict, indent=2)
Railings_JSON = json.dumps(railings_dict, indent=2)

Dashboard_JSON = json.dumps({
    "total_area": dash_total_area if json_in else 0.0,
    "programs": dash_programs if json_in else {},
    "buildings": dash_buildings if json_in else {}
}, indent=2)