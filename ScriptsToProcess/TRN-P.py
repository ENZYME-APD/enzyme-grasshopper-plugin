"""
=== TERRAIN GENERATOR INTERFACE ===
[Inputs]
In_Boundary        : Curve (Closed boundary limits)
In_MaxHeight       : Float (Maximum elevation in meters)
In_MinHeight       : Float (Minimum elevation in meters)
In_Seed            : Integer (Randomization seed)
In_PatternSizeXY   : Float (List of feature sizes in meters)
In_PatternHeightZ  : Float (List of relative feature strengths)
In_ContourStep     : Float (Interval for normal contour lines)
In_MainStep        : Float (Interval for main contour lines)
In_Colors          : Color (List of gradient colors based on height)
In_Resolution      : Integer (Grid density, default is 100)
In_UseSlopeColor   : Boolean (Toggle steep slope coloring)
In_SlopeColor      : Color (Color applied to sheer cliffs/slopes)
In_SlopeAngle      : Float (Angle where slope color starts)
In_TerrainStyle    : Integer (0 = Realistic Soft Hills, 1 = Ridged/Cellular Pattern)
In_Solid           : Boolean (Toggle closed mesh extrusion)
In_BaseCol         : Color (Color for the extruded solid base section)
In_TreeMsk         : Float (Coverage mask threshold 0.0 to 1.0)
In_TreeDns         : Float (Density multiplier inside mask areas)
In_TreeSeed        : Integer (Dedicated seed for the forest noise map)
In_TreeZMin        : Float (Minimum relative elevation for trees 0.0 to 1.0)
In_TreeZMax        : Float (Maximum relative elevation for trees 0.0 to 1.0)

[Outputs]
Instructions_Out   : String (Interface mapping guide)
Mesh_Out           : Mesh (Gradient colored terrain geometry)
NormContours_Out   : Curve (Standard contour lines)
MainContours_Out   : Curve (Major interval contour lines)
Trees_Out          : Point3d (Scattered point coordinates for trees)
"""

import time
import math
import random
import System
from System.Collections.Generic import List, IEnumerable
import Rhino.Geometry as rg
from System.Drawing import Color
from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path

# --- COMPONENT METADATA ---
ghenv.Component.Name = "Terrain Generator Pro"
ghenv.Component.NickName = "TRN-P"
ghenv.Component.Description = "Generates topography with noise-masked procedural forest scattering and strict elevation limits."

# --- INTERFACE INSTRUCTIONS ---
Instructions_Out = """
=== TERRAIN GENERATOR GRIPS ===
In_Boundary       -> Curve (Tree)
In_MaxHeight      -> Number (Tree)
In_MinHeight      -> Number (Tree)
In_Seed           -> Integer (Tree)
In_PatternSizeXY  -> Number (Tree)
In_PatternHeightZ -> Number (Tree)
In_ContourStep    -> Number (Tree)
In_MainStep       -> Number (Tree)
In_Colors         -> Color (Tree)
In_Resolution     -> Integer (Tree)
In_UseSlopeColor  -> Boolean (Tree)
In_SlopeColor     -> Color (Tree)
In_SlopeAngle     -> Number (Tree)
In_TerrainStyle   -> Integer (Tree)
In_Solid          -> Boolean (Tree)
In_BaseCol        -> Color (Tree)
In_TreeMsk        -> Number (Tree)
In_TreeDns        -> Number (Tree)
In_TreeSeed       -> Integer (Tree)
In_TreeZMin       -> Number (Tree) [e.g. 0.15]
In_TreeZMax       -> Number (Tree) [e.g. 0.85]
"""

# --- DEFENSIVE DATA STRUCTURE WRAPPER ---
def wrap_to_tree(input_data):
    tree = DataTree[System.Object]()
    if input_data is None: return tree
    if hasattr(input_data, 'BranchCount'): return input_data
    path = GH_Path(0)
    if isinstance(input_data, (list, tuple)):
        for item in input_data: tree.Add(item, path)
    else:
        tree.Add(input_data, path)
    return tree

