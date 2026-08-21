using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;

namespace Enzyme.Components
{
    public class RailParserComponent : GH_Component
    {
        public RailParserComponent()
          : base("Railing JSON Parser", "Rail_Parser",
              "Parses railing curves from JSON.",
              "Enzyme", "Masterplan (Beta)")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, false, 80, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 150, -15);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 1, 70, 4, 160, 22);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Railings_JSON", "Railings_JSON", "JSON string", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager.AddTextParameter("Filter_Building", "Filter_Building", "Filter by building", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Filter_Tower", "Filter_Tower", "Filter by tower", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddIntegerParameter("Filter_Level", "Filter_Level", "Filter by level", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("ExactMatch", "ExactMatch", "Exact match mode", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("RailingCurves", "RailingCurves", "Parsed railing curves", GH_ParamAccess.tree);
            pManager.AddTextParameter("Labels", "Labels", "Railing labels", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();

            string jsonIn = string.Empty;
            if (!DA.GetData(0, ref jsonIn) || string.IsNullOrEmpty(jsonIn))
            {
                this.Message = this.NickName + "\nTime: 0.0 ms\n---\nAwaiting Data";
                return;
            }

            List<string> fBldg = new List<string>();
            DA.GetDataList(1, fBldg);

            List<string> fTower = new List<string>();
            DA.GetDataList(2, fTower);

            List<int> fLevel = new List<int>();
            DA.GetDataList(3, fLevel);

            bool exactMatch = false;
            DA.GetData(4, ref exactMatch);

            GH_Structure<GH_Curve> outCurves = new GH_Structure<GH_Curve>();
            GH_Structure<GH_String> outLabels = new GH_Structure<GH_String>();

            int matchCount = 0;

            try
            {
                JObject data = JObject.Parse(jsonIn);
                int bldgIndex = 0;

                foreach (var kvp in data)
                {
                    string bldgName = kvp.Key;
                    if (!IsMatch(bldgName, fBldg, exactMatch)) continue;

                    JArray rails = kvp.Value as JArray;
                    if (rails == null) continue;

                    int railIndex = 0;
                    foreach (JObject rail in rails.OfType<JObject>())
                    {
                        int lvl = rail["floor_index"]?.Value<int>() ?? -1;
                        string towerId = rail["tower_id"]?.Value<string>() ?? "Unknown";

                        if (fLevel.Count > 0 && !fLevel.Contains(lvl)) 
                        {
                            railIndex++;
                            continue;
                        }
                        
                        if (!IsMatch(towerId, fTower, exactMatch)) 
                        {
                            railIndex++;
                            continue;
                        }

                        GH_Path path = new GH_Path(bldgIndex, railIndex);
                        string label = $"{bldgName} | {towerId} - Lvl {lvl} Railing";
                        outLabels.Append(new GH_String(label), path);

                        double trueZ = rail["true_z"]?.Value<double>() ?? 0.0;
                        Transform zTranslation = Transform.Translation(0, 0, trueZ);

                        JArray curvesData = rail["curves"] as JArray;
                        if (curvesData != null)
                        {
                            foreach (JArray crvData in curvesData.OfType<JArray>())
                            {
                                Curve c = DeserializeCurve(crvData);
                                if (c != null)
                                {
                                    c.Transform(zTranslation);
                                    outCurves.Append(new GH_Curve(c), path);
                                }
                            }
                        }

                        matchCount++;
                        railIndex++;
                    }
                    bldgIndex++;
                }

                sw.Stop();
                string searchMode = exactMatch ? "Exact" : "Flexible";
                this.Message = $"{this.NickName}\nTime: {sw.ElapsedMilliseconds} ms\n---\nReturned: {matchCount}\nMode: {searchMode}";

                DA.SetDataTree(0, outCurves);
                DA.SetDataTree(1, outLabels);
            }
            catch (Exception ex)
            {
                this.Message = this.NickName + "\nJSON Parse Error:\n" + ex.Message;
            }
        }

        private Curve DeserializeCurve(JArray segmentsData)
        {
            if (segmentsData == null || segmentsData.Count == 0) return null;

            List<Curve> crvs = new List<Curve>();
            foreach (JObject seg in segmentsData.OfType<JObject>())
            {
                string sType = seg["type"]?.Value<string>();
                if (sType == "Line")
                {
                    JArray start = seg["start"] as JArray;
                    JArray end = seg["end"] as JArray;
                    if (start != null && end != null && start.Count >= 3 && end.Count >= 3)
                    {
                        crvs.Add(new LineCurve(new Point3d((double)start[0], (double)start[1], (double)start[2]),
                                               new Point3d((double)end[0], (double)end[1], (double)end[2])));
                    }
                }
                else if (sType == "Arc")
                {
                    JArray start = seg["start"] as JArray;
                    JArray mid = seg["mid"] as JArray;
                    JArray end = seg["end"] as JArray;
                    if (start != null && mid != null && end != null && start.Count >= 3 && mid.Count >= 3 && end.Count >= 3)
                    {
                        crvs.Add(new ArcCurve(new Arc(new Point3d((double)start[0], (double)start[1], (double)start[2]),
                                                      new Point3d((double)mid[0], (double)mid[1], (double)mid[2]),
                                                      new Point3d((double)end[0], (double)end[1], (double)end[2]))));
                    }
                }
                else if (sType == "Polyline")
                {
                    JArray points = seg["points"] as JArray;
                    if (points != null)
                    {
                        List<Point3d> pts = new List<Point3d>();
                        foreach (JArray pt in points.OfType<JArray>())
                        {
                            if (pt != null && pt.Count >= 3)
                            {
                                pts.Add(new Point3d((double)pt[0], (double)pt[1], (double)pt[2]));
                            }
                        }
                        if (pts.Count >= 2)
                        {
                            crvs.Add(new PolylineCurve(pts));
                        }
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

        private bool IsMatch(string targetName, List<string> filterList, bool exactMode)
        {
            if (filterList == null || filterList.Count == 0) return true;
            if (string.IsNullOrEmpty(targetName)) return false;

            string target = targetName.Trim().ToUpperInvariant();

            foreach (string f in filterList)
            {
                if (string.IsNullOrWhiteSpace(f)) continue;
                string pattern = f.Trim().ToUpperInvariant();

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

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Rail_Parser.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("11915904-b9c6-47b8-b714-38c3e80e2f5e"); }
        }
    }
}
