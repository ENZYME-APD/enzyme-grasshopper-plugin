using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;

namespace Enzyme.Components
{
    public class AutoPhaseComponent : GH_Component
    {
        private List<string> _cachedLog = new List<string> { "Awaiting execution. Click the 'Run' button." };
        private string _cachedMsg = "Auto_Phase\nAwaiting Run";

        public AutoPhaseComponent()
          : base("Auto-Phase Assigner by Z", "Auto_Phase",
              "Strictly sequences the podium first, then cascades that count to independent towers.",
              "Enzyme", "Masterplan (Beta)")
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
                int ix = 220, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 1, ix, -150);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Guids", "Guids", "Referenced Rhino geometry IDs.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Run", "Run", "Wire a Button here to execute.", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Log", "Log", "Execution log", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Grasshopper.Kernel.Types.IGH_Goo> goos = new List<Grasshopper.Kernel.Types.IGH_Goo>();
            if (!DA.GetDataList(0, goos)) return;
            
            bool run = false;
            if (!DA.GetData(1, ref run)) return;

            if (!run)
            {
                DA.SetDataList(0, _cachedLog);
                Message = _cachedMsg;
                return;
            }

            if (goos == null || goos.Count == 0)
            {
                _cachedMsg = $"{this.NickName}\nNo Guids";
                _cachedLog = new List<string> { "No Guids provided." };
                DA.SetDataList(0, _cachedLog);
                Message = _cachedMsg;
                return;
            }

            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            var buildings = new Dictionary<string, BuildingData>();
            var log = new List<string>();
            int modifyCount = 0;

            Stopwatch sw = Stopwatch.StartNew();

            foreach (var goo in goos)
            {
                Guid gid = Guid.Empty;
                if (goo == null) continue;
                
                if (!goo.CastTo(out gid))
                {
                    if (goo.CastTo(out string strId) && Guid.TryParse(strId, out Guid parsedGuid))
                    {
                        gid = parsedGuid;
                    }
                }
                
                if (gid == Guid.Empty) continue;

                var obj = doc.Objects.FindId(gid);
                if (obj == null) continue;

                string bId = obj.Attributes.GetUserString("BuildingID");
                if (string.IsNullOrEmpty(bId)) bId = "Building_01";

                string objType = obj.Attributes.GetUserString("Type");
                if (string.IsNullOrEmpty(objType)) objType = "Block";

                if (objType.Equals("core", StringComparison.OrdinalIgnoreCase))
                    continue;

                var bbox = obj.Geometry.GetBoundingBox(true);
                double zHeight = Math.Round(bbox.Min.Z, 3);

                string tId = obj.Attributes.GetUserString("TowerID");
                if (string.IsNullOrEmpty(tId)) tId = "Main";

                if (!buildings.ContainsKey(bId))
                    buildings[bId] = new BuildingData();

                var item = new BlockItem { Obj = obj, TowerId = tId, Z = zHeight };

                if (tId.ToLower().Contains("podium"))
                {
                    buildings[bId].PodiumBlocks.Add(item);
                }
                else
                {
                    if (!buildings[bId].TowerBlocks.ContainsKey(tId))
                        buildings[bId].TowerBlocks[tId] = new List<BlockItem>();
                    buildings[bId].TowerBlocks[tId].Add(item);
                }
            }

            foreach (var kvp in buildings)
            {
                string bId = kvp.Key;
                var data = kvp.Value;

                int maxPodiumPhase = -1;

                var podiums = data.PodiumBlocks.OrderBy(x => x.Z).ToList();
                for (int i = 0; i < podiums.Count; i++)
                {
                    int currentPhase = i;
                    maxPodiumPhase = currentPhase;

                    podiums[i].Obj.Attributes.SetUserString("Phase", currentPhase.ToString());
                    podiums[i].Obj.CommitChanges();
                    modifyCount++;
                    log.Add($"-> {bId} ({podiums[i].TowerId}): Assigned Phase {currentPhase}");
                }

                foreach (var tKvp in data.TowerBlocks)
                {
                    string tId = tKvp.Key;
                    var tItems = tKvp.Value.OrderBy(x => x.Z).ToList();

                    for (int i = 0; i < tItems.Count; i++)
                    {
                        int newPhase = maxPodiumPhase + 1 + i;
                        string newPhaseStr = newPhase.ToString();

                        tItems[i].Obj.Attributes.SetUserString("Phase", newPhaseStr);
                        tItems[i].Obj.CommitChanges();
                        modifyCount++;
                        log.Add($"-> {bId} (Tower: {tId}): Assigned Phase {newPhaseStr}");
                    }
                }
            }

            string statusLine;
            if (modifyCount > 0)
            {
                statusLine = $"Updated {modifyCount} phases";
                log.Insert(0, "SUCCESS: " + statusLine);
            }
            else
            {
                statusLine = "No valid blocks found";
                log.Insert(0, "WARNING: Found no blocks to phase.");
            }

            sw.Stop();
            long execTime = sw.ElapsedMilliseconds;

            _cachedMsg = $"{this.NickName}\nTime: {execTime} ms\n{statusLine}";
            _cachedLog = log;

            Message = _cachedMsg;
            DA.SetDataList(0, _cachedLog);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Auto_Phase.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("6c5d1f8a-4932-4217-bbd3-6db0b5346061"); }
        }

        private class BuildingData
        {
            public List<BlockItem> PodiumBlocks { get; set; } = new List<BlockItem>();
            public Dictionary<string, List<BlockItem>> TowerBlocks { get; set; } = new Dictionary<string, List<BlockItem>>();
        }

        private class BlockItem
        {
            public RhinoObject Obj { get; set; }
            public string TowerId { get; set; }
            public double Z { get; set; }
        }
    }
}
