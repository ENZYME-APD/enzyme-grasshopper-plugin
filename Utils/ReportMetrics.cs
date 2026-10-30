using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Enzyme.Utils
{
    /// <summary>One metric line of a module file: key, value, unit, scope (site / buffer).</summary>
    public sealed class MetricItem
    {
        public string Key;
        public double Value;
        public string Unit;
        public string Scope = "site";
        public string Label;
    }

    /// <summary>A scorecard flag rule: theme rated green / amber / red on one metric.</summary>
    public sealed class FlagRule
    {
        public string Theme;
        public string Key;
        public double Amber;
        public double Red;
        /// <summary>True when higher values are worse (red > amber); false when lower is worse.</summary>
        public bool HigherIsWorse => Red >= Amber;
    }

    /// <summary>
    /// Builds the per-module metrics file of the Deep Site Analysis report
    /// (module, version, run_date, inputs, metrics, flags, maps, confidence) from Grasshopper outputs,
    /// so the report pipeline reads plugin results without retyping. Rhino-free, unit-tested.
    /// </summary>
    public static class ReportMetrics
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Regex Num = new Regex(@"^\s*([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*(.*)$");

        /// <summary>snake_case key from a label: "Comfort (5.0 m/s)" -> "comfort_5_0_m_s".</summary>
        public static string Slug(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "metric";
            var sb = new StringBuilder();
            foreach (char ch in s.Trim().ToLowerInvariant())
            {
                if (ch == '%') sb.Append("pct");
                else if (ch == '>') sb.Append("over_");
                else if (ch == '<') sb.Append("under_");
                else if (char.IsLetterOrDigit(ch) && ch < 128) sb.Append(ch);
                else sb.Append('_');
            }
            string k = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
            return k.Length == 0 ? "metric" : k;
        }

        /// <summary>Parses "12.3%", "5.2 m/s", "+0.40" into value + unit. False if no leading number.</summary>
        public static bool TryParseValue(string text, out double value, out string unit)
        {
            value = double.NaN; unit = "";
            if (string.IsNullOrWhiteSpace(text)) return false;
            var m = Num.Match(text.Replace(",", ""));
            if (!m.Success) return false;
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, Inv, out value)) return false;
            unit = m.Groups[2].Value.Trim();
            return true;
        }

        /// <summary>
        /// Reads the Metrics array (and BandAreas, when present) of an Enzyme "Dashboard Data" JSON.
        /// Keys are prefixed (e.g. "wind_") so several components can share one module file.
        /// Areas are converted from model units² to ha (model in metres).
        /// </summary>
        public static List<MetricItem> FromDashboard(string json, string prefix)
        {
            var o = new List<MetricItem>();
            if (string.IsNullOrWhiteSpace(json)) return o;
            JObject j;
            try { j = JObject.Parse(json); } catch { return o; }
            string pre = string.IsNullOrWhiteSpace(prefix) ? "" : Slug(prefix) + "_";
            var metrics = j["Metrics"] as JArray;
            if (metrics != null)
                foreach (var m in metrics)
                {
                    string name = m["Name"]?.ToString();
                    double v; string unit;
                    if (string.IsNullOrWhiteSpace(name) || !TryParseValue(m["Value"]?.ToString(), out v, out unit)) continue;
                    string key = pre + Slug(name);
                    if (unit == "%" && !key.EndsWith("_pct")) key += "_pct";
                    o.Add(new MetricItem { Key = key, Value = v, Unit = unit, Label = name });
                }
            var bands = j["BandAreas"] as JArray;
            if (bands != null)
                foreach (var b in bands)
                {
                    string name = b["Name"]?.ToString();
                    double area;
                    if (string.IsNullOrWhiteSpace(name) || b["Area"] == null) continue;
                    if (!double.TryParse(b["Area"].ToString(), NumberStyles.Float, Inv, out area)) continue;
                    o.Add(new MetricItem { Key = pre + Slug(name) + "_ha", Value = Math.Round(area / 10000.0, 4), Unit = "ha", Label = name + " (area)" });
                }
            return o;
        }

        /// <summary>green / amber / red for a value against a rule; "n/a" when the metric is missing.</summary>
        public static string Rate(FlagRule r, double v)
        {
            if (double.IsNaN(v)) return "n/a";
            if (r.HigherIsWorse) return v >= r.Red ? "red" : (v >= r.Amber ? "amber" : "green");
            return v <= r.Red ? "red" : (v <= r.Amber ? "amber" : "green");
        }

        /// <summary>Later items replace earlier ones with the same key + scope.</summary>
        public static List<MetricItem> Dedupe(IEnumerable<MetricItem> items)
        {
            var order = new List<string>();
            var map = new Dictionary<string, MetricItem>();
            foreach (var m in items)
            {
                string k = m.Key + "|" + (m.Scope ?? "site");
                if (!map.ContainsKey(k)) order.Add(k);
                map[k] = m;
            }
            return order.Select(k => map[k]).ToList();
        }

        /// <summary>
        /// Builds the module JSON. When existing (a previous file of the same module) is given, its
        /// metrics / flags / maps / inputs are kept and only the entries this run produces are replaced,
        /// so several Grasshopper definitions can contribute to one module.
        /// </summary>
        public static JObject Build(string module, string pluginVersion, DateTime runDate, IList<MetricItem> metrics,
                                    IList<FlagRule> rules, IList<string> inputs, IList<string> maps, IList<string> captions,
                                    string confidence, JObject existing)
        {
            var all = new List<MetricItem>();
            if (existing != null && existing["metrics"] is JArray em)
                foreach (var m in em)
                {
                    double v;
                    if (m["key"] == null || !double.TryParse(m["value"]?.ToString(), NumberStyles.Float, Inv, out v)) continue;
                    all.Add(new MetricItem { Key = m["key"].ToString(), Value = v, Unit = m["unit"]?.ToString() ?? "", Scope = m["scope"]?.ToString() ?? "site", Label = m["label"]?.ToString() });
                }
            all.AddRange(metrics);
            all = Dedupe(all);

            var j = new JObject
            {
                ["module"] = module,
                ["version"] = pluginVersion,
                ["run_date"] = runDate.ToString("yyyy-MM-dd", Inv),
                ["generator"] = "Enzyme Grasshopper plugin " + pluginVersion,
            };

            // inputs: keep previous ones, add new (by text)
            var inArr = new JArray();
            var seenIn = new HashSet<string>();
            if (existing?["inputs"] is JArray ei) foreach (var x in ei) if (seenIn.Add(x.ToString(Newtonsoft.Json.Formatting.None))) inArr.Add(x);
            foreach (var s in inputs ?? new string[0])
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                var parts = s.Split('|').Select(p => p.Trim()).ToArray();
                var io = new JObject { ["dataset"] = parts[0] };
                if (parts.Length > 1) io["version"] = parts[1];
                if (parts.Length > 2) io["resolution"] = parts[2];
                if (parts.Length > 3) io["date"] = parts[3];
                if (seenIn.Add(io.ToString(Newtonsoft.Json.Formatting.None))) inArr.Add(io);
            }
            j["inputs"] = inArr;

            j["metrics"] = new JArray(all.Select(m =>
            {
                var mo = new JObject { ["key"] = m.Key, ["value"] = m.Value, ["unit"] = m.Unit ?? "", ["scope"] = m.Scope ?? "site" };
                if (!string.IsNullOrWhiteSpace(m.Label)) mo["label"] = m.Label;
                return mo;
            }));

            // flags: re-rate every rule (new rules replace old ones with the same theme + key)
            var flagMap = new Dictionary<string, JObject>();
            var flagOrder = new List<string>();
            if (existing?["flags"] is JArray ef)
                foreach (JObject f in ef.OfType<JObject>())
                {
                    string k = f["theme"] + "|" + f["metric"];
                    if (!flagMap.ContainsKey(k)) flagOrder.Add(k);
                    flagMap[k] = f;
                }
            foreach (var r in rules ?? new FlagRule[0])
            {
                var m = all.FirstOrDefault(x => x.Key == r.Key);
                double v = m != null ? m.Value : double.NaN;
                var f = new JObject
                {
                    ["theme"] = r.Theme,
                    ["metric"] = r.Key,
                    ["rating"] = Rate(r, v),
                    ["threshold"] = new JObject { ["amber"] = r.Amber, ["red"] = r.Red, ["direction"] = r.HigherIsWorse ? "higher_is_worse" : "lower_is_worse" },
                    ["value"] = double.IsNaN(v) ? JValue.CreateNull() : new JValue(v)
                };
                string k = r.Theme + "|" + r.Key;
                if (!flagMap.ContainsKey(k)) flagOrder.Add(k);
                flagMap[k] = f;
            }
            j["flags"] = new JArray(flagOrder.Select(k => flagMap[k]));

            var mapArr = new JArray();
            var seenMap = new HashSet<string>();
            if (existing?["maps"] is JArray emp) foreach (var x in emp) if (seenMap.Add(x["file"]?.ToString() ?? "")) mapArr.Add(x);
            for (int i = 0; maps != null && i < maps.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(maps[i])) continue;
                var mo = new JObject { ["file"] = maps[i], ["caption"] = captions != null && i < captions.Count ? captions[i] : "" };
                if (seenMap.Add(maps[i])) mapArr.Add(mo);
                else foreach (JObject x in mapArr.OfType<JObject>()) if (x["file"]?.ToString() == maps[i]) x["caption"] = mo["caption"];
            }
            j["maps"] = mapArr;
            j["confidence"] = string.IsNullOrWhiteSpace(confidence) ? "modelled" : confidence.Trim().ToLowerInvariant();
            return j;
        }
    }
}
