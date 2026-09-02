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
    public class RoofParserComponent : GH_Component
    {
        public RoofParserComponent()
          : base("Roof JSON Parser", "Roof_Parser", "Parses roof data from JSON", Enzyme.Utils.TabInfo.TabName, "Masterplan (Beta)")
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
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, false, 210, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 220, -45);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "curve", 220, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 4, 220, 34, 180, 22);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Roof_JSON", "Roof_JSON", "JSON string containing roof data", GH_ParamAccess.item);
            pManager.AddTextParameter("Filter_Building", "Filter_Building", "Building names to filter", GH_ParamAccess.list);
            pManager.AddTextParameter("Filter_Tower", "Filter_Tower", "Tower IDs to filter", GH_ParamAccess.list);
            pManager.AddTextParameter("Filter_Type", "Filter_Type", "Roof types to filter", GH_ParamAccess.list);
            pManager.AddTextParameter("Filter_Program", "Filter_Program", "Programs above to filter", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Filter_Level", "Filter_Level", "Levels to filter", GH_ParamAccess.list);
            pManager.AddBooleanParameter("ExactMatch", "ExactMatch", "Use exact matching", GH_ParamAccess.item, false);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("SlabBounds", "SlabBounds", "Slab bounds curves", GH_ParamAccess.tree);
            pManager.AddCurveParameter("TowerBounds", "TowerBounds", "Tower bounds curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("RoofAreas", "RoofAreas", "Roof areas", GH_ParamAccess.tree);
            pManager.AddNumberParameter("TrueZ", "TrueZ", "True Z elevation", GH_ParamAccess.tree);
            pManager.AddTextParameter("Labels", "Labels", "Roof labels", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = Stopwatch.StartNew();

            string jsonIn = string.Empty;
            if (!DA.GetData(0, ref jsonIn) || string.IsNullOrWhiteSpace(jsonIn))
            {
                this.Message = $"{this.NickName}\nTime: 0.0 ms\n---\nAwaiting Data";
                return;
            }

            List<string> filterBuilding = new List<string>();
            DA.GetDataList(1, filterBuilding);
            filterBuilding = filterBuilding.Where(s => !string.IsNullOrEmpty(s)).ToList();

            List<string> filterTower = new List<string>();
            DA.GetDataList(2, filterTower);
            filterTower = filterTower.Where(s => !string.IsNullOrEmpty(s)).ToList();

            List<string> filterType = new List<string>();
            DA.GetDataList(3, filterType);
            filterType = filterType.Where(s => !string.IsNullOrEmpty(s)).ToList();

            List<string> filterProgram = new List<string>();
            DA.GetDataList(4, filterProgram);
            filterProgram = filterProgram.Where(s => !string.IsNullOrEmpty(s)).ToList();

            List<int> filterLevel = new List<int>();
            DA.GetDataList(5, filterLevel);

            bool exactMatch = false;
            DA.GetData(6, ref exactMatch);

            var outSlabs = new GH_Structure<GH_Curve>();
            var outTowers = new GH_Structure<GH_Curve>();
            var outZ = new GH_Structure<GH_Number>();
            var outAreas = new GH_Structure<GH_Number>();
            var outLabels = new GH_Structure<GH_String>();

            int matchCount = 0;
            string searchMode = exactMatch ? "Exact" : "Flexible";

            try
            {
                JObject data = JObject.Parse(jsonIn);
                int bldgIndex = 0;

                foreach (var bldg in data)
                {
                    string bldgName = bldg.Key;
                    if (!IsMatch(bldgName, filterBuilding, exactMatch))
                    {
                        bldgIndex++;
                        continue;
                    }

                    JArray roofs = bldg.Value as JArray;
                    if (roofs != null)
                    {
                        for (int roofIndex = 0; roofIndex < roofs.Count; roofIndex++)
                        {
                            var roof = roofs[roofIndex];
                            
                            string towerId = roof["tower_id"]?.ToString() ?? "Unknown";
                            int floorIndex = roof["floor_index"] != null ? roof["floor_index"].Value<int>() : -1;
                            string roofType = roof["type"]?.ToString() ?? "";
                            
                            if (filterLevel.Count > 0 && !filterLevel.Contains(floorIndex)) continue;
                            if (!IsMatch(roofType, filterType, exactMatch)) continue;
                            if (!IsMatch(towerId, filterTower, exactMatch)) continue;
                            
                            if (filterProgram.Count > 0)
                            {
                                bool progMatch = false;
                                var progs = roof["programs_above"] as JArray;
                                if (progs != null)
                                {
                                    foreach (var p in progs)
                                    {
                                        if (IsMatch(p.ToString(), filterProgram, exactMatch))
                                        {
                                            progMatch = true;
                                            break;
                                        }
                                    }
                                }
                                if (!progMatch) continue;
                            }

                            GH_Path path = new GH_Path(bldgIndex, roofIndex);
                            outSlabs.EnsurePath(path);
                            outTowers.EnsurePath(path);
                            
                            double trueZ = roof["true_z"] != null ? roof["true_z"].Value<double>() : 0.0;
                            double roofArea = roof["roof_area"] != null ? roof["roof_area"].Value<double>() : 0.0;
                            
                            string floorIndexStr = roof["floor_index"] != null ? roof["floor_index"].ToString() : "?";
                            string typeStr = roof["type"] != null ? roof["type"].ToString() : "Roof";
                            string label = $"{bldgName} | {towerId} | {typeStr} - Lvl {floorIndexStr}";
                            
                            outZ.Append(new GH_Number(trueZ), path);
                            outAreas.Append(new GH_Number(roofArea), path);
                            outLabels.Append(new GH_String(label), path);
                            
                            var zTranslation = Transform.Translation(0, 0, trueZ);
                            
                            var slabBounds = roof["slab_bounds"] as JArray;
                            if (slabBounds != null)
                            {
                                foreach (var crvData in slabBounds)
                                {
                                    Curve c = DeserializeCurve(crvData);
                                    if (c != null)
                                    {
                                        c.Transform(zTranslation);
                                        outSlabs.Append(new GH_Curve(c), path);
                                    }
                                }
                            }
                            
                            var towerBounds = roof["tower_bounds"] as JArray;
                            if (towerBounds != null)
                            {
                                foreach (var crvData in towerBounds)
                                {
                                    Curve c = DeserializeCurve(crvData);
                                    if (c != null)
                                    {
                                        c.Transform(zTranslation);
                                        outTowers.Append(new GH_Curve(c), path);
                                    }
                                }
                            }
                            
                            matchCount++;
                        }
                    }
                    bldgIndex++;
                }

                stopwatch.Stop();
                this.Message = $"{this.NickName}\nTime: {stopwatch.ElapsedMilliseconds} ms\n---\nReturned: {matchCount}\nMode: {searchMode}";

                DA.SetDataTree(0, outSlabs);
                DA.SetDataTree(1, outTowers);
                DA.SetDataTree(2, outAreas);
                DA.SetDataTree(3, outZ);
                DA.SetDataTree(4, outLabels);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                this.Message = $"JSON Parse Error:\n{ex.Message}";
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        private bool IsMatch(string targetName, List<string> filterList, bool exactMode)
        {
            if (filterList == null || filterList.Count == 0) return true;
            string target = (targetName ?? "").Trim().ToUpper();
            
            foreach (var f in filterList)
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

        private Curve DeserializeCurve(JToken segmentsData)
        {
            if (segmentsData == null || !segmentsData.HasValues) return null;
            
            List<Curve> crvs = new List<Curve>();
            foreach (var seg in segmentsData)
            {
                string sType = seg["type"]?.ToString();
                if (sType == "Line")
                {
                    var start = seg["start"];
                    var end = seg["end"];
                    if (start != null && end != null)
                    {
                        crvs.Add(new LineCurve(
                            new Point3d((double)start[0], (double)start[1], (double)start[2]),
                            new Point3d((double)end[0], (double)end[1], (double)end[2])
                        ));
                    }
                }
                else if (sType == "Arc")
                {
                    var start = seg["start"];
                    var mid = seg["mid"];
                    var end = seg["end"];
                    if (start != null && mid != null && end != null)
                    {
                        crvs.Add(new ArcCurve(new Arc(
                            new Point3d((double)start[0], (double)start[1], (double)start[2]),
                            new Point3d((double)mid[0], (double)mid[1], (double)mid[2]),
                            new Point3d((double)end[0], (double)end[1], (double)end[2])
                        )));
                    }
                }
                else if (sType == "Polyline")
                {
                    var pts = seg["points"];
                    if (pts != null)
                    {
                        List<Point3d> points = new List<Point3d>();
                        foreach (var p in pts)
                        {
                            points.Add(new Point3d((double)p[0], (double)p[1], (double)p[2]));
                        }
                        crvs.Add(new PolylineCurve(points));
                    }
                }
            }
            
            if (crvs.Count == 0) return null;
            if (crvs.Count == 1) return crvs[0];
            
            var joined = Curve.JoinCurves(crvs, 0.01);
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
            get
            {
                return IconLoader.Load("Roof_Parser.png");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public override Guid ComponentGuid => new Guid("B415CDE1-3A2F-433A-8F8D-1A1A5B4C4472");
    }
}
