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
            pManager.AddSurfaceParameter("Surface", "Srf", "Base surface to pixelate", GH_ParamAccess.item);
            pManager.AddIntegerParameter("U_Subdivisions", "U", "Number of tiles in U direction", GH_ParamAccess.item, 20);
            pManager.AddIntegerParameter("V_Subdivisions", "V", "Number of tiles in V direction", GH_ParamAccess.item, 20);
            pManager.AddColourParameter("Colors", "C", "List of colors mapped to brightness (dark to light)", GH_ParamAccess.list);
            pManager.AddColourParameter("Accent Color", "AC", "Accent color", GH_ParamAccess.item, Color.Empty);
            pManager.AddNumberParameter("Jitter Pct", "J", "Jitter percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Accent Pct", "AP", "Accent percentage (0-100)", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Inset Factor", "I", "Inset factor (0.0-1.0)", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("Bake", "B", "Bake trigger", GH_ParamAccess.item, false);
                        pManager.AddTextParameter("Bake Name", "BN", "Bake group/layer name", GH_ParamAccess.item, "");
            pManager.AddIntegerParameter("Rotate 90", "R90", "Rotate image by multiples of 90 degrees (1=90, 2=180, 3=270)", GH_ParamAccess.item, 0);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
                        pManager[10].Optional = true;
            pManager[11].Optional = true;
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
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "C:\\path\\to\\image.jpg", 300, -180, 150, 40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 1, 100, 20, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 1, 100, 20, 330, -60);
                
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(20, 20, 20),
                    System.Drawing.Color.FromArgb(100, 100, 100),
                    System.Drawing.Color.FromArgb(200, 200, 200),
                    System.Drawing.Color.FromArgb(250, 250, 250)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 4, colors, 150, 20);
                
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 5, System.Drawing.Color.FromArgb(255, 0, 0), 210, 80);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 100.0, 30.0, 330, 120);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 100.0, 10.0, 330, 160);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 1.0, 0.94, 330, 200);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 9, 210, 240);

                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, 0, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 70, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "mesh", 220, 140);
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
            Stopwatch t_start = new Stopwatch();
            t_start.Start();

            string imgPath = "";
            DA.GetData(0, ref imgPath);
            
            int rotSteps = 0;
            DA.GetData(11, ref rotSteps);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || rotSteps != _cachedRotation || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new Bitmap(imgPath);
                        _cachedImagePath = imgPath;
                        _cachedRotation = rotSteps;
                        
                        int r = ((rotSteps % 4) + 4) % 4; 
                        if (r == 1) _cachedBitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        if (r == 2) _cachedBitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        if (r == 3) _cachedBitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
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

            Surface srf = null;
            if (!DA.GetData(1, ref srf) || srf == null) return;

            int u_divs = 20;
            DA.GetData(2, ref u_divs);
            if (u_divs < 1) u_divs = 1;

            int v_divs = 20;
            DA.GetData(3, ref v_divs);
            if (v_divs < 1) v_divs = 1;

            List<Color> palette = new List<Color>();
            if (!DA.GetDataList(4, palette) || palette.Count == 0) return;

            Color accent_color = Color.Empty;
            DA.GetData(5, ref accent_color);

            double jitter_pct = 0.0;
            DA.GetData(6, ref jitter_pct);
            double j_factor = Math.Max(0.0, Math.Min(100.0, jitter_pct)) / 100.0;

            double accent_pct = 0.0;
            DA.GetData(7, ref accent_pct);
            double a_factor = Math.Max(0.0, Math.Min(100.0, accent_pct)) / 100.0;

            double inset_factor = 1.0;
            DA.GetData(8, ref inset_factor);
            double i_factor = Math.Max(0.0, Math.Min(1.0, inset_factor));

            bool do_bake = false;
            DA.GetData(9, ref do_bake);

            string b_name = "";
            DA.GetData(10, ref b_name);

            Random random = new Random(42);
            int num_colors = palette.Count;

            Dictionary<Color, string> color_to_tag = new Dictionary<Color, string>();
            for (int i = 0; i < num_colors; i++)
            {
                if (!color_to_tag.ContainsKey(palette[i]))
                    color_to_tag[palette[i]] = $"Tile {i + 1}";
            }

            Dictionary<Color, int> global_color_counts = new Dictionary<Color, int>();
            foreach (var c in palette)
            {
                if (!global_color_counts.ContainsKey(c))
                    global_color_counts[c] = 0;
            }

            bool has_accent = accent_color != Color.Empty && a_factor > 0;
            if (has_accent)
            {
                global_color_counts[accent_color] = 0;
                color_to_tag[accent_color] = "Accent Tile";
            }

            GH_Structure<GH_Mesh> out_mesh_tree = new GH_Structure<GH_Mesh>();
            GH_Structure<GH_Colour> out_cols_tree = new GH_Structure<GH_Colour>();
            GH_Structure<GH_String> out_tags_tree = new GH_Structure<GH_String>();
            GH_Structure<GH_Curve> out_geo_tree = new GH_Structure<GH_Curve>();

            int total_panels = 0;
            Dictionary<Color, Mesh> local_mesh_buckets = new Dictionary<Color, Mesh>();
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

                    if (i_factor < 1.0)
                    {
                        Transform scale_transform = Transform.Scale(center_pt, i_factor);
                        polyline.Transform(scale_transform);
                    }

                    double img_u = (norm_u0 + norm_u1) * 0.5;
                    double img_v = (norm_v0 + norm_v1) * 0.5;

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

                    for (int i = 0; i < 4; i++)
                    {
                        target_mesh.Vertices.Add(polyline[i]);
                        target_mesh.VertexColors.Add(cell_color);
                    }

                    for (int j = 0; j < 4; j++)
                    {
                        int next_j = (j + 1) % 4;
                        target_mesh.Faces.AddFace(v_start_idx, v_start_idx + 1 + j, v_start_idx + 1 + next_j);
                    }

                    branch_geometries.Add(new GH_Curve(polyline.ToPolylineCurve()));
                    total_panels++;
                }
            }

            GH_Path pth = new GH_Path(0);
            List<GH_Mesh> local_meshes = new List<GH_Mesh>();
            List<GH_Colour> local_colors = new List<GH_Colour>();
            List<GH_String> local_tags = new List<GH_String>();

            foreach (var kvp in local_mesh_buckets)
            {
                Color color = kvp.Key;
                Mesh m_bucket = kvp.Value;

                if (m_bucket.Vertices.Count > 0)
                {
                    m_bucket.Normals.ComputeNormals();
                    m_bucket.Compact();
                    local_meshes.Add(new GH_Mesh(m_bucket));
                    local_colors.Add(new GH_Colour(color));
                    local_tags.Add(new GH_String(color_to_tag[color]));
                }
            }

            if (local_meshes.Count > 0)
            {
                out_mesh_tree.AppendRange(local_meshes, pth);
                out_cols_tree.AppendRange(local_colors, pth);
                out_tags_tree.AppendRange(local_tags, pth);
            }
            if (branch_geometries.Count > 0)
            {
                out_geo_tree.AppendRange(branch_geometries, pth);
            }

            DA.SetDataTree(0, out_mesh_tree);
            DA.SetDataTree(1, out_cols_tree);
            DA.SetDataTree(2, out_tags_tree);
            DA.SetDataTree(3, out_geo_tree);

            string bake_status = "";
            int items_replaced = 0;

            if (do_bake)
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    if (!string.IsNullOrEmpty(b_name))
                    {
                        var existing_objs = doc.Objects.FindByUserString("ElefrontBakeName", b_name, false);
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
                        string group_name = !string.IsNullOrEmpty(b_name) ? $"SurfaceGroup_{b_name}_{branch_id}" : $"SurfaceGroup_{branch_id}";
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
                            Mesh f_mesh = branch_meshes[j].Value;
                            Color f_color = branch_colors[j].Value;
                            string f_tag = branch_tags[j].Value;

                            string layer_name = $"HexFacade_{f_tag.Replace(" ", "")}";
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
                            }

                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = layer_idx;
                            attr.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromLayer;
                            attr.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromLayer;

                            if (!string.IsNullOrEmpty(b_name))
                            {
                                attr.SetUserString("ElefrontBakeName", b_name);
                            }
                            attr.SetUserString("Surface_Path", path.ToString());
                            attr.SetUserString("Surface_ID", branch_id);
                            attr.SetUserString("Material_Tag", f_tag);

                            attr.AddToGroup(group_idx);

                            doc.Objects.AddMesh(f_mesh, attr);
                        }
                    }

                    string status_text = items_replaced == 0 ? "BAKED" : $"REPLACED ({items_replaced})";
                    bake_status = $"\n[ {status_text} TO RHINO ]";
                    doc.Views.Redraw();
                }
            }

            t_start.Stop();
            double execution_ms = t_start.Elapsed.TotalMilliseconds;

            List<string> ui_lines = new List<string>
            {
                "PIXELATED SURFACE",
                $"Time: {execution_ms:F2} ms",
                "---"
            };

            for (int i = 0; i < palette.Count; i++)
            {
                Color col = palette[i];
                int count = global_color_counts.ContainsKey(col) ? global_color_counts[col] : 0;
                ui_lines.Add($"Tile {i + 1}: {count}");
            }

            if (has_accent && global_color_counts.ContainsKey(accent_color) && global_color_counts[accent_color] > 0)
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
