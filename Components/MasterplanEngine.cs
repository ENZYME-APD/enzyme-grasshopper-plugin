using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Enzyme.Grasshopper.Components
{
    public class MasterplanEngineComponent : GH_Component
    {
        public MasterplanEngineComponent()
          : base("JSON MP Engine V2", "MP ENGINE",
              "A high-performance topological coordinator. Evaluates spatial intersections, identifies setbacks/roofs, and broadcasts lightweight JSON architectures.",
              "Enzyme", "Masterplan")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Payload", "J", "JSON payload with buildings", GH_ParamAccess.item);
            pManager.AddTextParameter("ColorPalette", "C", "JSON color palette", GH_ParamAccess.item, "");
            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Masses_JSON", "M", "Masses JSON", GH_ParamAccess.item);
            pManager.AddTextParameter("Slab_JSON", "S", "Slab JSON", GH_ParamAccess.item);
            pManager.AddTextParameter("Roof_JSON", "R", "Roof JSON", GH_ParamAccess.item);
            pManager.AddTextParameter("Facade_JSON", "F", "Facade JSON", GH_ParamAccess.item);
            pManager.AddTextParameter("Railings_JSON", "RL", "Railings JSON", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard_JSON", "D", "Dashboard JSON", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string jsonIn = "";
            string paletteIn = "";

            if (!DA.GetData(0, ref jsonIn))
            {
                this.Message = this.NickName + "\nTime: 0.0 ms\n---\nAwaiting Payload";
                return;
            }
            DA.GetData(1, ref paletteIn);

            var parsedPalette = new Dictionary<string, List<int>>();
            if (!string.IsNullOrEmpty(paletteIn))
            {
                try
                {
                    var palObj = JObject.Parse(paletteIn);
                    foreach (var prop in palObj.Properties())
                    {
                        var arr = prop.Value as JArray;
                        if (arr != null && arr.Count >= 3)
                        {
                            parsedPalette[prop.Name.Trim()] = new List<int> { (int)arr[0], (int)arr[1], (int)arr[2] };
                        }
                    }
                }
                catch { }
            }

            var massesDict = new JObject();
            var slabsDict = new JObject();
            var roofsDict = new JObject();
            var facadeDict = new JObject();
            var railingsDict = new JObject();
            double dashTotalArea = 0.0;
            var dashPrograms = new Dictionary<string, double>();
            var dashBuildings = new JObject();

            if (string.IsNullOrEmpty(jsonIn))
            {
                this.Message = this.NickName + "\nTime: 0.0 ms\n---\nAwaiting Payload";
            }
            else
            {
                try 
                {
                    var data = JObject.Parse(jsonIn);
                    var bldgsArr = data["buildings"] as JArray;
                    if (bldgsArr != null)
                    {
                        foreach (JObject bldgData in bldgsArr)
                        {
                            var bldg = new Building(bldgData, parsedPalette, this);
                            bldg.GenerateTopology();

                            string bName = bldg.name;
                            massesDict[bName] = new JArray();
                            facadeDict[bName] = new JObject();
                            slabsDict[bName] = bldg.slab_json_data;
                            roofsDict[bName] = bldg.roof_json_data;
                            railingsDict[bName] = bldg.railing_json_data;

                            var bldgDash = new JObject(
                                new JProperty("total_area", 0.0),
                                new JProperty("programs", new JObject())
                            );
                            dashBuildings[bName] = bldgDash;

                            foreach (var block in bldg.blocks)
                            {
                                var bnd = block.base_curve != null ? SerializeExactCurve(block.base_curve) : new JArray();
                                ((JArray)massesDict[bName]).Add(new JObject(
                                    new JProperty("tower_id", block.tower_id),
                                    new JProperty("program", block.program),
                                    new JProperty("total_height", block.floors * block.floor_height),
                                    new JProperty("true_z", Math.Round(bldg.elevation + block.base_z, 3)),
                                    new JProperty("color", new JArray(block.color)),
                                    new JProperty("boundary", bnd)
                                ));

                                double bArea = block.areas.Sum();
                                dashTotalArea += bArea;

                                if (!dashPrograms.ContainsKey(block.program)) dashPrograms[block.program] = 0.0;
                                dashPrograms[block.program] += bArea;

                                bldgDash["total_area"] = (double)bldgDash["total_area"] + bArea;
                                var progObj = bldgDash["programs"] as JObject;
                                if (progObj[block.program] == null) progObj[block.program] = 0.0;
                                progObj[block.program] = (double)progObj[block.program] + bArea;

                                var facadeBldg = facadeDict[bName] as JObject;
                                if (facadeBldg[block.program] == null) facadeBldg[block.program] = new JArray();

                                if (block.base_curve != null)
                                {
                                    for (int i = 0; i < block.floors; i++)
                                    {
                                        double relZ = Math.Round(block.base_z + (i * block.floor_height), 3);
                                        double floorTrueZ = Math.Round(bldg.elevation + relZ, 3);

                                        var masterSlabs = bldg.master_slabs.ContainsKey(relZ) ? bldg.master_slabs[relZ] : new List<Curve>();
                                        var extCrvs = GetExteriorSegments(block.base_curve, masterSlabs);

                                        var serializedExt = new JArray();
                                        foreach (var c in extCrvs)
                                        {
                                            foreach (var segObj in SerializeExactCurve(c)) serializedExt.Add(segObj);
                                        }

                                        ((JArray)facadeBldg[block.program]).Add(new JObject(
                                            new JProperty("tower_id", block.tower_id),
                                            new JProperty("floor_index", block.floor_indices[i]),
                                            new JProperty("true_z", floorTrueZ),
                                            new JProperty("height", block.floor_height),
                                            new JProperty("BoundsClosed", SerializeExactCurve(block.base_curve)),
                                            new JProperty("BoundsExt", serializedExt)
                                        ));
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }

                sw.Stop();
                double execTime = sw.Elapsed.TotalMilliseconds;
                List<string> msgLines = new List<string> {
                    this.NickName,
                    $"Time: {execTime:F1} ms",
                    "---",
                    $"Gross Area: {dashTotalArea:N1} SQM"
                };
                foreach (var kvp in dashPrograms)
                {
                    msgLines.Add($"  • {kvp.Key}: {kvp.Value:N1} SQM");
                }
                this.Message = string.Join("\n", msgLines);
            }

            var dashProgramsObj = new JObject();
            foreach (var kvp in dashPrograms) dashProgramsObj[kvp.Key] = kvp.Value;

            var dashboardJson = new JObject(
                new JProperty("total_area", string.IsNullOrEmpty(jsonIn) ? 0.0 : dashTotalArea),
                new JProperty("programs", string.IsNullOrEmpty(jsonIn) ? new JObject() : dashProgramsObj),
                new JProperty("buildings", string.IsNullOrEmpty(jsonIn) ? new JObject() : dashBuildings)
            );

            DA.SetData(0, massesDict.ToString(Formatting.Indented));
            DA.SetData(1, slabsDict.ToString(Formatting.Indented));
            DA.SetData(2, roofsDict.ToString(Formatting.Indented));
            DA.SetData(3, facadeDict.ToString(Formatting.Indented));
            DA.SetData(4, railingsDict.ToString(Formatting.Indented));
            DA.SetData(5, dashboardJson.ToString(Formatting.Indented));
        }

        protected override Bitmap Icon
        {
            get
            {
                try {
                    return IconLoader.Load("MP ENGINE V2.png");
                } catch {
                    return null;
                }
            }
        }

        public override Guid ComponentGuid => new Guid("A3F0E3E1-2C5E-49F1-8B35-C4F4A1439F58");

        // ==============================================================================
        // ENGINE HELPER FUNCTIONS
        // ==============================================================================
        private List<Curve> SafeBooleanOp(IEnumerable<Curve> curvesA, IEnumerable<Curve> curvesB, string opType = "diff", double tolerance = 0.001)
        {
            if (curvesA == null || !curvesA.Any()) return new List<Curve>();
            if ((curvesB == null || !curvesB.Any()) && opType == "diff") return curvesA.Select(c => c.DuplicateCurve()).ToList();
            if ((curvesB == null || !curvesB.Any()) && opType == "int") return new List<Curve>();

            List<Curve> resultCurves = new List<Curve>();
            if (opType == "diff")
            {
                var listB = new List<Curve>();
                foreach (var c in curvesB)
                {
                    if (!c.IsClosed) c.MakeClosed(tolerance);
                    listB.Add(c);
                }
                foreach (var crvA in curvesA)
                {
                    if (!crvA.IsClosed) crvA.MakeClosed(tolerance);
                    var res = Curve.CreateBooleanDifference(crvA, listB, tolerance);
                    if (res != null && res.Length > 0) resultCurves.AddRange(res);
                    else
                    {
                        var res2 = Curve.CreateBooleanDifference(crvA, listB, 0.05);
                        if (res2 != null && res2.Length > 0) resultCurves.AddRange(res2);
                        else resultCurves.Add(crvA.DuplicateCurve());
                    }
                }
            }
            else
            {
                foreach (var crvA in curvesA)
                {
                    if (!crvA.IsClosed) crvA.MakeClosed(tolerance);
                    foreach (var crvB in curvesB)
                    {
                        if (!crvB.IsClosed) crvB.MakeClosed(tolerance);
                        var res = Curve.CreateBooleanIntersection(crvA, crvB, tolerance);
                        if (res != null && res.Length > 0) resultCurves.AddRange(res);
                        else
                        {
                            var res2 = Curve.CreateBooleanIntersection(crvA, crvB, 0.05);
                            if (res2 != null && res2.Length > 0) resultCurves.AddRange(res2);
                        }
                    }
                }
            }
            return resultCurves;
        }

        private List<Curve> GetNakedRailings(List<Curve> exposedRoofCrvs, List<Curve> towerCrvs, double tolerance = 0.05)
        {
            if (towerCrvs == null || !towerCrvs.Any()) return exposedRoofCrvs.Select(c => c.DuplicateCurve()).ToList();
            List<Curve> nakedSegments = new List<Curve>();
            foreach (var crv in exposedRoofCrvs)
            {
                var segments = crv.DuplicateSegments();
                if (segments == null || segments.Length == 0) segments = new Curve[] { crv };
                foreach (var seg in segments)
                {
                    var midPt = seg.PointAtNormalizedLength(0.5);
                    bool isTouchingWall = false;
                    foreach (var tCrv in towerCrvs)
                    {
                        double t;
                        if (tCrv.ClosestPoint(midPt, out t))
                        {
                            if (tCrv.PointAt(t).DistanceTo(midPt) <= tolerance)
                            {
                                isTouchingWall = true;
                                break;
                            }
                        }
                    }
                    if (!isTouchingWall) nakedSegments.Add(seg);
                }
            }
            if (nakedSegments.Count > 0)
            {
                var joined = Curve.JoinCurves(nakedSegments, 0.01);
                return joined != null && joined.Length > 0 ? joined.ToList() : nakedSegments;
            }
            return new List<Curve>();
        }

        private List<Curve> GetExteriorSegments(Curve crv, List<Curve> masterCrvs, double tolerance = 0.05)
        {
            if (crv == null || masterCrvs == null || masterCrvs.Count == 0) return new List<Curve>();
            var segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0) segments = new Curve[] { crv };
            List<Curve> extSegments = new List<Curve>();
            foreach (var seg in segments)
            {
                var midPt = seg.PointAtNormalizedLength(0.5);
                bool isExterior = false;
                foreach (var mCrv in masterCrvs)
                {
                    double t;
                    if (mCrv.ClosestPoint(midPt, out t))
                    {
                        if (mCrv.PointAt(t).DistanceTo(midPt) <= tolerance)
                        {
                            isExterior = true;
                            break;
                        }
                    }
                }
                if (isExterior) extSegments.Add(seg);
            }
            if (extSegments.Count > 0)
            {
                var joined = Curve.JoinCurves(extSegments, 0.01);
                return joined != null && joined.Length > 0 ? joined.ToList() : extSegments;
            }
            return new List<Curve>();
        }

        private double GetBrepArea(List<Curve> crvs)
        {
            if (crvs == null || !crvs.Any()) return 0.0;
            var breps = Brep.CreatePlanarBreps(crvs, 0.01);
            if (breps == null) return 0.0;
            double area = 0.0;
            foreach (var b in breps)
            {
                var amp = AreaMassProperties.Compute(b);
                if (amp != null) area += amp.Area;
            }
            return area;
        }

        private List<int> GetProgramColor(string prog, Dictionary<string, List<int>> customPalette)
        {
            string cleanProg = prog.Trim();
            if (customPalette.ContainsKey(cleanProg)) return customPalette[cleanProg];
            Random rnd = new Random(cleanProg.GetHashCode());
            return new List<int> { rnd.Next(70, 200), rnd.Next(70, 200), rnd.Next(70, 200) };
        }

        private JArray SerializeExactCurve(Curve crv)
        {
            var segmentsData = new JArray();
            var segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0) segments = new Curve[] { crv };
            foreach (var seg in segments)
            {
                if (seg.IsLinear(0.001))
                {
                    segmentsData.Add(new JObject(
                        new JProperty("type", "Line"),
                        new JProperty("start", new JArray(Math.Round(seg.PointAtStart.X, 4), Math.Round(seg.PointAtStart.Y, 4), Math.Round(seg.PointAtStart.Z, 4))),
                        new JProperty("end", new JArray(Math.Round(seg.PointAtEnd.X, 4), Math.Round(seg.PointAtEnd.Y, 4), Math.Round(seg.PointAtEnd.Z, 4)))
                    ));
                }
                else if (seg.IsArc(0.001))
                {
                    Arc arc;
                    if (seg.TryGetArc(out arc))
                    {
                        segmentsData.Add(new JObject(
                            new JProperty("type", "Arc"),
                            new JProperty("start", new JArray(Math.Round(arc.StartPoint.X, 4), Math.Round(arc.StartPoint.Y, 4), Math.Round(arc.StartPoint.Z, 4))),
                            new JProperty("mid", new JArray(Math.Round(arc.MidPoint.X, 4), Math.Round(arc.MidPoint.Y, 4), Math.Round(arc.MidPoint.Z, 4))),
                            new JProperty("end", new JArray(Math.Round(arc.EndPoint.X, 4), Math.Round(arc.EndPoint.Y, 4), Math.Round(arc.EndPoint.Z, 4)))
                        ));
                    }
                }
                else
                {
                    var polyCrv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0);
                    Polyline pline;
                    if (polyCrv != null && polyCrv.TryGetPolyline(out pline))
                    {
                        var pts = new JArray();
                        foreach (var pt in pline)
                        {
                            pts.Add(new JArray(Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4)));
                        }
                        segmentsData.Add(new JObject(
                            new JProperty("type", "Polyline"),
                            new JProperty("points", pts)
                        ));
                    }
                }
            }
            return segmentsData;
        }

        private Curve DeserializeCurve(JToken segmentsData)
        {
            if (segmentsData == null || !(segmentsData is JArray arr)) return null;
            List<Curve> crvs = new List<Curve>();
            foreach (JObject seg in arr)
            {
                string stype = seg["type"]?.ToString();
                if (stype == "Line")
                {
                    var start = seg["start"] as JArray;
                    var end = seg["end"] as JArray;
                    if (start != null && end != null && start.Count >= 3 && end.Count >= 3)
                        crvs.Add(new LineCurve(new Point3d((double)start[0], (double)start[1], (double)start[2]), new Point3d((double)end[0], (double)end[1], (double)end[2])));
                }
                else if (stype == "Arc")
                {
                    var start = seg["start"] as JArray;
                    var mid = seg["mid"] as JArray;
                    var end = seg["end"] as JArray;
                    if (start != null && mid != null && end != null && start.Count >= 3 && mid.Count >= 3 && end.Count >= 3)
                    crvs.Add(new ArcCurve(new Arc(
                        new Point3d((double)start[0], (double)start[1], (double)start[2]),
                        new Point3d((double)mid[0], (double)mid[1], (double)mid[2]),
                        new Point3d((double)end[0], (double)end[1], (double)end[2])
                    )));
                }
                else if (stype == "Polyline")
                {
                    var points = seg["points"] as JArray;
                    if (points != null)
                    {
                        var ptsList = new List<Point3d>();
                        foreach (JArray p in points)
                        {
                            if (p.Count >= 3) ptsList.Add(new Point3d((double)p[0], (double)p[1], (double)p[2]));
                        }
                        crvs.Add(new PolylineCurve(ptsList));
                    }
                }
            }
            if (crvs.Count == 0) return null;
            if (crvs.Count == 1) return crvs[0];
            var joined = Curve.JoinCurves(crvs, 0.01);
            if (joined != null && joined.Length > 0)
            {
                if (!joined[0].IsClosed) joined[0].MakeClosed(0.01);
                return joined[0];
            }
            return null;
        }

        // ==============================================================================
        // ENGINE CLASSES
        // ==============================================================================
        private class MassingBlock
        {
            public string name;
            public string tower_id;
            public string program;
            public double floor_height;
            public int floors;
            public double base_z;
            public double bldg_elev;
            public Curve base_curve;
            public List<int> color;
            public List<double> areas = new List<double>();
            public List<int> floor_indices = new List<int>();

            public MassingBlock(JObject data, double bldg_elev, Dictionary<string, List<int>> customPalette, MasterplanEngineComponent comp)
            {
                name = data["name"]?.ToString() ?? "Unknown";
                tower_id = data["tower_id"]?.ToString() ?? "Main_Tower";
                program = data["program"]?.ToString() ?? "Mixed Use";
                floor_height = data["floor_height"]?.ToObject<double>() ?? 4.0;
                floors = data["floors"]?.ToObject<int>() ?? 1;
                base_z = data["base_z"]?.ToObject<double>() ?? 0.0;
                this.bldg_elev = bldg_elev;

                if (data["boundary_segments"] != null)
                {
                    base_curve = comp.DeserializeCurve(data["boundary_segments"]);
                    if (base_curve != null)
                    {
                        var bbox = base_curve.GetBoundingBox(true);
                        base_curve.Transform(Transform.Translation(new Vector3d(0, 0, -bbox.Min.Z)));
                    }
                }

                color = comp.GetProgramColor(program, customPalette);
                for (int i = 0; i < floors; i++)
                {
                    double a = 0.0;
                    if (base_curve != null)
                    {
                        var amp = AreaMassProperties.Compute(base_curve);
                        if (amp != null) a = amp.Area;
                    }
                    areas.Add(a);
                    floor_indices.Add(0);
                }
            }
        }

        private class Building
        {
            public string name;
            public double elevation;
            public Dictionary<string, List<MassingBlock>> tower_groups = new Dictionary<string, List<MassingBlock>>();
            public List<MassingBlock> blocks = new List<MassingBlock>();
            public Dictionary<double, List<Curve>> master_slabs = new Dictionary<double, List<Curve>>();
            public JArray roof_json_data = new JArray();
            public JArray slab_json_data = new JArray();
            public JArray railing_json_data = new JArray();
            private MasterplanEngineComponent comp;

            public Building(JObject data, Dictionary<string, List<int>> customPalette, MasterplanEngineComponent comp)
            {
                this.comp = comp;
                name = data["name"]?.ToString() ?? "Building";
                elevation = data["true_base_elevation"]?.ToObject<double>() ?? 0.0;

                if (data["blocks"] is JArray bArray)
                {
                    foreach (JObject bData in bArray)
                    {
                        string tid = bData["tower_id"]?.ToString() ?? "Main_Tower";
                        if (!tower_groups.ContainsKey(tid)) tower_groups[tid] = new List<MassingBlock>();
                        var block = new MassingBlock(bData, elevation, customPalette, comp);
                        tower_groups[tid].Add(block);
                        blocks.Add(block);
                    }
                }
                blocks = blocks.OrderBy(b => b.base_z).ToList();
            }

            public void GenerateTopology()
            {
                foreach (var kvp in tower_groups)
                {
                    string tid = kvp.Key;
                    var tBlocks = kvp.Value;
                    bool isPodium = tid.ToLower().Contains("podium");

                    HashSet<double> zSet = new HashSet<double>();
                    foreach (var block in tBlocks)
                    {
                        for (int j = 0; j <= block.floors; j++)
                        {
                            zSet.Add(Math.Round(block.base_z + (j * block.floor_height), 3));
                        }
                    }
                    var sortedZ = zSet.ToList();
                    sortedZ.Sort();
                    var zToIdx = new Dictionary<double, int>();
                    for (int i = 0; i < sortedZ.Count; i++) zToIdx[sortedZ[i]] = i;

                    foreach (var block in tBlocks)
                    {
                        for (int j = 0; j < block.floors; j++)
                        {
                            double relZ = Math.Round(block.base_z + (j * block.floor_height), 3);
                            block.floor_indices[j] = zToIdx[relZ];
                        }
                    }

                    var zDict = new Dictionary<double, List<Curve>>();
                    foreach (var block in tBlocks)
                    {
                        for (int j = 0; j <= block.floors; j++)
                        {
                            double relZ = Math.Round(block.base_z + (j * block.floor_height), 3);
                            if (!isPodium && j == 0 && relZ > 0.001) continue;
                            if (!zDict.ContainsKey(relZ)) zDict[relZ] = new List<Curve>();
                            if (block.base_curve != null)
                                zDict[relZ].Add(block.base_curve.DuplicateCurve());
                        }
                    }

                    var keys = zDict.Keys.ToList();
                    keys.Sort();
                    foreach (double relZ in keys)
                    {
                        var crvs = zDict[relZ];
                        int fIdx = zToIdx[relZ];
                        foreach (var c in crvs) if (!c.IsClosed) c.MakeClosed(0.01);
                        var unionedCrvs = Curve.CreateBooleanUnion(crvs, 0.01);
                        List<Curve> finalSlabCrvs = (unionedCrvs != null && unionedCrvs.Length > 0) ? unionedCrvs.ToList() : crvs;

                        if (!master_slabs.ContainsKey(relZ)) master_slabs[relZ] = new List<Curve>();
                        master_slabs[relZ].AddRange(finalSlabCrvs.Select(c => c.DuplicateCurve()));

                        var blocksAbove = blocks.Where(b => b.base_z <= relZ + 0.001 && Math.Round(b.base_z + (b.floors * b.floor_height), 3) > relZ + 0.001).ToList();
                        var boundsAboveCrvs = blocksAbove.Where(b => b.base_curve != null).Select(b => b.base_curve.DuplicateCurve()).ToList();
                        var unionedBoundsAbove = boundsAboveCrvs.Count > 0 ? Curve.CreateBooleanUnion(boundsAboveCrvs, 0.01) : null;
                        var unionedBoundsAboveList = (unionedBoundsAbove != null && unionedBoundsAbove.Length > 0) ? unionedBoundsAbove.ToList() : boundsAboveCrvs;

                        var intersectCrvs = comp.SafeBooleanOp(finalSlabCrvs, unionedBoundsAboveList, "int", 0.001);

                        double slabArea = comp.GetBrepArea(finalSlabCrvs);
                        double towerArea = comp.GetBrepArea(intersectCrvs);
                        double trueZ = elevation + relZ;

                        var boundArr = new JArray();
                        foreach (var c in finalSlabCrvs) boundArr.Add(comp.SerializeExactCurve(c));

                        var slabData = new JObject(
                            new JProperty("tower_id", tid),
                            new JProperty("floor_index", fIdx),
                            new JProperty("true_z", Math.Round(trueZ, 3)),
                            new JProperty("area", Math.Round(slabArea, 2)),
                            new JProperty("boundary", boundArr)
                        );
                        slab_json_data.Add(slabData);

                        bool isRoof = false;
                        string roofType = "";
                        if (towerArea < 0.1)
                        {
                            isRoof = true;
                            roofType = "Roof Top";
                        }
                        else if ((slabArea - towerArea) > 1.0)
                        {
                            isRoof = true;
                            roofType = isPodium ? "Podium Roof" : "Setback Roof";
                        }

                        if (isRoof)
                        {
                            var exposedCrvs = (intersectCrvs != null && intersectCrvs.Count > 0) ? comp.SafeBooleanOp(finalSlabCrvs, intersectCrvs, "diff", 0.001) : finalSlabCrvs;
                            var nakedCrvs = comp.GetNakedRailings(exposedCrvs, intersectCrvs, 0.05);
                            var programsAbove = blocksAbove.Select(b => b.program).Distinct().ToList();

                            var slabBoundsArr = new JArray();
                            foreach (var c in finalSlabCrvs) slabBoundsArr.Add(comp.SerializeExactCurve(c));
                            var towerBoundsArr = new JArray();
                            foreach (var c in intersectCrvs) towerBoundsArr.Add(comp.SerializeExactCurve(c));

                            roof_json_data.Add(new JObject(
                                new JProperty("tower_id", tid),
                                new JProperty("floor_index", fIdx),
                                new JProperty("true_z", Math.Round(trueZ, 3)),
                                new JProperty("type", roofType),
                                new JProperty("roof_area", Math.Round(Math.Max(0, slabArea - towerArea), 2)),
                                new JProperty("programs_above", new JArray(programsAbove)),
                                new JProperty("slab_bounds", slabBoundsArr),
                                new JProperty("tower_bounds", towerBoundsArr)
                            ));

                            if (nakedCrvs != null && nakedCrvs.Count > 0)
                            {
                                var curvesArr = new JArray();
                                foreach (var c in nakedCrvs) curvesArr.Add(comp.SerializeExactCurve(c));
                                railing_json_data.Add(new JObject(
                                    new JProperty("tower_id", tid),
                                    new JProperty("floor_index", fIdx),
                                    new JProperty("true_z", Math.Round(trueZ, 3)),
                                    new JProperty("curves", curvesArr)
                                ));
                            }
                        }
                    }
                }
            }
        }
    }
}
