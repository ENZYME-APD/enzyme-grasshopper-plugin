using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Enzyme.Components
{
    public class BimJsonComponent : GH_Component
    {
        public BimJsonComponent()
          : base("BIM Attribute Serializer", "BIM_JSON",
              "Serializes referenced Rhino curves via Attribute User Text.",
              "Enzyme", "Masterplan (Beta)")
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
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 1, false, 210, -20);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 3, false, 210, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -11, 180, 22);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Guids", "G", "Referenced Rhino geometry IDs.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Refresh", "R", "Wire a Button to force re-read of attributes.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Fillet_Config", "FC", "JSON string defining fillet rules.", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("PushToRhino", "PTR", "Wire a Button to push elevations to Rhino.", GH_ParamAccess.item, false);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Payload", "JSON", "Serialized JSON.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();

            List<IGH_Goo> gooGuids = new List<IGH_Goo>();
            if (!DA.GetDataList(0, gooGuids)) return;
            
            List<Guid> guids = new List<Guid>();
            foreach (var goo in gooGuids)
            {
                if (goo == null) continue;
                if (GH_Convert.ToGUID(goo, out Guid id, GH_Conversion.Both))
                {
                    guids.Add(id);
                }
            }

            bool refresh = false;
            DA.GetData(1, ref refresh);

            string filletJson = "";
            DA.GetData(2, ref filletJson);

            bool pushBtn = false;
            DA.GetData(3, ref pushBtn);

            double defaultRad = 0.0;
            JArray filletRules = new JArray();

            if (!string.IsNullOrWhiteSpace(filletJson))
            {
                try
                {
                    JObject config = JObject.Parse(filletJson);
                    if (config.TryGetValue("default_radius", out JToken drToken))
                        defaultRad = drToken.Value<double>();
                    if (config.TryGetValue("rules", out JToken rulesToken) && rulesToken is JArray array)
                        filletRules = array;
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Configurator JSON Error: " + e.Message);
                }
            }

            if (guids.Count == 0)
            {
                this.Message = this.NickName + "\nTime: 0.0 ms\n---\nAwaiting Data";
                return;
            }

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            Dictionary<string, BuildingData> bldgDataMap = new Dictionary<string, BuildingData>();
            int totalBlocks = 0;

            foreach (var gid in guids)
            {
                var obj = doc.Objects.FindId(gid);
                if (obj == null) continue;
                var crv = obj.Geometry as Curve;
                if (crv == null) continue;

                string objType = obj.Attributes.GetUserString("Type");
                if (!string.IsNullOrEmpty(objType) && objType.ToLower() == "core") continue;

                string bldgId = obj.Attributes.GetUserString("BuildingID");
                if (string.IsNullOrEmpty(bldgId)) bldgId = "Building_01";

                string prog = obj.Attributes.GetUserString("Program");
                if (string.IsNullOrEmpty(prog)) prog = "Default";

                string tId = obj.Attributes.GetUserString("TowerID");
                if (string.IsNullOrEmpty(tId)) tId = "Main_Tower";

                double fh = 3.5;
                string fhStr = obj.Attributes.GetUserString("FloorHeight");
                if (!string.IsNullOrEmpty(fhStr)) double.TryParse(fhStr, out fh);

                int flrs = 1;
                string flrsStr = obj.Attributes.GetUserString("Floors");
                if (!string.IsNullOrEmpty(flrsStr)) int.TryParse(flrsStr, out flrs);

                int phase = 0;
                string phaseStr = obj.Attributes.GetUserString("Phase");
                if (!string.IsNullOrEmpty(phaseStr)) int.TryParse(phaseStr, out phase);

                if (!bldgDataMap.ContainsKey(bldgId))
                {
                    bldgDataMap[bldgId] = new BuildingData();
                }

                double crvMinZ = crv.GetBoundingBox(true).Min.Z;
                bldgDataMap[bldgId].RawZCoords.Add(crvMinZ);

                Curve crvFlat = crv.DuplicateCurve();
                crvFlat.Transform(Transform.PlanarProjection(Plane.WorldXY));

                if (crvFlat.IsClosed)
                {
                    var orientation = crvFlat.ClosedCurveOrientation(Plane.WorldXY);
                    if (orientation == CurveOrientation.Clockwise)
                    {
                        crvFlat.Reverse();
                    }
                }

                double targetRadius = GetTargetRadius(bldgId, tId, prog, filletRules, defaultRad);
                if (targetRadius > 0)
                {
                    crvFlat = ApplySafeFillet(crvFlat, targetRadius);
                }

                bldgDataMap[bldgId].Blocks.Add(new BlockData
                {
                    Guid = gid,
                    Program = prog,
                    TowerId = tId,
                    FloorHeight = fh,
                    Floors = flrs,
                    Phase = phase,
                    CurveFlat = crvFlat
                });
                totalBlocks++;
            }

            var masterplanDict = new Dictionary<string, object>
            {
                { "buildings", new List<object>() }
            };

            Dictionary<Guid, double> guidZMap = new Dictionary<Guid, double>();
            var buildingsList = (List<object>)masterplanDict["buildings"];

            foreach (var kvp in bldgDataMap)
            {
                string bId = kvp.Key;
                var bData = kvp.Value;
                double minZ = bData.RawZCoords.Count > 0 ? bData.RawZCoords.Min() : 0.0;

                var buildingDict = new Dictionary<string, object>
                {
                    { "name", bId },
                    { "true_base_elevation", Math.Round(minZ, 3) },
                    { "blocks", new List<object>() }
                };

                var sortedBlocks = bData.Blocks.OrderBy(b => b.Phase).ToList();
                Dictionary<int, double> phaseMaxZ = new Dictionary<int, double> { { -1, 0.0 } };
                Dictionary<string, double> towerCurrentZ = new Dictionary<string, double>();
                var blocksList = (List<object>)buildingDict["blocks"];

                for (int i = 0; i < sortedBlocks.Count; i++)
                {
                    var blk = sortedBlocks[i];
                    int phase = blk.Phase;
                    string tId = blk.TowerId;
                    double fh = blk.FloorHeight;
                    int flrs = blk.Floors;
                    Curve crvFlat = blk.CurveFlat;
                    Guid gid = blk.Guid;

                    double currentBaseZ = 0.0;
                    if (towerCurrentZ.ContainsKey(tId))
                    {
                        currentBaseZ = towerCurrentZ[tId];
                    }
                    else
                    {
                        var prevPhases = phaseMaxZ.Keys.Where(p => p < phase).ToList();
                        if (prevPhases.Count > 0)
                        {
                            currentBaseZ = prevPhases.Max(p => phaseMaxZ[p]);
                        }
                    }

                    guidZMap[gid] = currentBaseZ + minZ;

                    double topZ = currentBaseZ + (fh * flrs);
                    towerCurrentZ[tId] = topZ;
                    
                    if (!phaseMaxZ.ContainsKey(phase)) phaseMaxZ[phase] = 0.0;
                    phaseMaxZ[phase] = Math.Max(phaseMaxZ[phase], topZ);

                    var blockDict = new Dictionary<string, object>
                    {
                        { "name", $"{blk.Program}_{i}" },
                        { "tower_id", tId },
                        { "program", blk.Program },
                        { "floor_height", fh },
                        { "floors", flrs },
                        { "base_z", Math.Round(currentBaseZ, 3) },
                        { "boundary_segments", SerializeExactCurve(crvFlat) }
                    };
                    blocksList.Add(blockDict);
                }

                buildingsList.Add(buildingDict);
            }

            int movedCount = PushElevationsToRhino(guidZMap, pushBtn, doc);

            string jsonPayload = JsonConvert.SerializeObject(masterplanDict, Formatting.Indented);
            DA.SetData(0, jsonPayload);

            sw.Stop();
            double execTime = sw.Elapsed.TotalMilliseconds;

            string uiMsg = $"{this.NickName}\nTime: {execTime:F1} ms\n---\nBldgs: {bldgDataMap.Count} | Blocks: {totalBlocks}";
            if (movedCount > 0) uiMsg += $"\nMoved to Z: {movedCount}";
            this.Message = uiMsg;
        }

        private class BuildingData
        {
            public List<double> RawZCoords { get; set; } = new List<double>();
            public List<BlockData> Blocks { get; set; } = new List<BlockData>();
        }

        private class BlockData
        {
            public Guid Guid { get; set; }
            public string Program { get; set; }
            public string TowerId { get; set; }
            public double FloorHeight { get; set; }
            public int Floors { get; set; }
            public int Phase { get; set; }
            public Curve CurveFlat { get; set; }
        }

        private double GetTargetRadius(string bId, string tId, string prog, JArray rules, double fallback)
        {
            foreach (JObject rule in rules.OfType<JObject>())
            {
                string rtype = rule["type"]?.ToString() ?? "";
                string rmatch = rule["match"]?.ToString() ?? "";
                double rrad = rule["radius"] != null ? rule["radius"].Value<double>() : 0.0;
                bool rexact = rule["exact"] != null ? rule["exact"].Value<bool>() : true;

                if (rtype == "Tower" && IsMatch(tId, rmatch, rexact)) return rrad;
                if (rtype == "Program" && IsMatch(prog, rmatch, rexact)) return rrad;
                if (rtype == "Building" && IsMatch(bId, rmatch, rexact)) return rrad;
            }
            return fallback;
        }

        private bool IsMatch(string target, string pattern, bool exactMatch)
        {
            if (pattern == "*") return true;
            if (string.IsNullOrEmpty(target)) return false;
            string tStr = target.Trim().ToUpperInvariant();
            string pStr = pattern.Trim().ToUpperInvariant();
            if (exactMatch) return tStr == pStr;
            else return tStr.Contains(pStr);
        }

        private Curve ApplySafeFillet(Curve crv, double requestedRadius)
        {
            if (requestedRadius <= 0.001) return crv;
            var segments = crv.DuplicateSegments();
            if (segments == null || segments.Length < 2) return crv;

            double minLen = segments.Min(s => s.GetLength());
            double safeRadius = Math.Min(requestedRadius, minLen * 0.49);

            if (safeRadius <= 0.001) return crv;
            var filletedCrv = Curve.CreateFilletCornersCurve(crv, safeRadius, 0.01, 0.1);
            return filletedCrv ?? crv;
        }

        private List<object> SerializeExactCurve(Curve crv)
        {
            var segmentsData = new List<object>();
            var segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0)
            {
                segments = new Curve[] { crv };
            }

            foreach (var seg in segments)
            {
                if (seg.IsLinear(0.001))
                {
                    segmentsData.Add(new Dictionary<string, object>
                    {
                        { "type", "Line" },
                        { "start", new[] { Math.Round(seg.PointAtStart.X, 4), Math.Round(seg.PointAtStart.Y, 4), Math.Round(seg.PointAtStart.Z, 4) } },
                        { "end", new[] { Math.Round(seg.PointAtEnd.X, 4), Math.Round(seg.PointAtEnd.Y, 4), Math.Round(seg.PointAtEnd.Z, 4) } }
                    });
                }
                else if (seg.IsArc(0.001))
                {
                    if (seg.TryGetArc(out Arc arc))
                    {
                        segmentsData.Add(new Dictionary<string, object>
                        {
                            { "type", "Arc" },
                            { "start", new[] { Math.Round(arc.StartPoint.X, 4), Math.Round(arc.StartPoint.Y, 4), Math.Round(arc.StartPoint.Z, 4) } },
                            { "mid", new[] { Math.Round(arc.MidPoint.X, 4), Math.Round(arc.MidPoint.Y, 4), Math.Round(arc.MidPoint.Z, 4) } },
                            { "end", new[] { Math.Round(arc.EndPoint.X, 4), Math.Round(arc.EndPoint.Y, 4), Math.Round(arc.EndPoint.Z, 4) } }
                        });
                    }
                }
                else
                {
                    var polyCrv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0);
                    if (polyCrv != null && polyCrv.TryGetPolyline(out Polyline poly))
                    {
                        var pts = poly.Select(pt => new[] { Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4) }).ToList();
                        segmentsData.Add(new Dictionary<string, object>
                        {
                            { "type", "Polyline" },
                            { "points", pts }
                        });
                    }
                }
            }
            return segmentsData;
        }

        private int PushElevationsToRhino(Dictionary<Guid, double> guidToZMap, bool executePush, RhinoDoc doc)
        {
            if (!executePush) return 0;
            int movedCount = 0;

            foreach (var kvp in guidToZMap)
            {
                Guid gid = kvp.Key;
                double targetZ = kvp.Value;
                var obj = doc.Objects.FindId(gid);
                if (obj == null) continue;
                var crv = obj.Geometry as Curve;
                if (crv == null) continue;

                double currentZ = crv.GetBoundingBox(true).Min.Z;
                double zDiff = targetZ - currentZ;

                if (Math.Abs(zDiff) > 0.001)
                {
                    var xform = Transform.Translation(0, 0, zDiff);
                    doc.Objects.Transform(gid, xform, true);
                    movedCount++;
                }
            }
            return movedCount;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("BIM_JSON.png");

        public override Guid ComponentGuid => new Guid("B5D0F005-0219-4822-BB7B-191BB8C391AA");

        public override GH_Exposure Exposure => GH_Exposure.primary;
    }
}
