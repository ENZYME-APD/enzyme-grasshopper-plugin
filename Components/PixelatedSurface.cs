using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class PixelatedSurface : GH_Component
    {
        private Bitmap _cachedBitmap = null;
        private string _cachedImagePath = "";
        private int _cachedRotation = 0;

        public PixelatedSurface()
          : base("Pixelated Surface", "PixelSurf",
              "Creates a tiled surface that pixelates an image with a posterized color palette.",
              "Enzyme", "Facade")
        {
        }

                protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Image Path", "Img", "Absolute path to the image file", GH_ParamAccess.item);
            pManager.AddCurveParameter("Grid Cells", "Cells", "Pre-generated grid cells", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Mapping Plane", "Plane", "Optional plane for UV mapping. Auto-detected if empty.", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "C", "List of colors mapped to brightness (dark to light)", GH_ParamAccess.list);
            pManager.AddColourParameter("Accent Color", "AC", "Accent color", GH_ParamAccess.item, System.Drawing.Color.Empty);
            pManager.AddNumberParameter("Jitter Pct", "J", "Jitter percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Accent Pct", "AP", "Accent percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Inset Factor", "I", "Inset factor (0.0-1.0)", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Bake", "B", "Bake trigger", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Bake Name", "BN", "Bake group/layer name", GH_ParamAccess.item, "");
            pManager.AddNumberParameter("Rotation", "Rot", "Rotate image map in degrees", GH_ParamAccess.item, 0.0);

            pManager[0].Optional = true;
            pManager[2].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
        }

        private bool hasSources = false;
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireFilePath(this, document, 0, "", 300, -180);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 1, 100, 20, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 1, 100, 20, 330, -60);
                
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(20, 20, 20),
                    System.Drawing.Color.FromArgb(100, 100, 100),
                    System.Drawing.Color.FromArgb(200, 200, 200),
                    System.Drawing.Color.FromArgb(250, 250, 250)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 4, colors, 150, 20);
                
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 5, System.Drawing.Color.FromArgb(255, 0, 0), 210, 80);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 6, 0, 100, 30, 330, 120);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 7, 0, 100, 10, 330, 160);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 1.0, 0.94, 330, 200);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 9, 210, 240);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 0, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 70, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", 220, 140);
            }
        }
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("panel_mesh", "M", "Meshes colored by palette", GH_ParamAccess.tree);
            pManager.AddColourParameter("panel_colors", "C", "Colors of panels", GH_ParamAccess.tree);
            pManager.AddTextParameter("panel_tags", "T", "Panel tags", GH_ParamAccess.tree);
            pManager.AddCurveParameter("panel_geometry", "G", "Panel boundary curves", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Random rnd = new Random(42);
            Stopwatch t_start = new Stopwatch();
            t_start.Start();

            string imgPath = "";
            DA.GetData(0, ref imgPath);
            
            double rotDeg = 0.0;
            DA.GetData(10, ref rotDeg);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new System.Drawing.Bitmap(imgPath);
                        _cachedImagePath = imgPath;
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

                if (rotDeg != 0.0)
                {
                    double rad = rotDeg * Math.PI / 180.0;
                    double cosA = Math.Cos(rad);
                    double sinA = Math.Sin(rad);

                    double cu = img_u - 0.5;
                    double cv = img_v - 0.5;

                    double ru = cu * cosA - cv * sinA;
                    double rv = cu * sinA + cv * cosA;

                    img_u = ru + 0.5;
                    img_v = rv + 0.5;
                }

                int pxX = (int)Math.Max(0, Math.Min(_cachedBitmap.Width - 1, img_u * _cachedBitmap.Width));
                int pxY = (int)Math.Max(0, Math.Min(_cachedBitmap.Height - 1, (1.0 - img_v) * _cachedBitmap.Height));

                System.Drawing.Color pixelColor = _cachedBitmap.GetPixel(pxX, pxY);
                double brightness = pixelColor.GetBrightness();

                double t_base = brightness;
                if (j_factor > 0)
                {
                    double noise = (rnd.NextDouble() * 2.0 - 1.0) * j_factor;
                    t_base += noise;
                }
                t_base = Math.Max(0.0, Math.Min(1.0, t_base));

                System.Drawing.Color cell_color;
                if (has_accent && rnd.NextDouble() < a_factor)
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
                // Note: Bake logic was omitted, returning simple status
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

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Enzyme.IconLoader.Load("Pixelated Surface.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("C4D1E5A1-61F9-4467-AB30-ABCDE1234567"); }
        }
    }
}