def safe_tree_item(tree, branch_idx, item_idx, default):
    if not tree or branch_idx >= tree.BranchCount: return default
    branch = tree.Branch(branch_idx)
    if not branch or len(branch) == 0: return default
    return branch[item_idx] if item_idx < len(branch) else branch[-1]

def safe_tree_list(tree, branch_idx, default):
    if not tree or branch_idx >= tree.BranchCount: return default
    branch = tree.Branch(branch_idx)
    return list(branch) if branch and len(branch) > 0 else default

# --- CORE ALGORITHMS (PERLIN NOISE + DOMAIN ROTATION) ---
def hash_2d(x, y, seed):
    val = math.sin(x * 12.9898 + y * 78.233 + seed * 37.719) * 43758.5453
    angle = (val - math.floor(val)) * math.pi * 2.0
    return math.cos(angle), math.sin(angle)

def perlin_noise(x, y, seed):
    ix, iy = math.floor(x), math.floor(y)
    fx, fy = x - ix, y - iy
    
    g00 = hash_2d(ix, iy, seed)
    g10 = hash_2d(ix + 1, iy, seed)
    g01 = hash_2d(ix, iy + 1, seed)
    g11 = hash_2d(ix + 1, iy + 1, seed)
    
    d00 = fx * g00[0] + fy * g00[1]
    d10 = (fx - 1.0) * g10[0] + fy * g10[1]
    d01 = fx * g01[0] + (fy - 1.0) * g01[1]
    d11 = (fx - 1.0) * g11[0] + (fy - 1.0) * g11[1]
    
    u = fx * fx * fx * (fx * (fx * 6.0 - 15.0) + 10.0)
    v = fy * fy * fy * (fy * (fy * 6.0 - 15.0) + 10.0)
    
    nx0 = d00 * (1.0 - u) + d10 * u
    nx1 = d01 * (1.0 - u) + d11 * u
    
    return (nx0 * (1.0 - v) + nx1 * v) * 1.25

def generate_fractal_noise(x, y, seed, octaves, weights, sizes, style):
    z, weight_sum = 0.0, 0.0
    cos_r, sin_r = 0.8, 0.6 
    cx, cy = x, y
    
    for i in range(octaves):
        w = weights[i] if i < len(weights) else weights[-1] * (0.5 ** (i - len(weights) + 1))
        size = sizes[i] if i < len(sizes) else sizes[-1] * (0.5 ** (i - len(sizes) + 1))
        freq = 1.0 / max(0.001, size) 
        
        n = perlin_noise(cx * freq, cy * freq, seed + i)
        
        if style == 1:
            n = 1.0 - abs(n)
            n *= n
            z += n * w
        else:
            z += n * w
            
        weight_sum += w
        nx = cx * cos_r - cy * sin_r
        ny = cx * sin_r + cy * cos_r
        cx, cy = nx, ny
        
    if style == 1:
        return z / max(0.001, weight_sum)
    else:
        val = (z / max(0.001, weight_sum)) * 0.5 + 0.5
        return max(0.0, min(1.0, val))

def get_gradient_color(t, colors):
    if not colors: return Color.White
    if len(colors) == 1: return colors[0]
    t = max(0.0, min(1.0, t))
    idx = t * (len(colors) - 1)
    i = int(math.floor(idx))
    frac = idx - i
    if i >= len(colors) - 1: return colors[-1]
    
    c1, c2 = colors[i], colors[i+1]
    r = int(c1.R + (c2.R - c1.R) * frac)
    g = int(c1.G + (c2.G - c1.G) * frac)
    b = int(c1.B + (c2.B - c1.B) * frac)
    return Color.FromArgb(r, g, b)

