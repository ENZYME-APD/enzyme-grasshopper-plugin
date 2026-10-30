using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Enzyme.Utils;

namespace Enzyme.Components
{
    /// <summary>
    /// Writes one module file of the Deep Site Analysis report (04_metrics/&lt;module&gt;.json) from
    /// Dashboard Data outputs and hand-picked metrics, with green / amber / red scorecard flags.
    /// </summary>
    public class ReportMetricsExport : EnzymeComponent
    {
        public ReportMetricsExport()
          : base("Report Metrics", "Metrics",
              "Collects Dashboard Data from any analysis components plus your own metrics into one report module file (04_metrics/<module>.json: metrics, green/amber/red flags, maps, sources, plugin version), ready for the report and its scorecard.",
              "Enzyme", "Site Analysis")
        { }

        public override Guid ComponentGuid => new Guid("5F0B9C3E-2D7A-4C61-A8E4-6B1D9F27C3A0");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => IconLoader.Load("report_metrics.png");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Module", "Module", "Report module code, e.g. C1, C2, D1, F2", GH_ParamAccess.item, "C1");
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "Dashboard Data JSON from any Enzyme analysis components (one per item)", GH_ParamAccess.list);
            pManager.AddTextParameter("Prefixes", "Prefixes", "Optional key prefix per Dashboard item, e.g. 'slope', 'wind_ne', 'storm_100y'", GH_ParamAccess.list);
            pManager.AddTextParameter("Keys", "Keys", "Extra metric keys, e.g. elev_min_m, retention_volume_m3", GH_ParamAccess.list);
            pManager.AddNumberParameter("Values", "Values", "Values for Keys (same order)", GH_ParamAccess.list);
            pManager.AddTextParameter("Units", "Units", "Units for Keys (m, ha, %, m3...)", GH_ParamAccess.list);
            pManager.AddTextParameter("Scopes", "Scopes", "Scope per key: site (default), buffer_1km, buffer_5km...", GH_ParamAccess.list);
            pManager.AddTextParameter("FlagThemes", "FlagThemes", "Scorecard themes, e.g. Buildability, Flood & drainage, Climate comfort", GH_ParamAccess.list);
            pManager.AddTextParameter("FlagKeys", "FlagKeys", "Metric key rated for each theme", GH_ParamAccess.list);
            pManager.AddNumberParameter("Amber", "Amber", "Amber threshold per flag", GH_ParamAccess.list);
            pManager.AddNumberParameter("Red", "Red", "Red threshold per flag (Red > Amber: higher is worse; Red < Amber: lower is worse)", GH_ParamAccess.list);
            pManager.AddTextParameter("Sources", "Sources", "Input datasets as 'dataset | version | resolution | date'", GH_ParamAccess.list);
            pManager.AddTextParameter("Maps", "Maps", "Map image files for this module (e.g. from Export Views)", GH_ParamAccess.list);
            pManager.AddTextParameter("Captions", "Captions", "Caption per map", GH_ParamAccess.list);
            pManager.AddTextParameter("Confidence", "Confidence", "field-verified, modelled or estimated", GH_ParamAccess.item, "modelled");
            pManager.AddTextParameter("Folder", "Folder", "Project folder (04_metrics is added) or the 04_metrics folder itself", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Merge", "Merge", "Keep metrics already in the file from other definitions and replace only this run's keys", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Write", "Write", "Write the file (use a button)", GH_ParamAccess.item, false);
            for (int i = 1; i < 18; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "JSON", "The module file content", GH_ParamAccess.item);
            pManager.AddTextParameter("Scorecard", "Scorecard", "Theme: rating (value vs thresholds)", GH_ParamAccess.list);
            pManager.AddTextParameter("Metrics", "Metrics", "key = value unit, for checking", GH_ParamAccess.list);
            pManager.AddTextParameter("File", "File", "Path written (empty until Write)", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void AutoWire(GH_Document doc)
        {
            AutoWireHelper.WireButton(this, doc, 17, 140, 0);
        }

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            string module = "C1"; DA.GetData(0, ref module);
            var dash = new List<string>(); DA.GetDataList(1, dash);
            var prefixes = new List<string>(); DA.GetDataList(2, prefixes);
            var keys = new List<string>(); DA.GetDataList(3, keys);
            var vals = new List<double>(); DA.GetDataList(4, vals);
            var units = new List<string>(); DA.GetDataList(5, units);
            var scopes = new List<string>(); DA.GetDataList(6, scopes);
            var themes = new List<string>(); DA.GetDataList(7, themes);
            var fkeys = new List<string>(); DA.GetDataList(8, fkeys);
            var amber = new List<double>(); DA.GetDataList(9, amber);
            var red = new List<double>(); DA.GetDataList(10, red);
            var sources = new List<string>(); DA.GetDataList(11, sources);
            var maps = new List<string>(); DA.GetDataList(12, maps);
            var caps = new List<string>(); DA.GetDataList(13, caps);
            string conf = "modelled"; DA.GetData(14, ref conf);
            string folder = null; DA.GetData(15, ref folder);
            bool merge = true; DA.GetData(16, ref merge);
            bool write = false; DA.GetData(17, ref write);

            module = (module ?? "").Trim().ToUpperInvariant();
            if (module.Length == 0) throw new ArgumentException("Module code is empty (e.g. C1).");
            if (keys.Count != vals.Count) throw new ArgumentException("Keys (" + keys.Count + ") and Values (" + vals.Count + ") must match.");
            int nf = themes.Count;
            if (fkeys.Count != nf || amber.Count != nf || red.Count != nf)
                throw new ArgumentException("FlagThemes, FlagKeys, Amber and Red must have the same number of items.");

            var metrics = new List<MetricItem>();
            for (int i = 0; i < dash.Count; i++)
                metrics.AddRange(ReportMetrics.FromDashboard(dash[i], i < prefixes.Count ? prefixes[i] : (dash.Count > 1 ? "d" + (i + 1) : "")));
            for (int i = 0; i < keys.Count; i++)
                metrics.Add(new MetricItem
                {
                    Key = ReportMetrics.Slug(keys[i]),
                    Value = vals[i],
                    Unit = i < units.Count ? units[i] : "",
                    Scope = i < scopes.Count && !string.IsNullOrWhiteSpace(scopes[i]) ? scopes[i].Trim() : "site"
                });
            var rules = new List<FlagRule>();
            for (int i = 0; i < nf; i++) rules.Add(new FlagRule { Theme = themes[i], Key = ReportMetrics.Slug(fkeys[i]), Amber = amber[i], Red = red[i] });

            string path = null;
            if (!string.IsNullOrWhiteSpace(folder))
            {
                string dir = folder.Trim();
                if (!string.Equals(Path.GetFileName(dir.TrimEnd('/', '\\')), "04_metrics", StringComparison.OrdinalIgnoreCase))
                    dir = Path.Combine(dir, "04_metrics");
                path = Path.Combine(dir, module + ".json");
            }
            JObject existing = null;
            if (merge && path != null && File.Exists(path))
            {
                try { existing = JObject.Parse(File.ReadAllText(path)); }
                catch (Exception ex) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Existing " + module + ".json could not be read and will be replaced: " + ex.Message); }
            }

            string version = typeof(ReportMetricsExport).Assembly.GetName().Version?.ToString(3) ?? "";
            var j = ReportMetrics.Build(module, version, DateTime.Today, metrics, rules, sources, maps, caps, conf, existing);
            string text = j.ToString(Newtonsoft.Json.Formatting.Indented);

            string written = "";
            if (write)
            {
                if (path == null) throw new ArgumentException("Set Folder to write the file.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
                written = path;
            }

            var inv = CultureInfo.InvariantCulture;
            var card = new List<string>();
            int reds = 0, ambers = 0;
            foreach (JObject f in (JArray)j["flags"])
            {
                string r = f["rating"]?.ToString();
                if (r == "red") reds++; else if (r == "amber") ambers++;
                string v = f["value"]?.Type == JTokenType.Null ? "missing" : ((double)f["value"]).ToString("0.##", inv);
                card.Add(f["theme"] + ": " + (r ?? "").ToUpperInvariant() + " (" + f["metric"] + " = " + v + "; amber " + f["threshold"]?["amber"] + ", red " + f["threshold"]?["red"] + ")");
            }
            var mlist = ((JArray)j["metrics"]).Select(m => m["key"] + " = " + ((double)m["value"]).ToString("0.###", inv) + (string.IsNullOrEmpty(m["unit"]?.ToString()) ? "" : " " + m["unit"]) + (m["scope"]?.ToString() == "site" ? "" : " [" + m["scope"] + "]")).ToList();
            foreach (var r in rules)
                if (!((JArray)j["metrics"]).Any(m => m["key"]?.ToString() == r.Key))
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Flag '" + r.Theme + "' uses metric '" + r.Key + "', which is not in this module. See the Metrics output for available keys.");

            DA.SetData(0, text);
            DA.SetDataList(1, card);
            DA.SetDataList(2, mlist);
            DA.SetData(3, written);
            DA.SetData(4, InfoText);

            var lines = new List<string> { Hud.Metric("Module", module), Hud.Metric("Metrics", mlist.Count) };
            if (card.Count > 0) lines.Add(Hud.Metric("Flags", reds + " red, " + ambers + " amber"));
            lines.Add(Hud.Metric("File", write ? "written" : "not written"));
            Hud.Done(this, sw, lines.ToArray());
        }

        private const string InfoText =
            "REPORT METRICS\n==============\n\n" +
            "HOW IT WORKS:\nBuilds one module file of the Deep Site Analysis report (e.g. 04_metrics/C1.json) in the shared schema: module, version, run_date, inputs, metrics (key, value, unit, scope), flags (theme, green/amber/red, thresholds, value), maps and confidence.\n\n" +
            "WHERE THE NUMBERS COME FROM:\n- Dashboard Data from any Enzyme analysis component (Fast CFD, Analysis Heatmap, Analysis Difference, Noise...). Each metric gets a key from its name, with the prefix you give, e.g. 'slope' + '15-30% costly' -> slope_15_30pct_costly_pct. Analysis Heatmap also adds the area of every band in ha.\n- Your own Keys / Values / Units for anything else (elevation range, cut and fill, retention volume).\n\n" +
            "FLAGS:\nEach flag rates one metric. If Red > Amber, higher is worse (e.g. % of site over 15% slope: amber 20, red 40). If Red < Amber, lower is worse (e.g. comfort area %: amber 60, red 40).\n\n" +
            "MERGE:\nSeveral definitions can write to the same module: with Merge on, metrics, flags, maps and sources already in the file are kept and only this run's keys are replaced. The plugin version is recorded for the Data & methods chapter (A3). Areas assume the model is in metres.";
    }
}
