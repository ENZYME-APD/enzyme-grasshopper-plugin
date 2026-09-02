using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class FacadeParserComponent : GH_Component
    {
        public FacadeParserComponent()
          : base("Facade JSON Parser", "Facade_Parser",
              "Parses Facade JSON data into Curves, Heights, and Programs.",
              Enzyme.Utils.TabInfo.TabName, "Masterplan (Beta)")
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
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 5, false, 210, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 220, -45);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "curve", 220, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 3, 220, 34, 180, 22);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Facade_JSON", "Facade_JSON", "JSON string representing facade data", GH_ParamAccess.item);
            pManager.AddTextParameter("Filter_Building", "Filter_Building", "Filter by building name", GH_ParamAccess.list);
            pManager.AddTextParameter("Filter_Tower", "Filter_Tower", "Filter by tower ID", GH_ParamAccess.list);
            pManager.AddTextParameter("Filter_Program", "Filter_Program", "Filter by program name", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Filter_Level", "Filter_Level", "Filter by level index", GH_ParamAccess.list);
            pManager.AddBooleanParameter("ExactMatch", "ExactMatch", "Require exact match for filters", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("BoundsExt", "BoundsExt", "Open Exterior Physical Lines", GH_ParamAccess.tree);
            pManager.AddCurveParameter("BoundsClosed", "BoundsClosed", "Closed Master Polygon for orientation", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "Heights", "Floor-to-floor height", GH_ParamAccess.tree);
            pManager.AddTextParameter("Programs", "Programs", "Tags/Labels", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();

            string jsonIn = null;
            if (!DA.GetData(0, ref jsonIn) || string.IsNullOrWhiteSpace(jsonIn))
            {
                this.Message = this.NickName + "\nTime: 0.0 ms\n---\nAwaiting Data";
                return;
            }

            List<string> fBldg = new List<string>();
            DA.GetDataList(1, fBldg);
            fBldg = fBldg.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            List<string> fTower = new List<string>();
            DA.GetDataList(2, fTower);
            fTower = fTower.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            List<string> fProg = new List<string>();
            DA.GetDataList(3, fProg);
            fProg = fProg.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            List<int> fLevel = new List<int>();
            DA.GetDataList(4, fLevel);

            bool exactToggle = false;
            DA.GetData(5, ref exactToggle);

            GH_Structure<GH_Curve> outBoundsExt = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Curve> outBoundsClosed = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Number> outHeights = new GH_Structure<GH_Number>();
            GH_Structure<GH_String> outPrograms = new GH_Structure<GH_String>();

            int matchCount = 0;

            try
            {
                JObject data = JObject.Parse(jsonIn);
                int bldgIndex = 0;

                foreach (var bldgKvp in data)
                {
                    string bldgName = bldgKvp.Key;
                    if (!IsMatch(bldgName, fBldg, exactToggle)) continue;
                    
                    JObject progDict = bldgKvp.Value as JObject;
                    if (progDict == null) continue;

                    int progIndex = 0;

                    foreach (var progKvp in progDict)
                    {
                        string progName = progKvp.Key;
                        if (!IsMatch(progName, fProg, exactToggle)) continue;
                        
                        GH_Path path = new GH_Path(bldgIndex, progIndex);
                        JArray floors = progKvp.Value as JArray;
                        if (floors == null) continue;

                        foreach (JObject floor in floors)
                        {
                            string towerId = floor["tower_id"]?.ToString() ?? "Unknown";
                            
                            // Treat missing floor_index as -1 matching the python code
                            int floorIndex = -1;
                            if (floor["floor_index"] != null && floor["floor_index"].Type != JTokenType.Null)
                            {
                                int.TryParse(floor["floor_index"].ToString(), out floorIndex);
                            }

                            if (fLevel.Count > 0 && !fLevel.Contains(floorIndex)) continue;
                            if (!IsMatch(towerId, fTower, exactToggle)) continue;

                            double trueZ = floor["true_z"]?.ToObject<double>() ?? 0.0;
                            Transform zOffset = Transform.Translation(0, 0, trueZ);

                            List<Curve> closedCrvs = DeserializeOpenCurves(floor["BoundsClosed"] as JArray);
                            Curve closedCrv = null;
                            if (closedCrvs.Count > 0)
                            {
                                closedCrv = closedCrvs[0];
                                if (!closedCrv.IsClosed)
                                {
                                    closedCrv.MakeClosed(0.01);
                                }
                                closedCrv.Transform(zOffset);
                            }

                            List<Curve> extCrvs = DeserializeOpenCurves(floor["BoundsExt"] as JArray);
                            
                            string floorLabel = floor["floor_index"]?.ToString() ?? "?";
                            string label = $"{bldgName} | {towerId} | {progName} - Lvl {floorLabel}";
                            double height = floor["height"]?.ToObject<double>() ?? 0.0;

                            foreach (Curve extCrv in extCrvs)
                            {
                                extCrv.Transform(zOffset);

                                outBoundsExt.Append(new GH_Curve(extCrv), path);
                                outBoundsClosed.Append(new GH_Curve(closedCrv), path);
                                outHeights.Append(new GH_Number(height), path);
                                outPrograms.Append(new GH_String(label), path);
                                matchCount++;
                            }
                        }
                        progIndex++;
                    }
                    bldgIndex++;
                }

                sw.Stop();
                string searchMode = exactToggle ? "Exact" : "Flexible";
                this.Message = this.NickName + $"\nTime: {sw.ElapsedMilliseconds} ms\n---\nReturned: {matchCount}\nMode: {searchMode}";
            }
            catch (Exception ex)
            {
                this.Message = "JSON Parse Error:\n" + ex.Message;
            }

            DA.SetDataTree(0, outBoundsExt);
            DA.SetDataTree(1, outBoundsClosed);
            DA.SetDataTree(2, outHeights);
            DA.SetDataTree(3, outPrograms);
        }

        private bool IsMatch(string targetName, List<string> filterList, bool exactMode)
        {
            if (filterList == null || filterList.Count == 0) return true;
            string target = (targetName ?? "").Trim().ToUpperInvariant();
            
            foreach (string f in filterList)
            {
                string pattern = (f ?? "").Trim().ToUpperInvariant();
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

        private List<Curve> DeserializeOpenCurves(JArray segmentsData)
        {
            if (segmentsData == null || segmentsData.Count == 0) return new List<Curve>();
            
            List<Curve> crvs = new List<Curve>();
            foreach (JObject seg in segmentsData)
            {
                string stype = seg["type"]?.ToString();
                if (stype == "Line")
                {
                    JArray start = seg["start"] as JArray;
                    JArray end = seg["end"] as JArray;
                    crvs.Add(new LineCurve(new Point3d((double)start[0], (double)start[1], (double)start[2]), 
                                           new Point3d((double)end[0], (double)end[1], (double)end[2])));
                }
                else if (stype == "Arc")
                {
                    JArray start = seg["start"] as JArray;
                    JArray mid = seg["mid"] as JArray;
                    JArray end = seg["end"] as JArray;
                    crvs.Add(new ArcCurve(new Arc(new Point3d((double)start[0], (double)start[1], (double)start[2]),
                                                  new Point3d((double)mid[0], (double)mid[1], (double)mid[2]),
                                                  new Point3d((double)end[0], (double)end[1], (double)end[2]))));
                }
                else if (stype == "Polyline")
                {
                    JArray points = seg["points"] as JArray;
                    List<Point3d> pts = new List<Point3d>();
                    foreach (JArray p in points)
                    {
                        pts.Add(new Point3d((double)p[0], (double)p[1], (double)p[2]));
                    }
                    crvs.Add(new PolylineCurve(pts));
                }
            }

            if (crvs.Count == 0) return new List<Curve>();
            if (crvs.Count == 1) return crvs;

            Curve[] joined = Curve.JoinCurves(crvs, 0.01);
            if (joined != null && joined.Length > 0)
            {
                return joined.ToList();
            }
            return crvs;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => IconLoader.Load("Facade_Parser.png");

        public override Guid ComponentGuid => new Guid("7F0429C2-82DA-4EAA-9076-E547BA93EAA4");
    }
}
