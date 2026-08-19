using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Enzyme.Components
{
    public class MassParserComponent : GH_Component
    {
        public MassParserComponent()
          : base("Masses JSON Parser", "Mass_Parser",
              "Parses Masses JSON",
              "Enzyme", "Masterplan")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ix = 200, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, false, ix, -120);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 10.0, 0.0, ix, -90);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Masses_JSON", "Masses_JSON", "Masses JSON string", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Filter_Building", "Filter_Building", "Limit by Building", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Filter_Tower", "Filter_Tower", "Limit by Tower ID", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddTextParameter("Filter_Program", "Filter_Program", "Limit by Program", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("ExactMatch", "ExactMatch", "Exact Match", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Transparency", "Transparency", "0.0 (Solid) to 1.0 (Invisible)", GH_ParamAccess.item, 0.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Volumes", "Volumes", "Output Volumes", GH_ParamAccess.tree);
            pManager.AddCurveParameter("BaseBounds", "BaseBounds", "Base bounds", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Areas", "Areas", "Areas", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "Heights", "Heights", GH_ParamAccess.tree);
            pManager.AddColourParameter("Colors", "Colors", "Colors", GH_ParamAccess.tree);
            pManager.AddTextParameter("Programs", "Programs", "Programs", GH_ParamAccess.tree);
            pManager.AddTextParameter("Labels", "Labels", "Labels", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();

            string json_in = "";
            DA.GetData(0, ref json_in);

            List<string> f_bldg = new List<string>();
            DA.GetDataList(1, f_bldg);
            f_bldg = f_bldg.Where(b => !string.IsNullOrEmpty(b)).ToList();

            List<string> f_tower = new List<string>();
            DA.GetDataList(2, f_tower);
            f_tower = f_tower.Where(t => !string.IsNullOrEmpty(t)).ToList();

            List<string> f_prog = new List<string>();
            DA.GetDataList(3, f_prog);
            f_prog = f_prog.Where(p => !string.IsNullOrEmpty(p)).ToList();

            bool exact_toggle = false;
            DA.GetData(4, ref exact_toggle);

            double raw_t = 0.0;
            DA.GetData(5, ref raw_t);
            double t_val = Math.Max(0.0, Math.Min(1.0, raw_t));
            int alpha_channel = (int)((1.0 - t_val) * 255);

            GH_Structure<GH_Brep> out_volumes = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Curve> out_bounds = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Number> out_areas = new GH_Structure<GH_Number>();
            GH_Structure<GH_Number> out_heights = new GH_Structure<GH_Number>();
            GH_Structure<GH_Colour> out_colors = new GH_Structure<GH_Colour>();
            GH_Structure<GH_String> out_programs = new GH_Structure<GH_String>();
            GH_Structure<GH_String> out_labels = new GH_Structure<GH_String>();

            if (string.IsNullOrWhiteSpace(json_in))
            {
                this.Message = $"{this.NickName}\nTime: 0.0 ms\n---\nAwaiting Data";
                DA.SetDataTree(0, out_volumes);
                DA.SetDataTree(1, out_bounds);
                DA.SetDataTree(2, out_areas);
                DA.SetDataTree(3, out_heights);
                DA.SetDataTree(4, out_colors);
                DA.SetDataTree(5, out_programs);
                DA.SetDataTree(6, out_labels);
                return;
            }

            try
            {
                JObject data = JObject.Parse(json_in);
                int bldg_index = 0;
                int match_count = 0;

                foreach (var bldg_prop in data.Properties())
                {
                    string bldg_name = bldg_prop.Name;
                    if (!IsMatch(bldg_name, f_bldg, exact_toggle)) continue;

                    JArray blocks = bldg_prop.Value as JArray;
                    if (blocks == null) continue;

                    for (int block_index = 0; block_index < blocks.Count; block_index++)
                    {
                        JObject block = blocks[block_index] as JObject;
                        if (block == null) continue;

                        string prog_name = block["program"]?.ToString() ?? "Unknown";
                        string tower_id = block["tower_id"]?.ToString() ?? "Unknown";

                        if (!IsMatch(prog_name, f_prog, exact_toggle)) continue;
                        if (!IsMatch(tower_id, f_tower, exact_toggle)) continue;

                        GH_Path path = new GH_Path(bldg_index, block_index);
                        string label = $"{bldg_name} | {tower_id} | {prog_name}";

                        JArray colorArr = block["color"] as JArray;
                        int r = 200, g = 200, b = 200;
                        if (colorArr != null && colorArr.Count >= 3)
                        {
                            r = colorArr[0].ToObject<int>();
                            g = colorArr[1].ToObject<int>();
                            b = colorArr[2].ToObject<int>();
                        }
                        Color color = Color.FromArgb(alpha_channel, r, g, b);

                        double height = block["total_height"]?.ToObject<double>() ?? 0.0;

                        out_heights.Append(new GH_Number(height), path);
                        out_colors.Append(new GH_Colour(color), path);
                        out_programs.Append(new GH_String(prog_name), path);
                        out_labels.Append(new GH_String(label), path);

                        Curve c = DeserializeCurve(block["boundary"] as JArray);
                        if (c != null)
                        {
                            var amp = AreaMassProperties.Compute(c);
                            double area = amp != null ? amp.Area : 0.0;
                            out_areas.Append(new GH_Number(area), path);

                            double true_z = block["true_z"]?.ToObject<double>() ?? 0.0;
                            c.Transform(Transform.Translation(0, 0, true_z));
                            out_bounds.Append(new GH_Curve(c), path);

                            Extrusion extrusion = Extrusion.Create(c, height, true);
                            if (extrusion != null)
                            {
                                Brep b_rep = extrusion.ToBrep();
                                if (b_rep != null) out_volumes.Append(new GH_Brep(b_rep), path);
                            }
                        }
                        match_count++;
                    }
                    bldg_index++;
                }

                sw.Stop();
                string search_mode = exact_toggle ? "Exact" : "Flexible";
                this.Message = $"{this.NickName}\nTime: {sw.ElapsedMilliseconds} ms\n---\nVolumes: {match_count}\nMode: {search_mode}";

                DA.SetDataTree(0, out_volumes);
                DA.SetDataTree(1, out_bounds);
                DA.SetDataTree(2, out_areas);
                DA.SetDataTree(3, out_heights);
                DA.SetDataTree(4, out_colors);
                DA.SetDataTree(5, out_programs);
                DA.SetDataTree(6, out_labels);
            }
            catch (Exception e)
            {
                this.Message = $"JSON Parse Error:\n{e.Message}";
                DA.SetDataTree(0, out_volumes);
                DA.SetDataTree(1, out_bounds);
                DA.SetDataTree(2, out_areas);
                DA.SetDataTree(3, out_heights);
                DA.SetDataTree(4, out_colors);
                DA.SetDataTree(5, out_programs);
                DA.SetDataTree(6, out_labels);
            }
        }

        private bool IsMatch(string targetName, List<string> filterList, bool exactMode)
        {
            if (filterList == null || filterList.Count == 0) return true;
            string target = (targetName ?? "").Trim().ToUpper();

            foreach (string f in filterList)
            {
                string pattern = (f ?? "").Trim().ToUpper();
                if (exactMode)
                {
                    if (target == pattern) return true;
                }
                else
                {
                    if (pattern.Contains("*") || pattern.Contains("?"))
                    {
                        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                        if (Regex.IsMatch(target, regexPattern)) return true;
                    }
                    else if (target.Contains(pattern))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private Curve DeserializeCurve(JArray segmentsData)
        {
            if (segmentsData == null || segmentsData.Count == 0) return null;

            List<Curve> crvs = new List<Curve>();
            foreach (JObject seg in segmentsData)
            {
                string stype = seg["type"]?.ToString();
                if (stype == "Line")
                {
                    JArray start = seg["start"] as JArray;
                    JArray end = seg["end"] as JArray;
                    if (start != null && end != null)
                    {
                        crvs.Add(new LineCurve(
                            new Point3d(start[0].ToObject<double>(), start[1].ToObject<double>(), start[2].ToObject<double>()),
                            new Point3d(end[0].ToObject<double>(), end[1].ToObject<double>(), end[2].ToObject<double>())
                        ));
                    }
                }
                else if (stype == "Arc")
                {
                    JArray start = seg["start"] as JArray;
                    JArray mid = seg["mid"] as JArray;
                    JArray end = seg["end"] as JArray;
                    if (start != null && mid != null && end != null)
                    {
                        crvs.Add(new ArcCurve(new Arc(
                            new Point3d(start[0].ToObject<double>(), start[1].ToObject<double>(), start[2].ToObject<double>()),
                            new Point3d(mid[0].ToObject<double>(), mid[1].ToObject<double>(), mid[2].ToObject<double>()),
                            new Point3d(end[0].ToObject<double>(), end[1].ToObject<double>(), end[2].ToObject<double>())
                        )));
                    }
                }
                else if (stype == "Polyline")
                {
                    JArray points = seg["points"] as JArray;
                    if (points != null)
                    {
                        List<Point3d> pts = new List<Point3d>();
                        foreach (JArray pt in points)
                        {
                            pts.Add(new Point3d(pt[0].ToObject<double>(), pt[1].ToObject<double>(), pt[2].ToObject<double>()));
                        }
                        crvs.Add(new PolylineCurve(pts));
                    }
                }
            }

            if (crvs.Count == 0) return null;
            if (crvs.Count == 1) return crvs[0];

            Curve[] joined = Curve.JoinCurves(crvs, 0.01);
            if (joined != null && joined.Length > 0)
            {
                Curve crv = joined[0];
                if (!crv.IsClosed) crv.MakeClosed(0.01);
                return crv;
            }

            return null;
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return IconLoader.Load("Mass_Parser.png"); }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("11976077-fcc3-48af-b3cf-9f15db81180b"); }
        }
    }
}
