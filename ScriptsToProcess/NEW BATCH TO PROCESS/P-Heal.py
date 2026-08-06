import Rhino.Geometry as rg
import time

# 1. Component Metadata Setup
ghenv.Component.Name = "Polyline Healer"
ghenv.Component.NickName = "P-Heal"
ghenv.Component.Description = "Heals polylines by extending segments and creating boolean regions."

def heal_by_extension_logic(poly, extension_factor):
    if not poly or poly.Count < 2: 
        return poly
    
    # Fit plane to points to handle non-XY geometry
    success, plane = rg.Plane.FitPlaneToPoints(poly)
    if not success: 
        plane = rg.Plane.WorldXY

    # Explode and Extend
    segments = poly.GetSegments()
    extended_curves = []
    for line in segments:
        if line.Length > 0.001:
            vec = line.UnitTangent
            p0 = line.From - vec * extension_factor
            p1 = line.To + vec * extension_factor
            extended_curves.append(rg.LineCurve(p0, p1))

    # Create Boolean Regions
    res = rg.Curve.CreateBooleanRegions(extended_curves, plane, True, 0.001)
    
    if res and res.RegionCount > 0:
        all_pieces = []
        for i in range(res.RegionCount):
            all_pieces.extend(res.RegionCurves(i))
        
        # Union and find the largest outer boundary
        final_union = rg.Curve.CreateBooleanUnion(all_pieces, 0.001)
        if final_union:
            outer = sorted(final_union, key=lambda c: rg.AreaMassProperties.Compute(c).Area)[-1]
            success, result_poly = outer.TryGetPolyline()
            return result_poly if success else outer
    return poly

# 2. Main Execution with Profiling
start_time = time.time()

if isinstance(p, list):
    a = [heal_by_extension_logic(item, tol) for item in p]
else:
    a = heal_by_extension_logic(p, tol)

end_time = time.time()
calc_time_ms = (end_time - start_time) * 1000

# 3. Dynamic Message Update
# Line 1: Nickname | Line 2: Version | Line 3: Calculation Time
version = "v1.2"
message = f"{version}\n{calc_time_ms:.2f} ms"
ghenv.Component.Message = message