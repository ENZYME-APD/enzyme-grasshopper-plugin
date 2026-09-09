using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Enzyme;

namespace Enzyme.Components
{
    public class SlopeTerrainPlus : GH_Component
    {
        public SlopeTerrainPlus()
            : base("Terrain Slope", "TerrainSlope",
                "Ultra-fast mesh slope analyzer using raw C# sequential array processing and safe UI automation.",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("SlopeTerrainPlus.png");
            }
        }

        public override Guid ComponentGuid => new Guid("B2C3D4E5-F6A7-489A-0B1C-2D3E4F5A6B7C");

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Grasshopper.Kernel.GH_DocumentObject.Menu_AppendItem(menu, "Add Default Controls", (sender, e) =>
            {
                var document = OnPingDocument();
                if (document == null) return;
                
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 60.0, 26.0, 330, -80);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 1.0, 330, -40);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 3, System.Drawing.Color.FromArgb(0, 150, 255), 210, 0);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 4, System.Drawing.Color.OrangeRed, 210, 40);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 5, true, 210, 80);
            });
        }

        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "TargetMeshes", "Meshes to analyze", GH_ParamAccess.list);
            pManager.AddNumberParameter("ThresholdValue", "ThresholdValue", "Threshold for slope analysis", GH_ParamAccess.item, 30.0);
            pManager.AddIntegerParameter("ThresholdMode", "ThresholdMode", "0: Degrees, 1: Percentage, 2: Ratio", GH_ParamAccess.item, 0);
            pManager.AddColourParameter("Custom Colors", "Custom Colors", "List of colors for gradient or binary mapping", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("EnableBinaryMode", "EnableBinaryMode", "If true, snaps to binary colors (Under/Over threshold)", GH_ParamAccess.item, true);
        }

                        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("AnalyzedMeshes", "AnalyzedMeshes", "Colored Meshes", GH_ParamAccess.list);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON Legend Data", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Component information and interpretation", GH_ParamAccess.item);
        }

                protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Mesh> TargetMeshes = new List<Mesh>();
            DA.GetDataList(0, TargetMeshes);

            double t_val = 30.0;
            DA.GetData(1, ref t_val);

            int t_mode = 0;
            DA.GetData(2, ref t_mode);

            List<Color> customColors = new List<Color>();
            DA.GetDataList(3, customColors);
            if (customColors.Count == 0)
            {
                customColors.Add(Color.LightGreen);
                customColors.Add(Color.Red);
            }
            else if (customColors.Count == 1)
            {
                customColors.Add(customColors[0]);
            }
            Color c_start = customColors[0];
            Color c_end = customColors[customColors.Count - 1];

            bool is_binary = true;
            DA.GetData(4, ref is_binary);

            System.Diagnostics.Stopwatch perf_start = System.Diagnostics.Stopwatch.StartNew();

            double deg = 0, pct = 0, ratio = 0;
            double threshold_rads = 0;

            if (t_mode == 1) { 
                pct = t_val;
                threshold_rads = Math.Atan(pct / 100.0);
                deg = threshold_rads * 180.0 / Math.PI;
                ratio = 1.0 / Math.Tan(threshold_rads);
            } else if (t_mode == 2) { 
                ratio = t_val;
                threshold_rads = ratio <= 0 ? 0 : Math.Atan(1.0 / ratio);
                deg = threshold_rads * 180.0 / Math.PI;
                pct = Math.Tan(threshold_rads) * 100.0;
            } else { 
                deg = t_val;
                threshold_rads = deg * Math.PI / 180.0;
                pct = Math.Tan(threshold_rads) * 100.0;
                ratio = threshold_rads == 0 ? 0 : 1.0 / Math.Tan(threshold_rads);
            }

            List<Mesh> out_meshes = new List<Mesh>();
            List<Color> out_colors = new List<Color>();
            List<string> out_values = new List<string>();
            List<double> out_ratios = new List<double>();

            int total_meshes = 0;
            int global_over_count = 0;
            int global_total_faces = 0;

            foreach (Mesh input_mesh in TargetMeshes)
            {
                if (input_mesh == null || !input_mesh.IsValid) continue;
                total_meshes++;

                Mesh eval_mesh = input_mesh.DuplicateMesh();
                eval_mesh.Unweld(0.0, true);
                eval_mesh.FaceNormals.ComputeFaceNormals();
                
                int faceCount = eval_mesh.Faces.Count;
                int vertCount = eval_mesh.Vertices.Count;
                if (faceCount == 0) continue;

                var normals = eval_mesh.FaceNormals;
                double[] slopeAngles = new double[faceCount];
                Color[] vertexColors = new Color[vertCount];

                double minSlope = double.MaxValue;
                double maxSlope = double.MinValue;
                int over_count = 0;

                for (int i = 0; i < faceCount; i++) {
                    float nz = normals[i].Z;
                    if (nz > 1f) nz = 1f;
                    else if (nz < -1f) nz = -1f;
                    
                    double s = Math.Acos(nz);
                    slopeAngles[i] = s;

                    if (s < minSlope) minSlope = s;
                    if (s > maxSlope) maxSlope = s;
                    if (s > threshold_rads) over_count++;
                }

                double slopeDomain = maxSlope - minSlope;
                if (slopeDomain <= 0) slopeDomain = 1e-9;

                global_over_count += over_count;
                global_total_faces += faceCount;
                double percent_over = ((double)over_count / faceCount) * 100.0;

                for (int i = 0; i < faceCount; i++) {
                    MeshFace face = eval_mesh.Faces[i];
                    double slope = slopeAngles[i];
                    Color c;

                    if (is_binary) {
                        c = slope <= threshold_rads ? c_start : c_end;
                    } else {
                        double t = (slope - minSlope) / slopeDomain;
                        if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
                        
                        double scaled = t * (customColors.Count - 1);
                        int idx = (int)scaled;
                        if (idx >= customColors.Count - 1) idx = customColors.Count - 2;
                        if (idx < 0) idx = 0;
                        
                        double blend = scaled - idx;
                        Color c1 = customColors[idx];
                        Color c2 = customColors[idx + 1];
                        
                        int r = (int)(c1.R + (c2.R - c1.R) * blend);
                        int g = (int)(c1.G + (c2.G - c1.G) * blend);
                        int b = (int)(c1.B + (c2.B - c1.B) * blend);
                        c = Color.FromArgb(255, r, g, b);
                    }

                    vertexColors[face.A] = c;
                    vertexColors[face.B] = c;
                    vertexColors[face.C] = c;
                    if (face.IsQuad) vertexColors[face.D] = c;
                }

                eval_mesh.VertexColors.SetColors(vertexColors);
                out_meshes.Add(eval_mesh);
                out_ratios.Add(Math.Round(percent_over, 2));
            }

            if (is_binary) {
                out_colors.Add(c_start);
                out_colors.Add(c_end);
                out_values.Add("Under Threshold");
                out_values.Add("Over Threshold");
            } else {
                for (int i = 0; i < customColors.Count; i++) {
                    out_colors.Add(customColors[i]);
                    double p = (i / (double)(customColors.Count - 1)) * 100.0;
                    out_values.Add($"Step {p:F0}%");
                }
            }

            DA.SetDataList(0, out_meshes);

            if (out_meshes.Count > 0)
            {
                var jColors = new Newtonsoft.Json.Linq.JArray();
                foreach (var c in out_colors) jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var jLabels = new Newtonsoft.Json.Linq.JArray();
                foreach (var v in out_values) jLabels.Add(v.ToString());
                
                double avgRatio = 0;
                foreach (var r in out_ratios) avgRatio += r;
                if (out_ratios.Count > 0) avgRatio = (avgRatio / out_ratios.Count);
                
                double total_pct_over = global_total_faces > 0 ? ((double)global_over_count / global_total_faces * 100.0) : 0.0;
                
                var legendObj = new Newtonsoft.Json.Linq.JObject
                {
                    ["Type"] = is_binary ? "Discrete" : "Gradient",
                    ["Title"] = $"TERRAIN SLOPE (>{deg:F1}°)",
                    ["Colors"] = jColors,
                    ["Labels"] = jLabels
                };

                Newtonsoft.Json.Linq.JArray jmetrics = new Newtonsoft.Json.Linq.JArray();
                jmetrics.Add(new Newtonsoft.Json.Linq.JObject { ["Name"] = "Total Area Over", ["Value"] = $"{total_pct_over:F1}%" });
                jmetrics.Add(new Newtonsoft.Json.Linq.JObject { ["Name"] = "Avg Mesh Ratio", ["Value"] = $"{avgRatio:F1}%" });
                legendObj["Metrics"] = jmetrics;

                DA.SetData(1, legendObj.ToString(Newtonsoft.Json.Formatting.None));
            }

            perf_start.Stop();
            double exec_ms = perf_start.Elapsed.TotalMilliseconds;

            double final_pct_over = global_total_faces > 0 ? ((double)global_over_count / global_total_faces * 100.0) : 0.0;
            string mode_str = is_binary ? "Binary" : "Gradient";
            string conversion_str = $"{deg:F1}° | {pct:F1}% | 1:{ratio:F1}";

            string thresholdModeName = "Degrees";
            if (t_mode == 1) thresholdModeName = "Percentage";
            else if (t_mode == 2) thresholdModeName = "Ratio";

            Message = $"{this.NickName}\nTime: {exec_ms:F1} ms\n---\nMode: {thresholdModeName}\nInput: {conversion_str}\n● {mode_str} | ○ Over: {final_pct_over:F1}%";
            
            DA.SetData(2, "TERRAIN SLOPE\n\nMETHODOLOGY:\nExtracts face normals from the un-welded mesh via the cross-product of vertex edges. The Z-component of each normal (n.Z) provides the slope angle using acos(n.Z), converting to degrees or percentage. Faces are sorted and colored dynamically to identify areas exceeding maximum gradient thresholds.\n\nINTERPRETATION & IMPORTANCE:\nHighlights severity of topography and naturally draining facets. Critical for planning accessible paths, building foundations, and managing stormwater runoff without exceeding max legal grades.");
        }
    }
}
