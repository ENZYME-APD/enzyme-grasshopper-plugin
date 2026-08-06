"""
COLORED MESH TILES: Branch-aware idempotent production engine.
Consolidates Mega-Meshes per surface, tracks discrete materials, 
and binds surfaces together into selectable Rhino Groups.
Runtime: Rhino 8 Python 3 (CPython)
"""

import Rhino
import Rhino.Geometry as rg
import System.Drawing as sd
import Grasshopper
import random
import time

# --- Telemetry Clock Start ---
t_start = time.perf_counter()

# --- Metadata Initialization ---
ghenv.Component.Name = "ColorMeshTiles"
ghenv.Component.NickName = "ColorMeshTiles"
ghenv.Component.Description = "BIM-ready Idempotent Engine. Consolidates Mega-Meshes per surface and binds them into Rhino Groups."

# Pre-allocate output tree structures safely
panel_mesh = Grasshopper.DataTree[object]()
panel_colors = Grasshopper.DataTree[object]()
panel_tags = Grasshopper.DataTree[object]()
panel_geometry = Grasshopper.DataTree[object]()

# --- Graceful Validation Matrix ---
inputs_valid = True
if 'polylines' not in globals() or polylines is None or polylines.BranchCount == 0:
    inputs_valid = False
if 'gradient_colors' not in globals() or not gradient_colors:
    inputs_valid = False

if not inputs_valid:
    ghenv.Component.Message = "COLORED_TILES\nSTATUS: IDLE\n---\nAWAITING DATA"
