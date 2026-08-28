using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class ColorMeshTiles : GH_Component
    {
        public ColorMeshTiles()
          : base("ColorMeshTiles", "ColorMeshTiles",
              "BIM-ready Idempotent Engine. Consolidates Mega-Meshes per surface and binds them into Rhino Groups.",
              "Enzyme", "Facade")
        {
        }

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                var colors = new System.Drawing.Color[] {
                    System.Drawing.Color.FromArgb(240, 120, 120),
                    System.Drawing.Color.FromArgb(200, 120, 120),
                    System.Drawing.Color.FromArgb(250, 210, 210)
                };
                Enzyme.Utils.AutoWireHelper.WireMergeWithSwatches(this, document, 1, colors, 150, -140);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 2, System.Drawing.Color.FromArgb(255, 30, 0), 210, -50);
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 3, new string[]{"X", "Y", "Z"}, new string[]{"0", "1", "2"}, 300, -10);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 100.0, 30.0, 330, 30);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 100.0, 40.0, 330, 70);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 1.0, 0.94, 330, 110);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 7, 210, 150);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -120);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 220, -50, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 20, 180, 50);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "mesh", 220, 90);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("polylines", "P", "Tree of polylines", GH_ParamAccess.tree);
            pManager.AddColourParameter("gradient_colors", "C", "List of gradient colors", GH_ParamAccess.list);
            pManager.AddColourParameter("accent_color", "AC", "Accent color", GH_ParamAccess.item, Color.Empty);
            pManager.AddIntegerParameter("axis", "A", "Axis (0=X, 1=Y, 2=Z)", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("jitter_pct", "J", "Jitter percentage", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("accent_pct", "AP", "Accent percentage", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("inset_factor", "I", "Inset factor", GH_ParamAccess.item, 1.0);
            pManager.AddBooleanParameter("bake_trigger", "B", "Bake trigger", GH_ParamAccess.item, false);
            pManager.AddTextParameter("bake_name", "BN", "Bake name", GH_ParamAccess.item, "");
            
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("panel_mesh", "M", "Consolidated panels", GH_ParamAccess.tree);
            pManager.AddColourParameter("panel_colors", "C", "Panel colors", GH_ParamAccess.tree);
            pManager.AddTextParameter("panel_tags", "T", "Panel tags", GH_ParamAccess.tree);
            pManager.AddCurveParameter("panel_geometry", "G", "Panel geometry curves", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var t_start = System.Diagnostics.Stopwatch.StartNew();

            GH_Structure<GH_Curve> polylinesTree;
            if (!DA.GetDataTree(0, out polylinesTree)) return;
            
            List<Color> gradient_colors = new List<Color>();
            if (!DA.GetDataList(1, gradient_colors)) return;
            
            if (polylinesTree.DataCount == 0 || gradient_colors.Count == 0)
            {
                Message = "COLORED_TILES\nSTATUS: IDLE\n---\nAWAITING DATA";
                return;
            }

            Color accent_color = Color.Empty;
            DA.GetData(2, ref accent_color);
            
            int axis_idx = 2;
            DA.GetData(3, ref axis_idx);
            
            double jitter_pct = 0.0;
            DA.GetData(4, ref jitter_pct);
            double j_factor = Math.Max(0.0, Math.Min(100.0, jitter_pct)) / 100.0;
            
            double accent_pct = 0.0;
            DA.GetData(5, ref accent_pct);
            double a_factor = Math.Max(0.0, Math.Min(100.0, accent_pct)) / 100.0;
            
            double inset_factor = 1.0;
            DA.GetData(6, ref inset_factor);
            double i_factor = Math.Max(0.0, Math.Min(1.0, inset_factor));
            
            bool do_bake = false;
            DA.GetData(7, ref do_bake);
            
            string b_name = "";
            DA.GetData(8, ref b_name);
            
            Random random = new Random(42);
            int num_available_colors = gradient_colors.Count;
            
            Dictionary<Color, string> color_to_tag = new Dictionary<Color, string>();
            for (int i = 0; i < num_available_colors; i++)
            {
                if (!color_to_tag.ContainsKey(gradient_colors[i]))
                    color_to_tag[gradient_colors[i]] = $"Tile {i + 1}";
            }
            
            Dictionary<Color, int> global_color_counts = new Dictionary<Color, int>();
            foreach (var c in gradient_colors)
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

            List<double> global_keys = new List<double>();
            foreach (var branch in polylinesTree.Branches)
            {
                foreach (GH_Curve ghCrv in branch.Cast<GH_Curve>())
                {
                    if (ghCrv == null || ghCrv.Value == null) continue;
                    BoundingBox bbox = ghCrv.Value.GetBoundingBox(true);
                    double val = axis_idx == 0 ? bbox.Center.X : (axis_idx == 1 ? bbox.Center.Y : bbox.Center.Z);
                    global_keys.Add(val);
                }
            }

            if (global_keys.Count == 0)
            {
                Message = "HME\nSTATUS: EMPTY\n---\nNO VALID CURVES";
                return;
            }

            double global_min = global_keys.Min();
            double global_max = global_keys.Max();
            double coordinate_span = global_max - global_min;
            if (Math.Abs(coordinate_span) < 1e-6) coordinate_span = 1.0;

            int total_panels = 0;

            for (int i = 0; i < polylinesTree.Branches.Count; i++)
            {
                GH_Path path = polylinesTree.Paths[i];
                var branch = polylinesTree.Branches[i];
                
                out_mesh_tree.EnsurePath(path);
                out_cols_tree.EnsurePath(path);
                out_tags_tree.EnsurePath(path);
                out_geo_tree.EnsurePath(path);
                
                List<GH_Curve> branch_geometries = new List<GH_Curve>();
                
                Dictionary<Color, Mesh> local_mesh_buckets = new Dictionary<Color, Mesh>();
                foreach (var c in gradient_colors)
                {
                    if (!local_mesh_buckets.ContainsKey(c))
                        local_mesh_buckets[c] = new Mesh();
                }
                    
                if (has_accent && !local_mesh_buckets.ContainsKey(accent_color))
                {
                    local_mesh_buckets[accent_color] = new Mesh();
                }

                foreach (GH_Curve ghCrv in branch.Cast<GH_Curve>())
                {
                    if (ghCrv == null || ghCrv.Value == null) continue;
                    
                    Polyline polyline;
                    if (!ghCrv.Value.TryGetPolyline(out polyline)) continue;
                    
                    BoundingBox bbox = ghCrv.Value.GetBoundingBox(true);
                    Point3d center_pt = bbox.Center;
                    
                    if (i_factor < 1.0)
                    {
                        Transform scale_transform = Transform.Scale(center_pt, i_factor);
                        polyline.Transform(scale_transform);
                    }
                    
                    List<Point3d> pts = polyline.ToList();
                    if (pts.Count < 4) continue;
                    if (pts[0].EpsilonEquals(pts[pts.Count - 1], 1e-6))
                    {
                        pts.RemoveAt(pts.Count - 1);
                    }
                    
                    int num_vertices = pts.Count;
                    
                    double current_val = axis_idx == 0 ? center_pt.X : (axis_idx == 1 ? center_pt.Y : center_pt.Z);
                    double t_base = (current_val - global_min) / coordinate_span;
                    
                    if (j_factor > 0)
                    {
                        t_base += (random.NextDouble() * j_factor) - (j_factor * 0.5);
                    }
                    
                    t_base = Math.Max(0.0, Math.Min(0.999999, t_base));
                    int color_index = (int)(t_base * num_available_colors);
                    Color cell_color = gradient_colors[color_index];
                    
                    if (has_accent && random.NextDouble() < a_factor)
                    {
                        cell_color = accent_color;
                    }
                    
                    global_color_counts[cell_color]++;
                    
                    Mesh target_mesh = local_mesh_buckets[cell_color];
                    int v_start_idx = target_mesh.Vertices.Count;
                    
                    target_mesh.Vertices.Add(center_pt);
                    target_mesh.VertexColors.Add(cell_color);
                    
                    foreach (var p in pts)
                    {
                        target_mesh.Vertices.Add(p);
                        target_mesh.VertexColors.Add(cell_color);
                    }
                    
                    for (int j = 0; j < num_vertices; j++)
                    {
                        int next_j = (j + 1) % num_vertices;
                        target_mesh.Faces.AddFace(v_start_idx, v_start_idx + 1 + j, v_start_idx + 1 + next_j);
                    }
                    
                    branch_geometries.Add(new GH_Curve(polyline.ToPolylineCurve()));
                    total_panels++;
                }
                
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
                    out_mesh_tree.AppendRange(local_meshes, path);
                    out_cols_tree.AppendRange(local_colors, path);
                    out_tags_tree.AppendRange(local_tags, path);
                }
                if (branch_geometries.Count > 0)
                {
                    out_geo_tree.AppendRange(branch_geometries, path);
                }
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
                "COLORED MESH TILES",
                $"Time: {execution_ms:F2} ms",
                "---"
            };

            for (int i = 0; i < gradient_colors.Count; i++)
            {
                Color col = gradient_colors[i];
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

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("ColorMeshTiles.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("1f3f6c8d-3b56-4c22-b2a8-1234abcd5678"); }
        }
    }
}
