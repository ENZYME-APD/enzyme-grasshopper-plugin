"""
=============================================================================
COMPONENT INTERFACE CONTRACT
=============================================================================
INPUTS:
- TargetMeshes       (DataTree) : The meshes to analyze.
- SearchRings        (DataTree) : Topological radius in rings (integer).
- ProminenceLimit    (DataTree) : Minimum Z-delta to be considered a peak/valley (float).
- CustomColors       (DataTree) : Custom colormap list (System.Drawing.Color).
- CullGlobals        (DataTree) : Toggle to remove the absolute highest/lowest points (bool).
- AvoidBoundaries    (DataTree) : Toggle to ignore naked edge vertices (bool).
- EnableHeatmap      (DataTree) : Toggle to compute and output the vertex heatmap mesh (bool).
- RotationPlane      (DataTree) : Orientation plane for the bounding box sectioning (Plane).
- SectionsX          (DataTree) : Number of sections running parallel to the X-axis (integer).
- SectionsY          (DataTree) : Number of sections running parallel to the Y-axis (integer).
- LayoutFlat         (DataTree) : Toggle to generate 2D XY print layouts next to the mesh (bool).

OUTPUTS:
- Instructions_Out   (str)      : Component documentation and usage manual.
- LocalPeaks         (DataTree) : Output points for local highs.
- PeakElevations     (DataTree) : Z-values for local highs.
- GlobalMaxPoint     (DataTree) : Absolute highest point on the mesh.
- GlobalMaxElevation (DataTree) : Absolute highest Z-value.
- LocalValleys       (DataTree) : Output points for local lows.
- ValleyElevations   (DataTree) : Z-values for local lows.
- GlobalMinPoint     (DataTree) : Absolute lowest point on the mesh.
- GlobalMinElevation (DataTree) : Absolute lowest Z-value.
- HeatmapMeshes      (DataTree) : The vertex-colored duplicate mesh.
- SectionOutlinesX   (DataTree) : 3D Polylines running parallel to the X-axis.
- SectionOutlinesY   (DataTree) : 3D Polylines running parallel to the Y-axis.
- FlatSectionsX      (DataTree) : 2D X-Sections stacked downwards (-Y direction).
- FlatSectionsY      (DataTree) : 2D Y-Sections stacked leftwards (-X direction).
- LabelText3D        (DataTree) : Text strings for 3D section labels (e.g. SecX_01).
- LabelPoints3D      (DataTree) : Points for 3D section labels (offset by 2m).
- LabelTextFlat      (DataTree) : Text strings for the flattened section layout.
- LabelPointsFlat    (DataTree) : Points for the flattened section layout (offset by 2m).
- SectionMetadata    (DataTree) : Dictionary keys containing spatial transform & ID data.
=============================================================================
"""

import Rhino.Geometry as rg
import System.Drawing as col
import time
import math
from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path

# =============================================================================
# 1. COMPONENT METADATA & INSTRUCTIONS
# =============================================================================
ghenv.Component.Name = "Mesh Terrain Analyzer"
ghenv.Component.NickName = "Terrain"
ghenv.Component.Description = "Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels."

Instructions_Out = __doc__

# =============================================================================
# 2. HELPER FUNCTIONS
# =============================================================================
def extract_first_item(tree, default_val):
    if tree is not None and tree.DataCount > 0:
        return tree.Branch(tree.Paths[0])[0]
    return default_val

def get_topo_neighbors(topology, start_idx, steps):
    visited = {start_idx}
    current_layer = {start_idx}
    for _ in range(steps):
        next_layer = set()
        for idx in current_layer:
            neighbors = topology.ConnectedTopologyVertices(idx)
            for n_idx in neighbors:
                if n_idx not in visited:
                    next_layer.add(n_idx)
                    visited.add(n_idx)
        if not next_layer: break
        current_layer = next_layer
    visited.remove(start_idx)
    return visited

