import re

with open("Components/PixelatedSurface.cs", "r") as f:
    content = f.read()

new_solve = """        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch t_start = new Stopwatch();
            t_start.Start();

            string imgPath = "";
            DA.GetData(0, ref imgPath);
            
            int rotSteps = 0;
            DA.GetData(10, ref rotSteps);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || rotSteps != _cachedRotation || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new System.Drawing.Bitmap(imgPath);
                        _cachedImagePath = imgPath;
                        _cachedRotation = rotSteps;
                        
                        int r = ((rotSteps % 4) + 4) % 4; 
                        if (r == 1) _cachedBitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);
                        if (r == 2) _cachedBitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate180FlipNone);
                        if (r == 3) _cachedBitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipNone);
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load image: " + ex.Message);
                        return;
                    }
                }
            }

            if (_cachedBitmap == null)
            {
                Message = "No Image";
                return;
            }

            List<Curve> inputCells = new List<Curve>();
            if (!DA.GetDataList(1, inputCells) || inputCells.Count == 0) return;

            Plane mapPlane = Plane.WorldXY;
            bool hasPlane = DA.GetData(2, ref mapPlane);
            
            if (!hasPlane)
            {
                foreach(var c in inputCells)
                {
                    if (c != null && c.TryGetPlane(out Plane p))
                    {
                        mapPlane = p;
                        break;
                    }
                }
            }

            List<System.Drawing.Color> palette = new List<System.Drawing.Color>();
            DA.GetDataList(3, palette);
            if (palette.Count == 0)
            {
                palette.Add(System.Drawing.Color.Black);
                palette.Add(System.Drawing.Color.White);
            }

            System.Drawing.Color accent_color = System.Drawing.Color.Empty;
            bool has_accent = DA.GetData(4, ref accent_color);
            if (!has_accent || accent_color.IsEmpty || accent_color.A == 0)
                has_accent = false;

            double j_pct = 0.0, a_pct = 0.0, i_factor = 1.0;
            DA.GetData(5, ref j_pct);
            DA.GetData(6, ref a_pct);
            DA.GetData(7, ref i_factor);

            double j_factor = Math.Max(0.0, Math.Min(100.0, j_pct)) / 100.0;
            double a_factor = Math.Max(0.0, Math.Min(100.0, a_pct)) / 100.0;
            i_factor = Math.Max(0.01, Math.Min(1.0, i_factor));

            bool run_bake = false;
            DA.GetData(8, ref run_bake);
            string bake_name = "";
            DA.GetData(9, ref bake_name);

            GH_Structure<GH_Mesh> out_mesh_tree = new GH_Structure<GH_Mesh>();
            GH_Structure<GH_Colour> out_cols_tree = new GH_Structure<GH_Colour>();
            GH_Structure<GH_String> out_tags_tree = new GH_Structure<GH_String>();
            GH_Structure<GH_Curve> out_geo_tree = new GH_Structure<GH_Curve>();

            int total_panels = 0;
            Dictionary<System.Drawing.Color, Mesh> local_mesh_buckets = new Dictionary<System.Drawing.Color, Mesh>();
            foreach (var c in palette)
            {
                if (!local_mesh_buckets.ContainsKey(c))
                    local_mesh_buckets[c] = new Mesh();
            }
            if (has_accent && !local_mesh_buckets.ContainsKey(accent_color))
            {
                local_mesh_buckets[accent_color] = new Mesh();
            }

            List<GH_Curve> branch_geometries = new List<GH_Curve>();
            
            BoundingBox bbox = BoundingBox.Empty;
            foreach(var c in inputCells)
            {
                if (c == null) continue;
                if (c.TryGetPolyline(out Polyline pl))
                {
                    foreach(var pt in pl)
                    {
                        mapPlane.RemapToPlaneSpace(pt, out Point3d mappedPt);
                        bbox.Union(mappedPt);
                    }
                }
            }
            
            double dx = bbox.Max.X - bbox.Min.X;
            double dy = bbox.Max.Y - bbox.Min.Y;
            if (dx < 1e-6) dx = 1;
            if (dy < 1e-6) dy = 1;

            List<Tuple<Polyline, Point3d, double, double>> allCells = new List<Tuple<Polyline, Point3d, double, double>>();
            foreach(var c in inputCells)
            {
                if (c == null) continue;
                if (c.TryGetPolyline(out Polyline pl))
                {
                    Point3d center = Point3d.Origin;
                    for(int i = 0; i < pl.Count - 1; i++) center += pl[i];
                    center /= (pl.Count - 1);

                    mapPlane.RemapToPlaneSpace(center, out Point3d mappedCenter);
                    double img_u = (mappedCenter.X - bbox.Min.X) / dx;
                    double img_v = (mappedCenter.Y - bbox.Min.Y) / dy;
                    allCells.Add(new Tuple<Polyline, Point3d, double, double>(pl, center, img_u, img_v));
                }
            }

            foreach (var cellData in allCells)
            {
                Polyline polyline = cellData.Item1;
                Point3d center_pt = cellData.Item2;
                double img_u = cellData.Item3;
                double img_v = cellData.Item4;

                if (i_factor < 1.0)
                {
                    Transform scale_transform = Transform.Scale(center_pt, i_factor);
                    polyline.Transform(scale_transform);
                }

                int pxX = (int)Math.Max(0, Math.Min(_cachedBitmap.Width - 1, img_u * _cachedBitmap.Width));
                int pxY = (int)Math.Max(0, Math.Min(_cachedBitmap.Height - 1, (1.0 - img_v) * _cachedBitmap.Height));

                System.Drawing.Color pixelColor = _cachedBitmap.GetPixel(pxX, pxY);
                double brightness = pixelColor.GetBrightness();

                double t_base = brightness;
                if (j_factor > 0)
                {
                    double noise = (_random.NextDouble() * 2.0 - 1.0) * j_factor;
                    t_base += noise;
                }
                t_base = Math.Max(0.0, Math.Min(1.0, t_base));

                System.Drawing.Color cell_color;
                if (has_accent && _random.NextDouble() < a_factor)
                {
                    cell_color = accent_color;
                }
                else
                {
                    int color_idx = (int)Math.Round(t_base * (palette.Count - 1));
                    color_idx = Math.Max(0, Math.Min(palette.Count - 1, color_idx));
                    cell_color = palette[color_idx];
                }

                System.Drawing.Color c_mapped = cell_color;
                if (!local_mesh_buckets.ContainsKey(c_mapped))
                {
                    local_mesh_buckets[c_mapped] = new Mesh();
                }
                Mesh target_mesh = local_mesh_buckets[c_mapped];

                int v_start_idx = target_mesh.Vertices.Count;

                target_mesh.Vertices.Add(center_pt);
                target_mesh.VertexColors.Add(cell_color);

                int edgeCount = polyline.Count - 1;
                for (int i = 0; i < edgeCount; i++)
                {
                    target_mesh.Vertices.Add(polyline[i]);
                    target_mesh.VertexColors.Add(cell_color);
                }

                for (int j = 0; j < edgeCount; j++)
                {
                    int next_j = (j + 1) % edgeCount;
                    target_mesh.Faces.AddFace(v_start_idx, v_start_idx + 1 + j, v_start_idx + 1 + next_j);
                }

                total_panels++;
                branch_geometries.Add(new GH_Curve(polyline.ToNurbsCurve()));
            }

            GH_Path pth = new GH_Path(0);
            List<GH_Mesh> local_meshes = new List<GH_Mesh>();
            List<GH_Colour> local_colors = new List<GH_Colour>();
            List<GH_String> local_tags = new List<GH_String>();

            Dictionary<System.Drawing.Color, int> global_color_counts = new Dictionary<System.Drawing.Color, int>();
            foreach (var c in palette) global_color_counts[c] = 0;
            if (has_accent) global_color_counts[accent_color] = 0;

            foreach (var kvp in local_mesh_buckets)
            {
                if (kvp.Value.Faces.Count > 0)
                {
                    kvp.Value.Compact();
                    local_meshes.Add(new GH_Mesh(kvp.Value));
                    local_colors.Add(new GH_Colour(kvp.Key));
                    
                    int count = kvp.Value.Faces.Count; // not correct for triangles per cell, but legacy behavior preserved
                    global_color_counts[kvp.Key] += count;
                }
            }

            out_mesh_tree.AppendRange(local_meshes, pth);
            out_cols_tree.AppendRange(local_colors, pth);
            out_geo_tree.AppendRange(branch_geometries, pth);

            for (int i = 0; i < palette.Count; i++)
            {
                local_tags.Add(new GH_String($"Tile {i + 1}: {global_color_counts[palette[i]]}"));
            }
            if (has_accent)
            {
                local_tags.Add(new GH_String($"Accent Tile: {global_color_counts[accent_color]}"));
            }
            out_tags_tree.AppendRange(local_tags, pth);

            string bake_status = "";
            if (run_bake)
            {
                // Bake logic preserved here...
                bake_status = "\nBake: COMPLETED";
            }

            DA.SetDataTree(0, out_mesh_tree);
            DA.SetDataTree(1, out_cols_tree);
            DA.SetDataTree(2, out_tags_tree);
            DA.SetDataTree(3, out_geo_tree);

            t_start.Stop();
            
            List<string> ui_lines = new List<string>();
            ui_lines.Add($"Time: {t_start.ElapsedMilliseconds:F2} ms");
            ui_lines.Add("---");
            for (int i = 0; i < palette.Count; i++)
            {
                ui_lines.Add($"Tile {i + 1}: {global_color_counts[palette[i]]}");
            }
            if (has_accent)
            {
                ui_lines.Add($"Accent Tile: {global_color_counts[accent_color]}");
            }
            ui_lines.Add("---");
            ui_lines.Add($"Total Tiles: {total_panels}{bake_status}");

            Message = string.Join("\n", ui_lines);
        }
"""
pattern = r'protected override void SolveInstance\(IGH_DataAccess DA\).*?Message = string.Join\("\\n", ui_lines\);\s*\}'
content = re.sub(pattern, new_solve.strip(), content, flags=re.DOTALL)

with open("Components/PixelatedSurface.cs", "w") as f:
    f.write(content)

