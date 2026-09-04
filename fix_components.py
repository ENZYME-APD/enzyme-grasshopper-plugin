import re

def update_color_mesh_tiles():
    with open("Components/ColorMeshTiles.cs", "r") as f:
        content = f.read()
    
    # 1. Constructor name
    content = content.replace(
        'base("ColorMeshTiles", "ColorMeshTiles",',
        'base("Pixel Gradient", "PixGrad",'
    )
    
    # 2. HUD
    content = content.replace(
        'ui_lines.Add($"Total Tiles:',
        'ui_lines.Insert(0, "PIXEL GRADIENT");\n            ui_lines.Add($"Total Tiles:'
    )
    
    # 3. Bake grouping
    old_bake = """                            string layer_name = $"HexFacade_{f_tag.Replace(" ", "")}";
                            int layer_idx = doc.Layers.Find(layer_name, true);

                            if (layer_idx < 0)
                            {
                                var new_layer = new Rhino.DocObjects.Layer();
                                new_layer.Name = layer_name;
                                new_layer.Color = f_color;

                                var new_mat = new Rhino.DocObjects.Material();
                                new_mat.DiffuseColor = f_color;
                                new_mat.Name = $"Mat_{layer_name}";

                                int mat_idx = doc.Materials.Add(new_mat);
                                new_layer.RenderMaterialIndex = mat_idx;
                                layer_idx = doc.Layers.Add(new_layer);
                            }"""
                            
    new_bake = """                            string parent_name = "Tiles";
                            int parent_idx = doc.Layers.Find(parent_name, true);
                            if (parent_idx < 0)
                            {
                                var parent_layer = new Rhino.DocObjects.Layer();
                                parent_layer.Name = parent_name;
                                parent_idx = doc.Layers.Add(parent_layer);
                            }

                            string layer_name = $"HexFacade_{f_tag.Replace(" ", "")}";
                            int layer_idx = -1;
                            
                            foreach(var l in doc.Layers)
                            {
                                if (l.Name == layer_name && l.ParentLayerId == doc.Layers[parent_idx].Id)
                                {
                                    layer_idx = l.Index;
                                    break;
                                }
                            }

                            if (layer_idx < 0)
                            {
                                var new_layer = new Rhino.DocObjects.Layer();
                                new_layer.Name = layer_name;
                                new_layer.Color = f_color;
                                new_layer.ParentLayerId = doc.Layers[parent_idx].Id;

                                var new_mat = new Rhino.DocObjects.Material();
                                new_mat.DiffuseColor = f_color;
                                new_mat.Name = $"Mat_{layer_name}";

                                int mat_idx = doc.Materials.Add(new_mat);
                                new_layer.RenderMaterialIndex = mat_idx;
                                layer_idx = doc.Layers.Add(new_layer);
                            }"""
    content = content.replace(old_bake, new_bake)
    
    with open("Components/ColorMeshTiles.cs", "w") as f:
        f.write(content)


