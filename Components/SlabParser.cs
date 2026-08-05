using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Newtonsoft.Json.Linq;

namespace Enzyme.Components
{
    public class SlabParserComponent : GH_Component
    {
        public SlabParserComponent()
          : base("Slab JSON Parser", "Slab_Parser",
              "Parses Slab JSON data.",
              "Enzyme", "Masterplan")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Slab_JSON", "Slab_JSON", "JSON string for slabs", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Filter_Building", "Filter_Building", "Building filters", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Filter_Tower", "Filter_Tower", "Tower filters", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddIntegerParameter("Filter_Level", "Filter_Level", "Level filters", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("ExactMatch", "ExactMatch", "Use exact matching", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("SlabBounds", "SlabBounds", "Slab boundary curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Areas", "Areas", "Slab areas", GH_ParamAccess.tree);
            pManager.AddTextParameter("Labels", "Labels", "Slab labels", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();
            
            string jsonIn = "";
            DA.GetData(0, ref jsonIn);

            List<string> rawBuilding = new List<string>();
            DA.GetDataList(1, rawBuilding);
            List<string> fBldg = rawBuilding.Where(b => !string.IsNullOrEmpty(b)).ToList();

            List<string> rawTower = new List<string>();
            DA.GetDataList(2, rawTower);
            List<string> fTower = rawTower.Where(t => !string.IsNullOrEmpty(t)).ToList();

            List<int> fLevel = new List<int>();
            DA.GetDataList(3, fLevel);

            bool exactToggle = false;
            DA.GetData(4, ref exactToggle);

            GH_Structure<GH_Curve> outBounds = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Number> outAreas = new GH_Structure<GH_Number>();
            GH_Structure<GH_String> outLabels = new GH_Structure<GH_String>();

            if (string.IsNullOrWhiteSpace(jsonIn))
            {
                this.Message = this.NickName + "\nTime: 0 ms\n---\nAwaiting Data";
                DA.SetDataTree(0, outBounds);
                DA.SetDataTree(1, outAreas);
                DA.SetDataTree(2, outLabels);
                return;
            }

            try
            {
                JObject data = JObject.Parse(jsonIn);
                int bldgIndex = 0;
                int matchCount = 0;

                foreach (var bldg in data)
                {
                    string bldgName = bldg.Key;
                    if (!IsMatch(bldgName, fBldg, exactToggle)) continue;

                    JArray slabs = bldg.Value as JArray;
                    if (slabs == null) continue;

                    int slabIndex = 0;
                    foreach (JToken slabToken in slabs)
                    {
                        JObject slab = slabToken as JObject;
                        if (slab == null) continue;

                        int lvl = slab["floor_index"]?.Value<int>() ?? -1;
                        string towerId = slab["tower_id"]?.Value<string>() ?? "Unknown";

                        if (fLevel.Count > 0 && !fLevel.Contains(lvl)) 
                        {
                            slabIndex++;
                            continue;
                        }
                        if (!IsMatch(towerId, fTower, exactToggle))
                        {
                            slabIndex++;
                            continue;
                        }

                        GH_Path path = new GH_Path(bldgIndex, slabIndex);
                        string label = $"{bldgName} | {towerId} - Lvl {lvl}";
                        
                        double area = slab["area"]?.Value<double>() ?? 0.0;
                        outAreas.Append(new GH_Number(area), path);
                        outLabels.Append(new GH_String(label), path);

                        double trueZ = slab["true_z"]?.Value<double>() ?? 0.0;
                        Transform zTranslation = Transform.Translation(0, 0, trueZ);

                        JArray boundaries = slab["boundary"] as JArray;
                        if (boundaries != null)
                        {
                            foreach (JToken crvData in boundaries)
                            {
                                Curve c = DeserializeCurve(crvData as JArray);
                                if (c != null)
                                {
                                    c.Transform(zTranslation);
                                    outBounds.Append(new GH_Curve(c), path);
                                }
                            }
                        }

                        matchCount++;
                        slabIndex++;
                    }
                    bldgIndex++;
                }

                sw.Stop();
                string searchMode = exactToggle ? "Exact" : "Flexible";
                this.Message = $"{this.NickName}\nTime: {sw.ElapsedMilliseconds} ms\n---\nReturned: {matchCount}\nMode: {searchMode}";
                
                DA.SetDataTree(0, outBounds);
                DA.SetDataTree(1, outAreas);
                DA.SetDataTree(2, outLabels);
            }
            catch (Exception ex)
            {
                this.Message = "JSON Parse Error:\n" + ex.Message;
                DA.SetDataTree(0, outBounds);
                DA.SetDataTree(1, outAreas);
                DA.SetDataTree(2, outLabels);
            }
        }

        private bool IsMatch(string targetName, List<string> filterList, bool exactMode)
        {
            if (filterList == null || filterList.Count == 0) return true;
            if (string.IsNullOrEmpty(targetName)) return false;

            string target = targetName.Trim().ToUpper();

            foreach (string f in filterList)
            {
                string pattern = f.Trim().ToUpper();
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

            foreach (JToken segToken in segmentsData)
            {
                JObject seg = segToken as JObject;
                if (seg == null) continue;

                string stype = seg["type"]?.Value<string>();
                if (stype == "Line")
                {
                    Point3d start = ParsePoint(seg["start"] as JArray);
                    Point3d end = ParsePoint(seg["end"] as JArray);
                    crvs.Add(new LineCurve(start, end));
                }
                else if (stype == "Arc")
                {
                    Point3d start = ParsePoint(seg["start"] as JArray);
                    Point3d mid = ParsePoint(seg["mid"] as JArray);
                    Point3d end = ParsePoint(seg["end"] as JArray);
                    crvs.Add(new ArcCurve(new Arc(start, mid, end)));
                }
                else if (stype == "Polyline")
                {
                    JArray pointsArray = seg["points"] as JArray;
                    if (pointsArray != null)
                    {
                        List<Point3d> pts = new List<Point3d>();
                        foreach (JToken ptToken in pointsArray)
                        {
                            pts.Add(ParsePoint(ptToken as JArray));
                        }
                        if (pts.Count > 0)
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

        private Point3d ParsePoint(JArray arr)
        {
            if (arr == null || arr.Count < 3) return Point3d.Origin;
            return new Point3d(
                arr[0].Value<double>(),
                arr[1].Value<double>(),
                arr[2].Value<double>()
            );
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Slab_Parser.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("11111111-2222-3333-4444-555555555556"); }
        }
    }
}