else:
    random.seed(42)
    
    axis_idx = axis if 'axis' in globals() and axis is not None else 2
    j_factor = max(0.0, min(100.0, jitter_pct)) / 100.0 if 'jitter_pct' in globals() and jitter_pct is not None else 0.0
    a_factor = max(0.0, min(100.0, accent_pct)) / 100.0 if 'accent_pct' in globals() and accent_pct is not None else 0.0
    i_factor = max(0.0, min(1.0, inset_factor)) if 'inset_factor' in globals() and inset_factor is not None else 1.0
    
    do_bake = bool(bake_trigger) if 'bake_trigger' in globals() and bake_trigger is not None else False
    b_name = str(bake_name) if 'bake_name' in globals() and bake_name is not None else ""
    
    num_available_colors = len(gradient_colors)
    color_to_tag = {gradient_colors[i]: f"Tile {i + 1}" for i in range(num_available_colors)}
    
    # Global tracking for UI Telemetry
    global_color_counts = {c: 0 for c in gradient_colors}
    has_accent = 'accent_color' in globals() and accent_color is not None and a_factor > 0
    if has_accent:
        global_color_counts[accent_color] = 0
        color_to_tag[accent_color] = "Accent Tile"

    # Allocation Trees
    out_mesh_tree = Grasshopper.DataTree[object]()
    out_cols_tree = Grasshopper.DataTree[object]()
    out_tags_tree = Grasshopper.DataTree[object]()
    out_geo_tree = Grasshopper.DataTree[object]()

    # ==========================================================================
    # PASS 1: GLOBAL BOUNDS SCAN
    # ==========================================================================
    global_keys = []
    for i in range(polylines.BranchCount):
        branch = polylines.Branch(i)
        for crv in branch:
            if crv is None: continue 
                
            bbox = crv.GetBoundingBox(True)
            global_keys.append(bbox.Center.X if axis_idx == 0 else (bbox.Center.Y if axis_idx == 1 else bbox.Center.Z))

    if not global_keys:
        ghenv.Component.Message = "HME\nSTATUS: EMPTY\n---\nNO VALID CURVES"
    else:
        global_min = min(global_keys)
        global_max = max(global_keys)
        coordinate_span = global_max - global_min if global_max != global_min else 1.0

        # ==========================================================================
        # PASS 2: BRANCH-AWARE CONSOLIDATION
        # ==========================================================================
        total_panels = 0

        for i in range(polylines.BranchCount):
            path = polylines.Path(i)
            branch = polylines.Branch(i)
            
            out_mesh_tree.EnsurePath(path)
            out_cols_tree.EnsurePath(path)
            out_tags_tree.EnsurePath(path)
            out_geo_tree.EnsurePath(path)
            
            branch_geometries = []
            
            # Initialize local mesh buckets strictly for this specific surface/branch
            local_mesh_buckets = {c: rg.Mesh() for c in gradient_colors}
            if has_accent:
                local_mesh_buckets[accent_color] = rg.Mesh()
            
            for crv in branch:
                if crv is None: continue
                
                success, polyline = crv.TryGetPolyline()
                if not success: continue
                
                bbox = crv.GetBoundingBox(True)
                center_pt = bbox.Center
                
                if i_factor < 1.0:
                    scale_transform = rg.Transform.Scale(center_pt, i_factor)
                    polyline.Transform(scale_transform)
                
                pts = list(polyline)
                if len(pts) < 4: continue
                if pts[0].EpsilonEquals(pts[-1], 1e-6):
                    pts.pop()
                
                num_vertices = len(pts)
                
                current_val = center_pt.X if axis_idx == 0 else (center_pt.Y if axis_idx == 1 else center_pt.Z)
                t_base = (current_val - global_min) / coordinate_span
                
                if j_factor > 0:
                    t_base += random.uniform(-j_factor * 0.5, j_factor * 0.5)
                
                t_base = max(0.0, min(0.999999, t_base))
                color_index = int(t_base * num_available_colors)
                cell_color = gradient_colors[color_index]
                
                if has_accent and random.random() < a_factor:
                    cell_color = accent_color
                
                global_color_counts[cell_color] += 1
                
                target_mesh = local_mesh_buckets[cell_color]
                v_start_idx = target_mesh.Vertices.Count
                
                target_mesh.Vertices.Add(center_pt)
                target_mesh.VertexColors.Add(cell_color)
                
                for p in pts:
                    target_mesh.Vertices.Add(p)
                    target_mesh.VertexColors.Add(cell_color)
                
                for j in range(num_vertices):
                    next_j = (j + 1) % num_vertices
                    target_mesh.Faces.AddFace(v_start_idx, v_start_idx + 1 + j, v_start_idx + 1 + next_j)
                
                branch_geometries.append(polyline.ToPolylineCurve())
                total_panels += 1
            
            # Compact and append local buckets to the Data Trees
            local_meshes = []
            local_colors = []
            local_tags = []
            
            for color, m_bucket in local_mesh_buckets.items():
                if m_bucket.Vertices.Count > 0:
                    m_bucket.Normals.ComputeNormals()
                    m_bucket.Compact()
                    local_meshes.append(m_bucket)
                    local_colors.append(color)
                    local_tags.append(color_to_tag[color])
                    
            if local_meshes:
                out_mesh_tree.AddRange(local_meshes, path)
                out_cols_tree.AddRange(local_colors, path)
                out_tags_tree.AddRange(local_tags, path)
            if branch_geometries:
                out_geo_tree.AddRange(branch_geometries, path)

        panel_mesh = out_mesh_tree
        panel_colors = out_cols_tree
        panel_tags = out_tags_tree
        panel_geometry = out_geo_tree

        # ==========================================================================
        # PASS 3: IDEMPOTENT BIM BAKING SUBROUTINE WITH GROUP BINDING
        # ==========================================================================
        bake_status = ""
        items_replaced = 0
        
        if do_bake:
            doc = Rhino.RhinoDoc.ActiveDoc
            
            # Search & Destroy Previous Iterations
            if b_name:
                existing_objs = doc.Objects.FindByUserString("ElefrontBakeName", b_name, False)
                if existing_objs:
                    for obj in existing_objs:
                        doc.Objects.Delete(obj.Id, True)
                        items_replaced += 1
            
            # Iterate through the synchronized Data Trees
            for i in range(out_mesh_tree.BranchCount):
                path = out_mesh_tree.Path(i)
                branch_meshes = out_mesh_tree.Branch(i)
                branch_colors = out_cols_tree.Branch(i)
                branch_tags = out_tags_tree.Branch(i)
                
                branch_id = str(path).strip("{}").replace(";", "_")
                
                # --- RHINO GROUP GENERATION ---
                # Safely search for an existing group or spawn a new one
                group_name = f"SurfaceGroup_{b_name}_{branch_id}" if b_name else f"SurfaceGroup_{branch_id}"
                group_idx = -1
                
                for g in doc.Groups:
                    if g and g.Name == group_name:
                        group_idx = g.Index
                        break
                        
                if group_idx < 0:
                    group_idx = doc.Groups.Add(group_name)
                
                # Assign Mega-Meshes to Layers and bind them into the surface Group
                for f_mesh, f_color, f_tag in zip(branch_meshes, branch_colors, branch_tags):
                    layer_name = f"HexFacade_{f_tag.replace(' ', '')}"
                    
                    layer_idx = doc.Layers.Find(layer_name, True)
                    if layer_idx < 0:
                        new_layer = Rhino.DocObjects.Layer()
                        new_layer.Name = layer_name
                        new_layer.Color = f_color
                        
                        new_mat = Rhino.DocObjects.Material()
                        new_mat.DiffuseColor = f_color
                        new_mat.Name = f"Mat_{layer_name}"
                        
                        mat_idx = doc.Materials.Add(new_mat)
                        new_layer.RenderMaterialIndex = mat_idx
                        layer_idx = doc.Layers.Add(new_layer)
                    
                    attr = Rhino.DocObjects.ObjectAttributes()
                    attr.LayerIndex = layer_idx
                    attr.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromLayer
                    attr.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromLayer
                    
                    # INJECT BIM METADATA
                    if b_name:
                        attr.SetUserString("ElefrontBakeName", b_name)
                    attr.SetUserString("Surface_Path", str(path))
                    attr.SetUserString("Surface_ID", branch_id)
                    attr.SetUserString("Material_Tag", f_tag)
                    
                    # BIND TO RHINO GROUP
                    attr.AddToGroup(group_idx)
                    
                    doc.Objects.AddMesh(f_mesh, attr)
                
            status_text = "BAKED" if items_replaced == 0 else f"REPLACED ({items_replaced})"
            bake_status = f"\n[ {status_text} TO RHINO ]"
            doc.Views.Redraw()

        # ==========================================================================
        # PASS 4: UI TELEMETRY
        # ==========================================================================
        t_end = time.perf_counter()
        execution_ms = (t_end - t_start) * 1000.0

        ui_lines = [
            "COLORED MESH TILES",
            f"Time: {execution_ms:.2f} ms",
            "---"
        ]
        
        for idx, col in enumerate(gradient_colors):
            count = global_color_counts.get(col, 0)
            ui_lines.append(f"Tile {idx + 1}: {count}")
            
        if has_accent and global_color_counts.get(accent_color, 0) > 0:
            ui_lines.append(f"Accent Tile: {global_color_counts[accent_color]}")
            
        ui_lines.append("---")
        ui_lines.append(f"Total Tiles: {total_panels}{bake_status}")

        ghenv.Component.Message = "\n".join(ui_lines)