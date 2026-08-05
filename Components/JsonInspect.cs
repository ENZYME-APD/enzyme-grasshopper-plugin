using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Enzyme.Core.UI; 

namespace Enzyme.Components
{
    public class JsonInspectComponent : GH_Component
    {
        public JsonInspectComponent()
          : base("JSON Metadata Inspector", "JSON_Inspect",
              "Extracts all unique filter keys from any MP Engine JSON.",
              "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("MP_JSON", "MP_JSON", "Any JSON stream (Masses, Slabs, Roofs, Railings, Facades).", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Buildings", "Buildings", "Unique Building IDs.", GH_ParamAccess.list);
            pManager.AddTextParameter("Towers", "Towers", "Unique Tower IDs.", GH_ParamAccess.list);
            pManager.AddTextParameter("Programs", "Programs", "Unique Program names.", GH_ParamAccess.list);
            pManager.AddTextParameter("Types", "Types", "Unique element types (e.g., Roof classifications).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Levels", "Levels", "Unique floor indices, sorted numerically.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string jsonIn = string.Empty;
            if (!DA.GetData(0, ref jsonIn) || string.IsNullOrWhiteSpace(jsonIn))
            {
                this.Message = this.NickName + "\nTime: 0.0 ms\n---\nAwaiting Data";
                return;
            }

            HashSet<string> bldgs = new HashSet<string>();
            HashSet<string> towers = new HashSet<string>();
            HashSet<string> progs = new HashSet<string>();
            HashSet<string> types = new HashSet<string>();
            HashSet<int> levels = new HashSet<int>();

            try
            {
                JObject data = JObject.Parse(jsonIn);

                foreach (var bldg in data)
                {
                    bldgs.Add(bldg.Key);

                    if (bldg.Value is JObject contentDict)
                    {
                        foreach (var prog in contentDict)
                        {
                            progs.Add(prog.Key);
                            if (prog.Value is JArray floors)
                            {
                                foreach (var item in floors)
                                {
                                    if (item is JObject itemDict)
                                    {
                                        if (itemDict.ContainsKey("tower_id")) towers.Add(itemDict["tower_id"].ToString());
                                        if (itemDict.ContainsKey("floor_index")) levels.Add(itemDict["floor_index"].Value<int>());
                                        if (itemDict.ContainsKey("type")) types.Add(itemDict["type"].ToString());
                                    }
                                }
                            }
                        }
                    }
                    else if (bldg.Value is JArray contentList)
                    {
                        foreach (var item in contentList)
                        {
                            if (item is JObject itemDict)
                            {
                                if (itemDict.ContainsKey("tower_id")) towers.Add(itemDict["tower_id"].ToString());
                                if (itemDict.ContainsKey("program")) progs.Add(itemDict["program"].ToString());
                                if (itemDict.ContainsKey("type")) types.Add(itemDict["type"].ToString());
                                if (itemDict.ContainsKey("floor_index")) levels.Add(itemDict["floor_index"].Value<int>());
                                
                                if (itemDict.ContainsKey("programs_above") && itemDict["programs_above"] is JArray programsAbove)
                                {
                                    foreach (var p in programsAbove)
                                    {
                                        progs.Add(p.ToString());
                                    }
                                }
                            }
                        }
                    }
                }

                List<string> outBldgs = bldgs.ToList();
                outBldgs.Sort();
                List<string> outTowers = towers.ToList();
                outTowers.Sort();
                List<string> outProgs = progs.ToList();
                outProgs.Sort();
                List<string> outTypes = types.ToList();
                outTypes.Sort();
                List<int> outLevels = levels.ToList();
                outLevels.Sort();

                stopwatch.Stop();
                double execTime = stopwatch.Elapsed.TotalMilliseconds;
                int totalTags = outBldgs.Count + outTowers.Count + outProgs.Count + outTypes.Count + outLevels.Count;

                this.Message = this.NickName + $"\nTime: {execTime:F1} ms\n---\nTags Found: {totalTags}";

                DA.SetDataList(0, outBldgs);
                DA.SetDataList(1, outTowers);
                DA.SetDataList(2, outProgs);
                DA.SetDataList(3, outTypes);
                DA.SetDataList(4, outLevels);
            }
            catch (Exception ex)
            {
                this.Message = $"JSON Parse Error:\n{ex.Message}";
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("JSON_Inspect.png");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public override Guid ComponentGuid => new Guid("CA44BB4A-E36D-41D1-A5BC-B0D13993D6B5");
    }
}