def compute_heatmap_color(val, min_val, max_val, color_list):
    if max_val == min_val: 
        return col.Color.Gray
    param = max(0.0, min(1.0, float(val - min_val) / (max_val - min_val)))
    if color_list and len(color_list) > 1:
        idx_f = param * (len(color_list) - 1)
        idx_low = int(idx_f)
        idx_high = min(idx_low + 1, len(color_list) - 1)
        t = idx_f - idx_low
        c1, c2 = color_list[idx_low], color_list[idx_high]
        return col.Color.FromArgb(
            int(c1.R + (c2.R - c1.R) * t), 
            int(c1.G + (c2.G - c1.G) * t), 
            int(c1.B + (c2.B - c1.B) * t)
        )
    if param < 0.5: 
        return col.Color.FromArgb(int(param * 2 * 255), 255, int((1 - param * 2) * 255))
    return col.Color.FromArgb(255, int((1 - (param - 0.5) * 2) * 255), 0)

# =============================================================================
# 3. MAIN EXECUTION & TELEMETRY
# =============================================================================
perf_start = time.perf_counter()

# Initialize Output DataTrees
LocalPeaks = DataTree[object]()
PeakElevations = DataTree[object]()
GlobalMaxPoint = DataTree[object]()
GlobalMaxElevation = DataTree[object]()
LocalValleys = DataTree[object]()
ValleyElevations = DataTree[object]()
GlobalMinPoint = DataTree[object]()
GlobalMinElevation = DataTree[object]()
HeatmapMeshes = DataTree[object]()
SectionOutlinesX = DataTree[object]()
SectionOutlinesY = DataTree[object]()
FlatSectionsX = DataTree[object]()
FlatSectionsY = DataTree[object]()
LabelText3D = DataTree[object]()
LabelPoints3D = DataTree[object]()
LabelTextFlat = DataTree[object]()
LabelPointsFlat = DataTree[object]()
SectionMetadata = DataTree[object]()

# Extract Settings
rings = int(extract_first_item(SearchRings, 5))
prominence = float(extract_first_item(ProminenceLimit, 0.5))
cull_globals = bool(extract_first_item(CullGlobals, False))
avoid_bounds = bool(extract_first_item(AvoidBoundaries, False))
enable_heatmap = bool(extract_first_item(EnableHeatmap, True))
sec_plane = extract_first_item(RotationPlane, rg.Plane.WorldXY)
sec_count_x = int(extract_first_item(SectionsX, 0))
sec_count_y = int(extract_first_item(SectionsY, 0))
layout_flat = bool(extract_first_item(LayoutFlat, False))

custom_color_list = []
if CustomColors is not None and CustomColors.DataCount > 0:
    for color_path in CustomColors.Paths:
        custom_color_list.extend(CustomColors.Branch(color_path))

# Global Telemetry Trackers
total_peaks_found = 0
total_valleys_found = 0
global_terrain_z_min = float('inf')
global_terrain_z_max = float('-inf')
total_z_sum = 0.0
total_vertices_count = 0
total_terrain_area = 0.0
total_sections_x = 0
total_sections_y = 0

# Determine global bounding box for print layout
global_bb = rg.BoundingBox.Empty
if TargetMeshes is not None and TargetMeshes.DataCount > 0:
    for path in TargetMeshes.Paths:
        for m in TargetMeshes.Branch(path):
            if m and m.IsValid:
                global_bb.Union(m.GetBoundingBox(True))

# Layout Cursors matching your sketch logic
padding = global_bb.Diagonal.Length * 0.05 if global_bb.IsValid else 10.0

# X Sections align left, stack downwards (-Y)
cursor_y_Xsecs = global_bb.Min.Y - padding

# Y Sections align bottom, stack leftwards (-X)
cursor_x_Ysecs = global_bb.Min.X - padding

