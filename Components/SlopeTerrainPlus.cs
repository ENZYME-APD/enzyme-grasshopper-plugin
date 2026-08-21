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
            : base("Slope Terrain Plus", "SlopeMesh+",
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

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ix = 220, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 60.0, 30.0, ix, -150);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10, 0, ix, -120);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 3, System.Drawing.Color.LightGreen, ix, -90);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 4, System.Drawing.Color.Red, ix, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 5, true, ix, -30);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), ox, -100);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "TargetMeshes", "Meshes to analyze", GH_ParamAccess.list);
            pManager.AddNumberParameter("ThresholdValue", "ThresholdValue", "Threshold for slope analysis", GH_ParamAccess.item, 30.0);
            pManager.AddIntegerParameter("ThresholdMode", "ThresholdMode", "0: Degrees, 1: Percentage, 2: Ratio", GH_ParamAccess.item, 0);
            pManager.AddColourParameter("ColorStart", "ColorStart", "Color for flat terrain", GH_ParamAccess.item, Color.LightGreen);
            pManager.AddColourParameter("ColorEnd", "ColorEnd", "Color for steep terrain", GH_ParamAccess.item, Color.Red);
            pManager.AddBooleanParameter("EnableBinaryMode", "EnableBinaryMode", "If true, snaps to binary colors", GH_ParamAccess.item, true);
        }

                protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("AnalyzedMeshes", "AnalyzedMeshes", "Colored Meshes", GH_ParamAccess.list);
            pManager.AddColourParameter("LegendColors", "LegendColors", "Legend Colors", GH_ParamAccess.list);
            pManager.AddTextParameter("LegendValues", "LegendValues", "Legend Values", GH_ParamAccess.list);
            pManager.AddNumberParameter("OverThresholdRatio", "OverThresholdRatio", "Ratio of faces over threshold", GH_ParamAccess.list);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "JSON Legend Data", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            foreach (var param in Params.Input)
            {
                if (param.Name == "ThresholdMode")
                {
                    foreach (var source in param.Sources)
                    {
                        if (source is Grasshopper.Kernel.Special.GH_ValueList vl)
                        {
                            if (vl.ListItems.Count == 3 && vl.ListItems[0].Name == "Degrees")
                                continue;
                                
                            vl.ListItems.Clear();
                            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Degrees", "0"));
                            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Percentage", "1"));
                            vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Ratio (1:X)", "2"));
                            
                            var ghDoc = OnPingDocument();
                            if (ghDoc != null) {
                                ghDoc.ScheduleSolution(5, d => {
                                    vl.ExpireSolution(false);
                                });
                            }
                        }
                    }
                }
            }

            List<Mesh> TargetMeshes = new List<Mesh>();
            if (!DA.GetDataList(0, TargetMeshes)) return;

            double t_val = 30.0;
            DA.GetData(1, ref t_val);

            int t_mode = 0;
            DA.GetData(2, ref t_mode);

            Color c_start = Color.LightGreen;
            DA.GetData(3, ref c_start);

            Color c_end = Color.Red;
            DA.GetData(4, ref c_end);

            bool is_binary = true;
            DA.GetData(5, ref is_binary);

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
                        int r = (int)(c_start.R + (c_end.R - c_start.R) * t);
                        int g = (int)(c_start.G + (c_end.G - c_start.G) * t);
                        int b = (int)(c_start.B + (c_end.B - c_start.B) * t);
                        c = Color.FromArgb(r, g, b);
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
                for (int i = 0; i < 5; i++) {
                    double t = i / 4.0;
                    int r = (int)(c_start.R + (c_end.R - c_start.R) * t);
                    int g = (int)(c_start.G + (c_end.G - c_start.G) * t);
                    int b = (int)(c_start.B + (c_end.B - c_start.B) * t);
                    out_colors.Add(Color.FromArgb(r, g, b));
                    out_values.Add($"Step {(t * 100):F0}%");
                }
            }

                        DA.SetDataList(0, out_meshes);
            DA.SetDataList(1, out_colors);
            DA.SetDataList(2, out_values);
            DA.SetDataList(3, out_ratios);

            if (out_meshes.Count > 0)
            {
                var jColors = new JArray();
                foreach (var c in out_colors) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var jLabels = new JArray();
                foreach (var v in out_values) jLabels.Add(v.ToString());
                
                double avgRatio = 0;
                foreach (var r in out_ratios) avgRatio += r;
                if (out_ratios.Count > 0) avgRatio = (avgRatio / out_ratios.Count) * 100.0;
                
                var legendObj = new JObject
                {
                    ["Type"] = is_binary ? "Blocks" : "Gradient",
                    ["Title"] = $"Slope Terrain (Thresh: {deg:F1}°)",
                    ["Colors"] = jColors,
                    ["Labels"] = jLabels,
                    ["SubLabels"] = new JArray($"{avgRatio:F1}% over threshold")
                };
                DA.SetData(4, legendObj.ToString());
            }

            perf_start.Stop();
            double exec_ms = perf_start.Elapsed.TotalMilliseconds;

            double total_pct_over = global_total_faces > 0 ? ((double)global_over_count / global_total_faces * 100.0) : 0.0;
            string mode_str = is_binary ? "Binary" : "Gradient";
            string conversion_str = $"{deg:F1}° | {pct:F1}% | 1:{ratio:F1}";

            Message = $"{this.NickName}\nTime: {exec_ms:F1} ms\n---\nInput: {conversion_str}\n● {mode_str} | ○ Over: {total_pct_over:F1}%";
        }
    }
}