def update_pixelated_surface():
    with open("Components/PixelatedSurface.cs", "r") as f:
        content = f.read()
    
    # 1. Constructor name
    content = content.replace(
        'base("Pixelated Surface", "PixelSurf",',
        'base("Pixel Image", "PixImg",'
    )
    
    # 2. HUD
    content = content.replace(
        'ui_lines.Add($"Time:',
        'ui_lines.Add("PIXEL IMAGE");\n            ui_lines.Add($"Time:'
    )
    
    # 3. Bake Logic
    old_bake_stub = """            string bake_status = "";
            if (run_bake)
            {
                // Note: Bake logic was omitted, returning simple status
                bake_status = "\\nBake: COMPLETED";
            }"""
            
    new_bake_logic = """            string bake_status = "";
            if (run_bake)
            {
                var doc = Rhino.RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    int items_replaced = 0;
                    if (!string.IsNullOrEmpty(bake_name))
                    {
                        var existing_objs = doc.Objects.FindByUserString("ElefrontBakeName", bake_name, false);
                        if (existing_objs != null && existing_objs.Length > 0)
                        {
                            foreach (var obj in existing_objs)
                            {
                                doc.Objects.Delete(obj.Id, true);
                                items_replaced++;
                            }
                        }
                    }

                    for (int i = 0; i < out_mesh_tree.Branches.Count; i++)
                    {
                        GH_Path path = out_mesh_tree.Paths[i];
                        var branch_meshes = out_mesh_tree.Branches[i];
                        var branch_colors = out_cols_tree.Branches[i];
                        var branch_tags = out_tags_tree.Branches[i];

                        string branch_id = path.ToString().Replace("{", "").Replace("}", "").Replace(";", "_");
                        string group_name = !string.IsNullOrEmpty(bake_name) ? $"SurfaceGroup_{bake_name}_{branch_id}" : $"SurfaceGroup_{branch_id}";
                        int group_idx = -1;

                        foreach (var g in doc.Groups)
                        {
                            if (g != null && g.Name == group_name)
                            {
                                group_idx = g.Index;
                                break;
                            }
                        }

                        if (group_idx < 0)
                        {
                            group_idx = doc.Groups.Add(group_name);
                        }

                        for (int j = 0; j < branch_meshes.Count; j++)
                        {
                            Rhino.Geometry.Mesh f_mesh = branch_meshes[j].Value;
                            System.Drawing.Color f_color = branch_colors[j].Value;
                            string f_tag = branch_tags[j].Value;

                            string parent_name = "Tiles";
                            int parent_idx = doc.Layers.Find(parent_name, true);
                            if (parent_idx < 0)
                            {
                                var parent_layer = new Rhino.DocObjects.Layer();
                                parent_layer.Name = parent_name;
                                parent_idx = doc.Layers.Add(parent_layer);
                            }

                            string layer_name = $"HexFacade_{f_tag.Replace(" ", "")}";
                            int layer_idx = -1;
                            
                            foreach(var l in doc.Layers)
                            {
                                if (l.Name == layer_name && l.ParentLayerId == doc.Layers[parent_idx].Id)
                                {
                                    layer_idx = l.Index;
                                    break;
                                }
                            }

                            if (layer_idx < 0)
                            {
                                var new_layer = new Rhino.DocObjects.Layer();
                                new_layer.Name = layer_name;
                                new_layer.Color = f_color;
                                new_layer.ParentLayerId = doc.Layers[parent_idx].Id;

                                var new_mat = new Rhino.DocObjects.Material();
                                new_mat.DiffuseColor = f_color;
                                new_mat.Name = $"Mat_{layer_name}";

                                int mat_idx = doc.Materials.Add(new_mat);
                                new_layer.RenderMaterialIndex = mat_idx;
                                layer_idx = doc.Layers.Add(new_layer);
                            }

                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = layer_idx;
                            attr.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromLayer;
                            attr.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromLayer;

                            if (!string.IsNullOrEmpty(bake_name))
                            {
                                attr.SetUserString("ElefrontBakeName", bake_name);
                            }
                            attr.SetUserString("Surface_Path", path.ToString());
                            attr.SetUserString("Surface_ID", branch_id);
                            attr.SetUserString("Material_Tag", f_tag);

                            attr.AddToGroup(group_idx);
                            doc.Objects.AddMesh(f_mesh, attr);
                        }
                    }
                    string status_text = items_replaced == 0 ? "BAKED" : $"REPLACED ({items_replaced})";
                    bake_status = $"\\n[ {status_text} TO RHINO ]";
                }
            }"""
    content = content.replace(old_bake_stub, new_bake_logic)

    with open("Components/PixelatedSurface.cs", "w") as f:
        f.write(content)

def update_tile_grid():
    with open("Components/TileGridGenerator.cs", "r") as f:
        content = f.read()

    # 1. Constructor name
    content = content.replace(
        'base("Grid Pattern Generator and Trimmer", "GridPattern",',
        'base("Tile Pattern", "TilePat",'
    )
    
    # 2. HUD
    old_hud = """            string capGridType = gridType.Length > 0 ? char.ToUpper(gridType[0]) + gridType.Substring(1).ToLower() : gridType;
            Message = $"{capGridType} Grid";
            Message += $"\\n{fullCount} complete | {trimCount} trimmed";
            Message += $"\\nTime: {executionTime:F3}s";"""
            
    new_hud = """            string capGridType = gridType.Length > 0 ? char.ToUpper(gridType[0]) + gridType.Substring(1).ToLower() : gridType;
            Message = "TILE PATTERN";
            Message += $"\\nTime: {executionTime:F3}s";
            Message += "\\n---";
            Message += $"\\n{capGridType} Grid";
            Message += $"\\n{fullCount} complete | {trimCount} trimmed";"""
            
    content = content.replace(old_hud, new_hud)

    with open("Components/TileGridGenerator.cs", "w") as f:
        f.write(content)


update_color_mesh_tiles()
update_pixelated_surface()
update_tile_grid()
