using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class FacadeDist : GH_Component
    {
        public FacadeDist()
          : base("Facade Module Distributor", "FACADE_DIST",
              "Dynamically streams procedural facade modules with high-performance hot-reloading.",
              Enzyme.Utils.TabInfo.TabName, "Facade")
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
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 3, false, 210, -20);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 4, 210, 20);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Base_Curves", "Base_Curves", "Floor boundaries to run facade loops against.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Floor_Heights", "Floor_Heights", "Intended structural heights per level.", GH_ParamAccess.list);
            pManager.AddTextParameter("Local_Repo_Dir", "Local_Repo_Dir", "Path to your local git directory OR python file.", GH_ParamAccess.item, "");
            pManager.AddBooleanParameter("Dev_Mode", "Dev_Mode", "True = Local Git Repo | False = Production Github Remote.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Force_Update", "Force_Update", "Force a re-download of the remote production scripts.", GH_ParamAccess.item, false);
            
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Horizontal_Bands", "Horizontal_Bands", "Horizontal_Bands", GH_ParamAccess.list);
            pManager.AddGenericParameter("Storefront_Mullions", "Storefront_Mullions", "Storefront_Mullions", GH_ParamAccess.list);
            pManager.AddGenericParameter("Storefront_Glass", "Storefront_Glass", "Storefront_Glass", GH_ParamAccess.list);
        }

        private static Dictionary<string, string> _scriptCache = new Dictionary<string, string>();
        private static Dictionary<string, DateTime> _timeCache = new Dictionary<string, DateTime>();

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            GH_Structure<GH_Curve> crvsTree = null;
            List<double> heights = new List<double>();
            string local_path_in = "";
            bool dev_toggle = false;
            bool update_toggle = false;

            DA.GetDataTree(0, out crvsTree);
            DA.GetDataList(1, heights);
            DA.GetData(2, ref local_path_in);
            DA.GetData(3, ref dev_toggle);
            DA.GetData(4, ref update_toggle);

            List<Curve> clean_crvs = new List<Curve>();
            if (crvsTree != null && !crvsTree.IsEmpty)
            {
                foreach (var path in crvsTree.Paths)
                {
                    var branch = crvsTree.get_Branch(path);
                    foreach (GH_Curve ghCrv in branch.Cast<GH_Curve>())
                    {
                        if (ghCrv != null && ghCrv.Value != null && ghCrv.Value.IsClosed)
                        {
                            clean_crvs.Add(ghCrv.Value);
                        }
                    }
                }
            }

            if (clean_crvs.Count == 0)
            {
                Message = "FACADE_DIST\nTime: 0.0 ms\n---\nAwaiting Curves";
                return;
            }

            string GITHUB_RAW_URL = "https://raw.githubusercontent.com/ENZYME-APD/Grasshopper-GitHub-test/refs/heads/main/facade_modules.py";
            string source_file_path = "";
            string mode_label = "";

            if (dev_toggle)
            {
                if (string.IsNullOrWhiteSpace(local_path_in) || (!File.Exists(local_path_in) && !Directory.Exists(local_path_in)))
                {
                    Message = "FACADE_DIST\nTime: 0.0 ms\n---\nInvalid Local Path";
                    return;
                }
                
                if (File.Exists(local_path_in) && local_path_in.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
                    source_file_path = local_path_in;
                else
                    source_file_path = Path.Combine(local_path_in, "facade_modules.py");

                mode_label = "DEV MODE: Local";
            }
            else
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".rhinocode", "gh_distributed_scripts");
                source_file_path = Path.Combine(appData, "facade_modules.py");
                mode_label = "PROD MODE: Remote";

                if (update_toggle || !File.Exists(source_file_path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(source_file_path));
                    try
                    {
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFile(GITHUB_RAW_URL, source_file_path);
                        }
                    }
                    catch (Exception)
                    {
                        if (!File.Exists(source_file_path))
                        {
                            Message = "FACADE_DIST\nTime: 0.0 ms\n---\nSync Error";
                            return;
                        }
                    }
                }
            }

            string module_id = "enzyme_facade_core";
            string cache_key = "mtime_" + module_id;
            
            DateTime current_mtime = File.GetLastWriteTime(source_file_path);
            
            bool needs_compile = update_toggle || 
                                 !_scriptCache.ContainsKey(module_id) || 
                                 !_timeCache.ContainsKey(cache_key) || 
                                 _timeCache[cache_key] != current_mtime;

            string cache_status = "";
            try
            {
                if (needs_compile)
                {
                    string scriptContent = File.ReadAllText(source_file_path);
                    _scriptCache[module_id] = scriptContent;
                    _timeCache[cache_key] = current_mtime;
                    cache_status = "Compiled";
                }
                else
                {
                    cache_status = "RAM Cached";
                }
            }
            catch
            {
                Message = "FACADE_DIST\nTime: 0.0 ms\n---\nCompile Error";
                return;
            }

            var safe_heights = (heights != null && heights.Count > 0) ? heights : new List<double> { 4.0 };

            try
            {
                var py = Rhino.Runtime.PythonScript.Create();
                py.SetVariable("clean_crvs", clean_crvs);
                py.SetVariable("safe_heights", safe_heights);
                
                string runnerScript = _scriptCache[module_id] + "\n" +
                                      "bands = generate_horizontal_bands(clean_crvs, safe_heights, band_thickness=0.30, division_count=3)\n" +
                                      "res_storefront = generate_storefront(clean_crvs, safe_heights, mullion_spacing=1.5, glass_inset=0.05)\n" +
                                      "mullions = res_storefront[0]\n" +
                                      "glass = res_storefront[1]\n";

                py.ExecuteScript(runnerScript);

                var bands = py.GetVariable("bands");
                var mullions = py.GetVariable("mullions");
                var glass = py.GetVariable("glass");

                DA.SetDataList(0, bands as System.Collections.IEnumerable);
                DA.SetDataList(1, mullions as System.Collections.IEnumerable);
                DA.SetDataList(2, glass as System.Collections.IEnumerable);
            }
            catch
            {
                Message = "FACADE_DIST\nTime: 0.0 ms\n---\nCompile Error";
                return;
            }

            watch.Stop();
            double exec_time = watch.Elapsed.TotalMilliseconds;
            Message = $"FACADE_DIST\nTime: {exec_time:F1} ms\n---\n{mode_label} [{cache_status}]\nProfiles: {clean_crvs.Count}";
        }

        public override Guid ComponentGuid => new Guid("08412F4D-DDE4-42CE-A1C8-243B5761358F");

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("FacadeDist.png");

        public override GH_Exposure Exposure => GH_Exposure.secondary;
    }
}
