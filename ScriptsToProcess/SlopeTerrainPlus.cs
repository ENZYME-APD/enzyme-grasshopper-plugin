// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Notes
    /* 
      Members:
        RhinoDoc RhinoDocument
        GH_Document GrasshopperDocument
        IGH_Component Component
        int Iteration

      Methods (Virtual & overridable):
        Print(string text)
        Print(string format, params object[] args)
        Reflect(object obj)
        Reflect(object obj, string method_name)
    */
    #endregion

    private void RunScript(
		DataTree<Mesh> TargetMeshes,
		DataTree<double> ThresholdValue,
		DataTree<int> ThresholdMode,
		DataTree<Color> ColorStart,
		DataTree<Color> ColorEnd,
		DataTree<bool> EnableBinaryMode,
		ref object Instructions_Out,
		ref object AnalyzedMeshes,
		ref object LegendColors,
		ref object LegendValues,
		ref object OverThresholdRatio)
    {

    // =============================================================================
    // 1. COMPONENT METADATA & INSTRUCTIONS
    // =============================================================================
    Component.Name = "Mesh Slope Analyzer";
    Component.NickName = "SlopeMesh";
    Component.Description = "Ultra-fast mesh slope analyzer using raw C# sequential array processing and safe UI automation.";
    
    Instructions_Out = "See source code block for component interface contract.";

    System.Diagnostics.Stopwatch perf_start = System.Diagnostics.Stopwatch.StartNew();

    // =============================================================================
    // 2. SAFE UI AUTOMATION (Deferred Value List Updater)
    // =============================================================================
    foreach (var param in Component.Params.Input)
    {
        if (param.Name == "ThresholdMode")
        {
            foreach (var source in param.Sources)
            {
                if (source is Grasshopper.Kernel.Special.GH_ValueList vl)
                {
                    // If already configured, skip to maintain performance
                    if (vl.ListItems.Count == 3 && vl.ListItems[0].Name == "Degrees")
                        continue;
                        
                    vl.ListItems.Clear();
                    vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Degrees", "0"));
                    vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Percentage", "1"));
                    vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Ratio (1:X)", "2"));
                    
                    // Safely schedule a UI update 5ms after the current geometry solve finishes
                    var ghDoc = Component.OnPingDocument();
                    if (ghDoc != null) {
                        ghDoc.ScheduleSolution(5, d => {
                            vl.ExpireSolution(false);
                        });
                    }
                }
            }
        }
    }

    // =============================================================================
    // 3. PARAMETER EXTRACTION & CONVERSION LOGIC
    // =============================================================================
    // Local extraction helper to prevent null-reference allocations
    T Extract<T>(Grasshopper.DataTree<T> tree, T fallback) {
        if (tree != null && tree.DataCount > 0) {
            foreach (Grasshopper.Kernel.Data.GH_Path p in tree.Paths) {
                var b = tree.Branch(p);
                if (b != null && b.Count > 0 && b[0] != null) return b[0];
            }
        }
        return fallback;
    }

    // Hard defaults guarantee the logic never fails even if inputs are unplugged
    double t_val = Extract(ThresholdValue, 30.0); 
    int t_mode = Extract(ThresholdMode, 0);
    System.Drawing.Color c_start = Extract(ColorStart, System.Drawing.Color.LightGreen);
    System.Drawing.Color c_end = Extract(ColorEnd, System.Drawing.Color.Red);
    bool is_binary = Extract(EnableBinaryMode, true);

    double deg = 0, pct = 0, ratio = 0;
    double threshold_rads = 0;

    // Mathematical unit standardizations
    if (t_mode == 1) { // Percentage
        pct = t_val;
        threshold_rads = Math.Atan(pct / 100.0);
        deg = threshold_rads * 180.0 / Math.PI;
        ratio = 1.0 / Math.Tan(threshold_rads);
    } else if (t_mode == 2) { // Ratio 1:X
        ratio = t_val;
        threshold_rads = ratio <= 0 ? 0 : Math.Atan(1.0 / ratio);
        deg = threshold_rads * 180.0 / Math.PI;
        pct = Math.Tan(threshold_rads) * 100.0;
    } else { // Degrees
        deg = t_val;
        threshold_rads = deg * Math.PI / 180.0;
        pct = Math.Tan(threshold_rads) * 100.0;
        ratio = threshold_rads == 0 ? 0 : 1.0 / Math.Tan(threshold_rads);
    }

    // Output Initialization
    Grasshopper.DataTree<Rhino.Geometry.Mesh> out_meshes = new Grasshopper.DataTree<Rhino.Geometry.Mesh>();
    Grasshopper.DataTree<System.Drawing.Color> out_colors = new Grasshopper.DataTree<System.Drawing.Color>();
    Grasshopper.DataTree<string> out_values = new Grasshopper.DataTree<string>();
    Grasshopper.DataTree<double> out_ratios = new Grasshopper.DataTree<double>();

    int total_meshes = 0;
    int global_over_count = 0;
    int global_total_faces = 0;

    // =============================================================================
    // 4. SEQUENTIAL CORE PROCESSING
    // =============================================================================
    if (TargetMeshes != null && TargetMeshes.DataCount > 0)
    {
        for (int p = 0; p < TargetMeshes.Paths.Count; p++)
        {
            Grasshopper.Kernel.Data.GH_Path path = TargetMeshes.Paths[p];
            var branch = TargetMeshes.Branch(path);
            
            out_meshes.EnsurePath(path);
            out_colors.EnsurePath(path);
            out_values.EnsurePath(path);
            out_ratios.EnsurePath(path);

            foreach (Rhino.Geometry.Mesh input_mesh in branch)
            {
                if (input_mesh == null || !input_mesh.IsValid) continue;
                total_meshes++;

                Rhino.Geometry.Mesh eval_mesh = input_mesh.DuplicateMesh();
                eval_mesh.Unweld(0.0, true);
                eval_mesh.FaceNormals.ComputeFaceNormals();
                
                int faceCount = eval_mesh.Faces.Count;
                int vertCount = eval_mesh.Vertices.Count;
                if (faceCount == 0) continue;

                var normals = eval_mesh.FaceNormals;
                double[] slopeAngles = new double[faceCount];
                System.Drawing.Color[] vertexColors = new System.Drawing.Color[vertCount];

                double minSlope = double.MaxValue;
                double maxSlope = double.MinValue;
                int over_count = 0;

                // Step A: Extract Slopes
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

                // Step B: Vertex Color Injection
                for (int i = 0; i < faceCount; i++) {
                    Rhino.Geometry.MeshFace face = eval_mesh.Faces[i];
                    double slope = slopeAngles[i];
                    System.Drawing.Color c;

                    if (is_binary) {
                        c = slope <= threshold_rads ? c_start : c_end;
                    } else {
                        double t = (slope - minSlope) / slopeDomain;
                        if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
                        int r = (int)(c_start.R + (c_end.R - c_start.R) * t);
                        int g = (int)(c_start.G + (c_end.G - c_start.G) * t);
                        int b = (int)(c_start.B + (c_end.B - c_start.B) * t);
                        c = System.Drawing.Color.FromArgb(r, g, b);
                    }

                    vertexColors[face.A] = c;
                    vertexColors[face.B] = c;
                    vertexColors[face.C] = c;
                    if (face.IsQuad) vertexColors[face.D] = c;
                }

                eval_mesh.VertexColors.SetColors(vertexColors);
                out_meshes.Add(eval_mesh, path);
                out_ratios.Add(Math.Round(percent_over, 2), path);
            }

            // Step C: Generate Standardized Legends
            if (is_binary) {
                out_colors.Add(c_start, path);
                out_colors.Add(c_end, path);
                out_values.Add("Under Threshold", path);
                out_values.Add("Over Threshold", path);
            } else {
                for (int i = 0; i < 5; i++) {
                    double t = i / 4.0;
                    int r = (int)(c_start.R + (c_end.R - c_start.R) * t);
                    int g = (int)(c_start.G + (c_end.G - c_start.G) * t);
                    int b = (int)(c_start.B + (c_end.B - c_start.B) * t);
                    out_colors.Add(System.Drawing.Color.FromArgb(r, g, b), path);
                    out_values.Add($"Step {(t * 100):F0}%", path);
                }
            }
        }
    }

    AnalyzedMeshes = out_meshes;
    LegendColors = out_colors;
    LegendValues = out_values;
    OverThresholdRatio = out_ratios;

    // =============================================================================
    // 4. HUD & TELEMETRY UPDATES
    // =============================================================================
    perf_start.Stop();
    double exec_ms = perf_start.Elapsed.TotalMilliseconds;

    double total_pct_over = global_total_faces > 0 ? ((double)global_over_count / global_total_faces * 100.0) : 0.0;
    string mode_str = is_binary ? "Binary" : "Gradient";
    string conversion_str = $"{deg:F1}° | {pct:F1}% | 1:{ratio:F1}";

    Component.Message = 
        $"SLOPE MESH\n" +
        $"Time: {exec_ms:F1} ms\n" +
        $"---\n" +
        $"Input: {conversion_str}\n" +
        $"● {mode_str} | ○ Over: {total_pct_over:F1}%";


    }
}