def blend_colors(c1, c2, t):
    t = max(0.0, min(1.0, t))
    r = int(c1.R + (c2.R - c1.R) * t)
    g = int(c1.G + (c2.G - c1.G) * t)
    b = int(c1.B + (c2.B - c1.B) * t)
    return Color.FromArgb(r, g, b)

# --- MAIN EXECUTION ---
t_start = time.perf_counter()

Mesh_Out = DataTree[System.Object]()
NormContours_Out = DataTree[System.Object]()
MainContours_Out = DataTree[System.Object]()
Trees_Out = DataTree[System.Object]()

total_items = 0
total_trees = 0
total_area = 0.0
global_min_h = float('inf')
global_max_h = float('-inf')

if 'In_Boundary' in globals():
    boundary_tree = wrap_to_tree(In_Boundary)
    max_h_tree = wrap_to_tree(In_MaxHeight) if 'In_MaxHeight' in globals() else None
    min_h_tree = wrap_to_tree(In_MinHeight) if 'In_MinHeight' in globals() else None
    seed_tree = wrap_to_tree(In_Seed) if 'In_Seed' in globals() else None
    size_tree = wrap_to_tree(In_PatternSizeXY) if 'In_PatternSizeXY' in globals() else None
    weight_tree = wrap_to_tree(In_PatternHeightZ) if 'In_PatternHeightZ' in globals() else None
    colors_tree = wrap_to_tree(In_Colors) if 'In_Colors' in globals() else None
    res_tree = wrap_to_tree(In_Resolution) if 'In_Resolution' in globals() else None
    c_step_tree = wrap_to_tree(In_ContourStep) if 'In_ContourStep' in globals() else None
    m_step_tree = wrap_to_tree(In_MainStep) if 'In_MainStep' in globals() else None
    use_slope_tree = wrap_to_tree(In_UseSlopeColor) if 'In_UseSlopeColor' in globals() else None
    slope_col_tree = wrap_to_tree(In_SlopeColor) if 'In_SlopeColor' in globals() else None
    slope_ang_tree = wrap_to_tree(In_SlopeAngle) if 'In_SlopeAngle' in globals() else None
    style_tree = wrap_to_tree(In_TerrainStyle) if 'In_TerrainStyle' in globals() else None
    solid_tree = wrap_to_tree(In_Solid) if 'In_Solid' in globals() else None
    base_col_tree = wrap_to_tree(In_BaseCol) if 'In_BaseCol' in globals() else None
    tree_msk_tree = wrap_to_tree(In_TreeMsk) if 'In_TreeMsk' in globals() else None
    tree_dns_tree = wrap_to_tree(In_TreeDns) if 'In_TreeDns' in globals() else None
    tree_seed_tree = wrap_to_tree(In_TreeSeed) if 'In_TreeSeed' in globals() else None
    tree_zmin_tree = wrap_to_tree(In_TreeZMin) if 'In_TreeZMin' in globals() else None
    tree_zmax_tree = wrap_to_tree(In_TreeZMax) if 'In_TreeZMax' in globals() else None

    for b_idx in range(boundary_tree.BranchCount):
        path = boundary_tree.Path(b_idx)
        Mesh_Out.EnsurePath(path)
        NormContours_Out.EnsurePath(path)
        MainContours_Out.EnsurePath(path)
        Trees_Out.EnsurePath(path)
        
        boundaries = boundary_tree.Branch(b_idx)
        if not boundaries: continue
        
        max_h = safe_tree_item(max_h_tree, b_idx, 0, 100.0)
        min_h = safe_tree_item(min_h_tree, b_idx, 0, 0.0)
        seed = safe_tree_item(seed_tree, b_idx, 0, 42)
        res = int(safe_tree_item(res_tree, b_idx, 0, 100))
        c_step = safe_tree_item(c_step_tree, b_idx, 0, 1.0)
        m_step = safe_tree_item(m_step_tree, b_idx, 0, 5.0)
        
        use_slope = safe_tree_item(use_slope_tree, b_idx, 0, False)
        slope_col = safe_tree_item(slope_col_tree, b_idx, 0, Color.DarkGray)
        slope_angle = safe_tree_item(slope_ang_tree, b_idx, 0, 30.0)
        t_style = int(safe_tree_item(style_tree, b_idx, 0, 0))
        make_solid = safe_tree_item(solid_tree, b_idx, 0, False)
        base_col = safe_tree_item(base_col_tree, b_idx, 0, Color.DimGray)
        
        mask_val = max(0.0, min(1.0, safe_tree_item(tree_msk_tree, b_idx, 0, 0.0)))
        dens_val = max(0.0, safe_tree_item(tree_dns_tree, b_idx, 0, 0.0))
        
        tree_seed = int(safe_tree_item(tree_seed_tree, b_idx, 0, 12345))
        tree_zmin = max(0.0, min(1.0, safe_tree_item(tree_zmin_tree, b_idx, 0, 0.15)))
        tree_zmax = max(0.0, min(1.0, safe_tree_item(tree_zmax_tree, b_idx, 0, 0.85)))
        
        sizes = safe_tree_list(size_tree, b_idx, [500.0, 150.0, 30.0])
        weights = safe_tree_list(weight_tree, b_idx, [1.0, 0.3, 0.05])
        colors = safe_tree_list(colors_tree, b_idx, [Color.LightGreen, Color.SaddleBrown, Color.White])
        octaves = max(len(sizes), len(weights))

        for curve in boundaries:
            if not curve or not curve.IsClosed: continue
            
            bbox = curve.GetBoundingBox(True)
            w = bbox.Max.X - bbox.Min.X
            h = bbox.Max.Y - bbox.Min.Y
            if w <= 0 or h <= 0: continue
            
            total_area += rg.AreaMassProperties.Compute(curve).Area
            
            nx = max(2, res)
            ny = max(2, int(nx * (h / w)))
            grid_step = w / nx
            
            # --- 1. Discretize flush boundary points ---
            flat_crv = curve.DuplicateCurve()
            flat_crv.Translate(rg.Vector3d(0, 0, -bbox.Min.Z))
            crv_length = flat_crv.GetLength()
            div_count = max(4, int(crv_length / grid_step))
            
            pts_bnd = []
            t_vals = flat_crv.DivideByCount(div_count, True)
            if t_vals:
                pts_bnd = [flat_crv.PointAt(t) for t in t_vals]
            else:
                nc = flat_crv.ToPolyline(0.01, 0.1, 0.0, 0.0)
                if nc: pts_bnd = list(nc.ToPolyline())
                
            if len(pts_bnd) > 0 and pts_bnd[0].DistanceTo(pts_bnd[-1]) > 0.001:
                pts_bnd.append(pts_bnd[0])
                
            if not pts_bnd: continue
                
            net_bnd = List[rg.Point3d]()
            for p in pts_bnd: net_bnd.Add(p)
            
            net_boundaries = List[IEnumerable[rg.Point3d]]()
            net_boundaries.Add(net_bnd)
            
            net_all_pts = List[rg.Point3d]()
            for p in pts_bnd: net_all_pts.Add(p)
            
            # --- 2. Scatter interior grid points ---
            min_dist = grid_step * 0.35
            for j in range(ny + 1):
                y = bbox.Min.Y + (j / ny) * h
                for i in range(nx + 1):
                    x = bbox.Min.X + (i / nx) * w
                    pt = rg.Point3d(x, y, 0)
                    if flat_crv.Contains(pt, rg.Plane.WorldXY, 0.01) == rg.PointContainment.Inside:
                        rc, t = flat_crv.ClosestPoint(pt)
                        if rc and pt.DistanceTo(flat_crv.PointAt(t)) > min_dist:
                            net_all_pts.Add(pt)
                            
            # --- 3. Constrained Delaunay Tessellation ---
            mesh = rg.Mesh.CreateFromTessellation(net_all_pts, net_boundaries, rg.Plane.WorldXY, False)
            if not mesh or not mesh.IsValid: continue
            
            # --- 4. Elevation Pass ---
            actual_min_z = float('inf')
            actual_max_z = float('-inf')
            
            for v_idx in range(mesh.Vertices.Count):
                v = mesh.Vertices[v_idx]
                t_val = generate_fractal_noise(v.X, v.Y, seed, octaves, weights, sizes, t_style)
                z = min_h + t_val * (max_h - min_h)
                
                actual_min_z = min(actual_min_z, z)
                actual_max_z = max(actual_max_z, z)
                global_min_h = min(global_min_h, z)
                global_max_h = max(global_max_h, z)
                
                mesh.Vertices.SetVertex(v_idx, v.X, v.Y, z)
                
            # --- Flush flat caches and rebuild true 3D normals ---
            mesh.RebuildNormals()
            mesh.VertexColors.Clear()
            
            actual_h_range = actual_max_z - actual_min_z if (actual_max_z - actual_min_z) > 0.001 else 0.001
            slope_rad = math.radians(min(max(slope_angle, 0.0), 90.0))
            threshold_z = math.cos(slope_rad) 
            falloff_range = 0.20
            
            # --- 5A. Procedural Forest Scattering (Mask, Density, & Configurable Z-Limits) ---
            mesh.FaceNormals.ComputeFaceNormals()
            if mask_val > 0.0 and dens_val > 0.0:
                # Patches roughly every 150m mapped using the dedicated tree seed
                tree_freq = 1.0 / 150.0 
                
                for f_idx in range(mesh.Faces.Count):
                    face = mesh.Faces[f_idx]
                    if not face.IsTriangle: continue
                    
                    center = mesh.Faces.GetFaceCenter(f_idx)
                    f_norm = mesh.FaceNormals[f_idx]
                    
                    # Guard 1: No trees on steep cliffs
                    if abs(f_norm.Z) < 0.7: continue
                    
                    # Guard 2: Strict configurable elevation limits
                    t_height = (center.Z - actual_min_z) / actual_h_range
                    if t_height < tree_zmin or t_height > tree_zmax: continue
                    
                    # Evaluate Forest Noise Map
                    patch_noise = perlin_noise(center.X * tree_freq, center.Y * tree_freq, tree_seed)
                    patch_val = (patch_noise * 0.5) + 0.5
                    
                    if patch_val < mask_val:
                        intensity = 1.0 - (patch_val / mask_val)
                        prob = intensity * dens_val * 3.0 
                        spawn_count = int(prob)
                        
                        if random.random() < (prob - spawn_count):
                            spawn_count += 1
                        
                        pA = mesh.Vertices[face.A]
                        pB = mesh.Vertices[face.B]
                        pC = mesh.Vertices[face.C]
                        
                        for _ in range(spawn_count):
                            r1, r2 = random.random(), random.random()
                            if r1 + r2 > 1.0:
                                r1, r2 = 1.0 - r1, 1.0 - r2
                                
                            tX = pA.X * (1.0 - r1 - r2) + pB.X * r1 + pC.X * r2
                            tY = pA.Y * (1.0 - r1 - r2) + pB.Y * r1 + pC.Y * r2
                            tZ = pA.Z * (1.0 - r1 - r2) + pB.Z * r1 + pC.Z * r2
                            
                            Trees_Out.Add(rg.Point3d(tX, tY, tZ), path)
                            total_trees += 1
            
            # --- 5B. Normals & Relative Coloring ---
            for v_idx in range(mesh.Vertices.Count):
                pt = mesh.Vertices[v_idx]
                t_height = (pt.Z - actual_min_z) / actual_h_range
                base_color = get_gradient_color(t_height, colors)
                
                if use_slope:
                    normal = mesh.Normals[v_idx]
                    nz = abs(normal.Z)
                    if nz < threshold_z:
                        blend_factor = min((threshold_z - nz) / falloff_range, 1.0)
                        final_color = blend_colors(base_color, slope_col, blend_factor)
                        mesh.VertexColors.Add(final_color)
                    else:
                        mesh.VertexColors.Add(base_color)
                else:
                    mesh.VertexColors.Add(base_color)
            
            # --- 6. Contours (Surface Only) ---
            if c_step > 0.0:
                mesh_box = mesh.GetBoundingBox(True)
                start_z = math.floor(mesh_box.Min.Z / c_step) * c_step
                p0 = rg.Point3d(0, 0, start_z)
                p1 = rg.Point3d(0, 0, mesh_box.Max.Z + c_step)
                contours = rg.Mesh.CreateContourCurves(mesh, p0, p1, c_step)
                
                if contours:
                    for crv in contours:
                        pt_z = crv.PointAtStart.Z
                        rem = abs(pt_z % m_step)
                        if rem < 0.001 or abs(rem - m_step) < 0.001:
                            MainContours_Out.Add(crv, path)
                        else:
                            NormContours_Out.Add(crv, path)

            # --- 7. Extrude Solid Base ---
            if make_solid:
                base_z = actual_min_z - max(1.0, (actual_max_z - actual_min_z) * 0.1)
                
                bottom_mesh = mesh.Duplicate()
                for i in range(bottom_mesh.Vertices.Count):
                    v = bottom_mesh.Vertices[i]
                    bottom_mesh.Vertices.SetVertex(i, v.X, v.Y, base_z)
                
                bottom_mesh.Flip(True, True, True)
                
                bottom_mesh.VertexColors.Clear()
                for i in range(bottom_mesh.Vertices.Count):
                    bottom_mesh.VertexColors.Add(base_col)
                
                wall_mesh = rg.Mesh()
                naked_polys = mesh.GetNakedEdges()
                if naked_polys:
                    for poly in naked_polys:
                        for i in range(poly.Count - 1):
                            p0, p1 = poly[i], poly[i+1]
                            p0_b = rg.Point3d(p0.X, p0.Y, base_z)
                            p1_b = rg.Point3d(p1.X, p1.Y, base_z)
                            
                            v0 = wall_mesh.Vertices.Add(p0)
                            v1 = wall_mesh.Vertices.Add(p1)
                            v2 = wall_mesh.Vertices.Add(p1_b)
                            v3 = wall_mesh.Vertices.Add(p0_b)
                            
                            wall_mesh.VertexColors.Add(base_col)
                            wall_mesh.VertexColors.Add(base_col)
                            wall_mesh.VertexColors.Add(base_col)
                            wall_mesh.VertexColors.Add(base_col)
                            
                            wall_mesh.Faces.AddFace(v0, v1, v2, v3)
                
                mesh.Append(bottom_mesh)
                mesh.Append(wall_mesh)
                mesh.Weld(math.pi)
                
                mesh.UnifyNormals()
                
                if mesh.SolidOrientation() == -1:
                    mesh.Flip(True, True, True)
                    
                mesh.Normals.ComputeNormals()
                
            mesh.Compact()
            Mesh_Out.Add(mesh, path)
            total_items += 1

# --- TELEMETRY & HUD ---
t_end = time.perf_counter()
ms = (t_end - t_start) * 1000

if global_min_h == float('inf'): global_min_h, global_max_h = 0, 0

ghenv.Component.Message = (
    f"{ghenv.Component.NickName}\n"
    f"Time: {ms:.1f} ms\n"
    f"---\n"
    f"Items: {total_items}\n"
    f"Trees: {total_trees}\n"
    f"Height: {global_min_h:.1f}m - {global_max_h:.1f}m\n"
    f"Area: {total_area:,.0f}m2"
)