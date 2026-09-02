import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

# Try again with a tighter regex.
pattern = r"List<GH_Curve> branch_geometries = new List<GH_Curve>\(\);\s*Interval u_domain = srf\.Domain\(0\);\s*Interval v_domain = srf\.Domain\(1\);\s*for \(int u = 0; u < u_divs; u\+\+\)[\s\S]*?branch_geometries\.Add\(new GH_Curve\(polyline\.ToNurbsCurve\(\)\)\);\s*}\s*}"

new_code = """List<GH_Curve> branch_geometries = new List<GH_Curve>();

            List<Curve> inputCells = new List<Curve>();
            DA.GetDataList(12, inputCells);
            bool useCells = inputCells != null && inputCells.Count > 0;

            List<Tuple<Polyline, Point3d, double, double>> allCells = new List<Tuple<Polyline, Point3d, double, double>>();

            if (useCells) {
                BoundingBox bbox = BoundingBox.Empty;
                foreach(var c in inputCells) {
                    if (c != null) bbox.Union(c.GetBoundingBox(true));
                }
                
                double dx = bbox.Max.X - bbox.Min.X;
                double dy = bbox.Max.Y - bbox.Min.Y;
                if (dx < 1e-6) dx = 1;
                if (dy < 1e-6) dy = 1;

                foreach(var c in inputCells) {
                    if (c == null) continue;
                    if (c.TryGetPolyline(out Polyline pl)) {
                        Point3d center = Point3d.Origin;
                        for(int i = 0; i < pl.Count - 1; i++) center += pl[i];
                        center /= (pl.Count - 1);

                        double img_u = (center.X - bbox.Min.X) / dx;
                        double img_v = (center.Y - bbox.Min.Y) / dy;
                        allCells.Add(new Tuple<Polyline, Point3d, double, double>(pl, center, img_u, img_v));
                    }
                }
            } else {
                if (srf == null) return;
                Interval u_domain = srf.Domain(0);
                Interval v_domain = srf.Domain(1);

                for (int u = 0; u < u_divs; u++)
                {
                    for (int v = 0; v < v_divs; v++)
                    {
                        double norm_u0 = (double)u / u_divs;
                        double norm_u1 = (double)(u + 1) / u_divs;
                        double norm_v0 = (double)v / v_divs;
                        double norm_v1 = (double)(v + 1) / v_divs;

                        Point3d pt0 = srf.PointAt(u_domain.ParameterAt(norm_u0), v_domain.ParameterAt(norm_v0));
                        Point3d pt1 = srf.PointAt(u_domain.ParameterAt(norm_u1), v_domain.ParameterAt(norm_v0));
                        Point3d pt2 = srf.PointAt(u_domain.ParameterAt(norm_u1), v_domain.ParameterAt(norm_v1));
                        Point3d pt3 = srf.PointAt(u_domain.ParameterAt(norm_u0), v_domain.ParameterAt(norm_v1));
                        Point3d center_pt = srf.PointAt(u_domain.ParameterAt((norm_u0 + norm_u1) * 0.5), v_domain.ParameterAt((norm_v0 + norm_v1) * 0.5));

                        List<Point3d> pts = new List<Point3d>() { pt0, pt1, pt2, pt3, pt0 };
                        Polyline polyline = new Polyline(pts);

                        double img_u = (norm_u0 + norm_u1) * 0.5;
                        double img_v = (norm_v0 + norm_v1) * 0.5;

                        allCells.Add(new Tuple<Polyline, Point3d, double, double>(polyline, center_pt, img_u, img_v));
                    }
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

                Color pixelColor = _cachedBitmap.GetPixel(pxX, pxY);
                double brightness = pixelColor.GetBrightness();

                double t_base = brightness;
                if (j_factor > 0)
                {
                    t_base += (random.NextDouble() * j_factor) - (j_factor * 0.5);
                }
                t_base = Math.Max(0.0, Math.Min(0.999999, t_base));

                int color_index = (int)(t_base * num_colors);
                Color cell_color = palette[color_index];

                if (has_accent && random.NextDouble() < a_factor)
                {
                    cell_color = accent_color;
                }

                global_color_counts[cell_color]++;

                Mesh target_mesh = local_mesh_buckets[cell_color];
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
            }"""

if re.search(pattern, content):
    content = re.sub(pattern, new_code, content)
    with open('Components/PixelatedSurface.cs', 'w') as f:
        f.write(content)
    print("Success")
else:
    print("Pattern not found")