if TargetMeshes is not None and TargetMeshes.DataCount > 0:
    for path_idx in range(TargetMeshes.Paths.Count):
        current_path = TargetMeshes.Paths[path_idx]
        branch_meshes = TargetMeshes.Branch(current_path)
        
        # Structure Preservations
        LocalPeaks.EnsurePath(current_path)
        PeakElevations.EnsurePath(current_path)
        GlobalMaxPoint.EnsurePath(current_path)
        GlobalMaxElevation.EnsurePath(current_path)
        LocalValleys.EnsurePath(current_path)
        ValleyElevations.EnsurePath(current_path)
        GlobalMinPoint.EnsurePath(current_path)
        GlobalMinElevation.EnsurePath(current_path)
        HeatmapMeshes.EnsurePath(current_path)
        SectionOutlinesX.EnsurePath(current_path)
        SectionOutlinesY.EnsurePath(current_path)
        FlatSectionsX.EnsurePath(current_path)
        FlatSectionsY.EnsurePath(current_path)
        LabelText3D.EnsurePath(current_path)
        LabelPoints3D.EnsurePath(current_path)
        LabelTextFlat.EnsurePath(current_path)
        LabelPointsFlat.EnsurePath(current_path)
        SectionMetadata.EnsurePath(current_path)
        
        for mesh in branch_meshes:
            if not mesh or not mesh.IsValid:
                continue
                
            topology = mesh.TopologyVertices
            vertices = mesh.Vertices
            is_naked_edge = mesh.GetNakedEdgePointStatus()
            
            amp = rg.AreaMassProperties.Compute(mesh)
            if amp is not None:
                total_terrain_area += amp.Area
            
            z_values = [vertices[idx].Z for idx in range(topology.Count)]
            if not z_values: continue
            
            z_min = min(z_values)
            z_max = max(z_values)
            
            global_terrain_z_min = min(global_terrain_z_min, z_min)
            global_terrain_z_max = max(global_terrain_z_max, z_max)
            total_z_sum += sum(z_values)
            total_vertices_count += len(z_values)
            
            global_min_idx = z_values.index(z_min)
            global_max_idx = z_values.index(z_max)
            
            found_peaks = []
            found_valleys = []
            
            b_min_x, b_max_x = float('inf'), float('-inf')
            b_min_y, b_max_y = float('inf'), float('-inf')
            
            for v_idx in range(topology.Count):
                pt = rg.Point3d(vertices[v_idx])
                
                if sec_count_x > 0 or sec_count_y > 0:
                    success, u, v = sec_plane.ClosestParameter(pt)
                    if success:
                        if u < b_min_x: b_min_x = u
                        if u > b_max_x: b_max_x = u
                        if v < b_min_y: b_min_y = v
                        if v > b_max_y: b_max_y = v

                if avoid_bounds and is_naked_edge[v_idx]: 
                    continue
                    
                current_z = z_values[v_idx]
                immediate_neighbors = topology.ConnectedTopologyVertices(v_idx)
                
                is_local_max = True
                is_local_min = True
                
                for n_idx in immediate_neighbors:
                    n_z = z_values[n_idx]
                    if n_z > current_z + 0.0001: is_local_max = False
                    if n_z < current_z - 0.0001: is_local_min = False
                
                if not is_local_max and not is_local_min:
                    continue
                    
                full_neighbors = get_topo_neighbors(topology, v_idx, rings)
                if not full_neighbors:
                    continue
                
                max_neighbor_z = float('-inf')
                min_neighbor_z = float('inf')
                
                for n_idx in full_neighbors:
                    n_z = z_values[n_idx]
                    if n_z > max_neighbor_z: max_neighbor_z = n_z
                    if n_z < min_neighbor_z: min_neighbor_z = n_z
                    
                    if is_local_max and n_z > current_z + 0.0001: is_local_max = False
                    if is_local_min and n_z < current_z - 0.0001: is_local_min = False
                
                if is_local_max and (current_z - min_neighbor_z) >= prominence:
                    found_peaks.append((v_idx, current_z, pt))
                elif is_local_min and (max_neighbor_z - current_z) >= prominence:
                    found_valleys.append((v_idx, current_z, pt))

            peak_indices = {p[0] for p in found_peaks}
            valley_indices = {v[0] for v in found_valleys}

            if not avoid_bounds or not is_naked_edge[global_min_idx]:
                if global_min_idx not in valley_indices:
                    found_valleys.append((global_min_idx, z_min, rg.Point3d(vertices[global_min_idx])))
            if not avoid_bounds or not is_naked_edge[global_max_idx]:
                if global_max_idx not in peak_indices:
                    found_peaks.append((global_max_idx, z_max, rg.Point3d(vertices[global_max_idx])))

            found_peaks.sort(key=lambda x: x[1], reverse=True)
            found_valleys.sort(key=lambda x: x[1])

            if cull_globals:
                found_peaks = [p for p in found_peaks if p[0] != global_max_idx]
                found_valleys = [v for v in found_valleys if v[0] != global_min_idx]

            for data in found_peaks:
                LocalPeaks.Add(data[2], current_path)
                PeakElevations.Add(round(data[1], 2), current_path)
                total_peaks_found += 1
            for data in found_valleys:
                LocalValleys.Add(data[2], current_path)
                ValleyElevations.Add(round(data[1], 2), current_path)
                total_valleys_found += 1
                
            GlobalMaxPoint.Add(rg.Point3d(vertices[global_max_idx]), current_path)
            GlobalMaxElevation.Add(round(z_max, 2), current_path)
            GlobalMinPoint.Add(rg.Point3d(vertices[global_min_idx]), current_path)
            GlobalMinElevation.Add(round(z_min, 2), current_path)

            if enable_heatmap:
                heatmap_dup = mesh.DuplicateMesh()
                heatmap_dup.VertexColors.Clear()
                for z_val in z_values:
                    color = compute_heatmap_color(z_val, z_min, z_max, custom_color_list)
                    heatmap_dup.VertexColors.Add(color)
                HeatmapMeshes.Add(heatmap_dup, current_path)

            # -----------------------------------------------------------------
            # BI-DIRECTIONAL BOUNDING BOX SECTIONING & LABEL LOGIC
            # -----------------------------------------------------------------
            
            # X Sections (Short): Run parallel to X, Step along Y, Stack downwards (-Y)
            if sec_count_x > 0 and (b_max_y - b_min_y) > 1e-5:
                y_vals = [(b_min_y + b_max_y) * 0.5] if sec_count_x == 1 else [b_min_y + i * ((b_max_y - b_min_y) / (sec_count_x - 1)) for i in range(sec_count_x)]
                for i, v in enumerate(y_vals):
                    sec_id = "SecX_{:02d}".format(i + 1)
                    origin = sec_plane.PointAt(0, v, 0)
                    cut_plane_x_dir = rg.Plane(origin, sec_plane.XAxis, sec_plane.ZAxis)
                    polys = rg.Intersect.Intersection.MeshPlane(mesh, cut_plane_x_dir)
                    
                    if polys:
                        valid_crvs = []
                        for p in polys:
                            if p.IsValid and p.Count > 1:
                                crv = rg.PolylineCurve(p)
                                # Unify direction along local X
                                vec = crv.PointAtEnd - crv.PointAtStart
                                if vec * sec_plane.XAxis < 0:
                                    crv.Reverse()
                                valid_crvs.append(crv)
                                
                        if not valid_crvs: continue
                        
                        # Sort left-to-right to find true start/end points of the section
                        valid_crvs.sort(key=lambda c: sec_plane.ClosestParameter(c.PointAtStart)[1])
                        
                        bb_flat = rg.BoundingBox.Empty
                        flat_crvs = []
                        xform_to_world = rg.Transform.PlaneToPlane(cut_plane_x_dir, rg.Plane.WorldXY)
                        
                        for crv in valid_crvs:
                            SectionOutlinesX.Add(crv, current_path)
                            total_sections_x += 1
                            
                            if layout_flat:
                                flat_crv = crv.Duplicate()
                                flat_crv.Transform(xform_to_world)
                                bb_flat.Union(flat_crv.GetBoundingBox(True))
                                flat_crvs.append(flat_crv)
                        
                        # 3D Label Point generation (offset strictly horizontally along the cut plane X axis)
                        first_crv = valid_crvs[0]
                        last_crv = valid_crvs[-1]
                        pt_start_3d = first_crv.PointAtStart - cut_plane_x_dir.XAxis * 2.0
                        pt_end_3d = last_crv.PointAtEnd + cut_plane_x_dir.XAxis * 2.0
                        
                        LabelText3D.Add(sec_id, current_path)
                        LabelText3D.Add(sec_id, current_path)
                        LabelPoints3D.Add(pt_start_3d, current_path)
                        LabelPoints3D.Add(pt_end_3d, current_path)
                        
                        if layout_flat:
                            xform_move = rg.Transform.Translation(rg.Vector3d(global_bb.Min.X - bb_flat.Min.X, cursor_y_Xsecs - bb_flat.Max.Y, 0))
                            for flat_crv in flat_crvs:
                                flat_crv.Transform(xform_move)
                                FlatSectionsX.Add(flat_crv, current_path)
                                
                            # Propagate 3D label points to 2D layout map
                            pt_start_flat = rg.Point3d(pt_start_3d)
                            pt_end_flat = rg.Point3d(pt_end_3d)
                            pt_start_flat.Transform(xform_to_world)
                            pt_start_flat.Transform(xform_move)
                            pt_end_flat.Transform(xform_to_world)
                            pt_end_flat.Transform(xform_move)
                            
                            LabelTextFlat.Add(sec_id, current_path)
                            LabelTextFlat.Add(sec_id, current_path)
                            LabelPointsFlat.Add(pt_start_flat, current_path)
                            LabelPointsFlat.Add(pt_end_flat, current_path)
                            
                            meta = {"id": sec_id, "plane_origin": str(origin), "direction": "X_Section"}
                            SectionMetadata.Add(str(meta), current_path)
                            
                            cursor_y_Xsecs -= ((bb_flat.Max.Y - bb_flat.Min.Y) + padding)
            
            # Y Sections (Long): Run parallel to Y, Step along X, Stack leftwards (-X)
            if sec_count_y > 0 and (b_max_x - b_min_x) > 1e-5:
                target_plane_y = rg.Plane.WorldXY
                target_plane_y.Rotate(math.pi/2, rg.Vector3d.ZAxis)
                
                x_vals = [(b_min_x + b_max_x) * 0.5] if sec_count_y == 1 else [b_min_x + i * ((b_max_x - b_min_x) / (sec_count_y - 1)) for i in range(sec_count_y)]
                for i, u in enumerate(x_vals):
                    sec_id = "SecY_{:02d}".format(i + 1)
                    origin = sec_plane.PointAt(u, 0, 0)
                    cut_plane_y_dir = rg.Plane(origin, sec_plane.YAxis, sec_plane.ZAxis)
                    polys = rg.Intersect.Intersection.MeshPlane(mesh, cut_plane_y_dir)
                    
                    if polys:
                        valid_crvs = []
                        for p in polys:
                            if p.IsValid and p.Count > 1:
                                crv = rg.PolylineCurve(p)
                                # Unify direction along local Y
                                vec = crv.PointAtEnd - crv.PointAtStart
                                if vec * sec_plane.YAxis < 0:
                                    crv.Reverse()
                                valid_crvs.append(crv)
                                
                        if not valid_crvs: continue
                        
                        # Sort bottom-to-top to find true start/end points of the section
                        valid_crvs.sort(key=lambda c: sec_plane.ClosestParameter(c.PointAtStart)[2])
                        
                        bb_flat = rg.BoundingBox.Empty
                        flat_crvs = []
                        xform_to_world = rg.Transform.PlaneToPlane(cut_plane_y_dir, target_plane_y)
                        
                        for crv in valid_crvs:
                            SectionOutlinesY.Add(crv, current_path)
                            total_sections_y += 1
                            
                            if layout_flat:
                                flat_crv = crv.Duplicate()
                                flat_crv.Transform(xform_to_world)
                                bb_flat.Union(flat_crv.GetBoundingBox(True))
                                flat_crvs.append(flat_crv)
                                
                        # 3D Label Point generation
                        first_crv = valid_crvs[0]
                        last_crv = valid_crvs[-1]
                        pt_start_3d = first_crv.PointAtStart - cut_plane_y_dir.XAxis * 2.0
                        pt_end_3d = last_crv.PointAtEnd + cut_plane_y_dir.XAxis * 2.0
                        
                        LabelText3D.Add(sec_id, current_path)
                        LabelText3D.Add(sec_id, current_path)
                        LabelPoints3D.Add(pt_start_3d, current_path)
                        LabelPoints3D.Add(pt_end_3d, current_path)
                                
                        if layout_flat:
                            xform_move = rg.Transform.Translation(rg.Vector3d(cursor_x_Ysecs - bb_flat.Max.X, global_bb.Min.Y - bb_flat.Min.Y, 0))
                            for flat_crv in flat_crvs:
                                flat_crv.Transform(xform_move)
                                FlatSectionsY.Add(flat_crv, current_path)
                                
                            # Propagate 3D label points to 2D layout map
                            pt_start_flat = rg.Point3d(pt_start_3d)
                            pt_end_flat = rg.Point3d(pt_end_3d)
                            pt_start_flat.Transform(xform_to_world)
                            pt_start_flat.Transform(xform_move)
                            pt_end_flat.Transform(xform_to_world)
                            pt_end_flat.Transform(xform_move)
                            
                            LabelTextFlat.Add(sec_id, current_path)
                            LabelTextFlat.Add(sec_id, current_path)
                            LabelPointsFlat.Add(pt_start_flat, current_path)
                            LabelPointsFlat.Add(pt_end_flat, current_path)
                            
                            meta = {"id": sec_id, "plane_origin": str(origin), "direction": "Y_Section"}
                            SectionMetadata.Add(str(meta), current_path)
                            
                            cursor_x_Ysecs -= ((bb_flat.Max.X - bb_flat.Min.X) + padding)

# =============================================================================
# 4. HUD & TELEMETRY UPDATES
# =============================================================================
perf_end = time.perf_counter()
execution_ms = round((perf_end - perf_start) * 1000, 1)

if total_vertices_count > 0:
    terrain_relief = round(global_terrain_z_max - global_terrain_z_min, 2)
    mean_elevation = round(total_z_sum / total_vertices_count, 2)
else:
    terrain_relief = 0.0
    mean_elevation = 0.0

layout_status = "ON (Bi-Directional Unroll)" if layout_flat else "OFF"

hud_message = (
    "TERRAIN ANALYZER\n"
    "Time: {} ms\n"
    "---\n"
    "Area: {}\n"
    "Relief (ΔZ): {}\n"
    "Avg Elev: {}\n"
    "● Peaks: {} | ○ Valleys: {}\n"
    "≡ Sections X: {} | Y: {}\n"
    "[] XY Layout: {}"
).format(
    execution_ms, 
    round(total_terrain_area, 2),
    terrain_relief, 
    mean_elevation, 
    total_peaks_found, 
    total_valleys_found,
    total_sections_x,
    total_sections_y,
    layout_status
)

ghenv.Component.Message = hud_message