using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Grasshopper.Kernel;

namespace Enzyme.Utils
{
    /// <summary>
    /// Enzyme HUD standard: the message block drawn under every component.
    ///
    ///   {NickName}
    ///   {time}            e.g. "12.34 ms", "1.20 s", "12.34 ms (cached)"
    ///   ---
    ///   {status / metric lines}   "Label: value", max 6 lines
    ///
    /// Idle states (Awaiting / Off / Error) omit the time line.
    /// See DEVELOPMENT_GUIDE.md, section "HUD Standard".
    /// </summary>
    public static class Hud
    {
        public const string Separator = "---";
        public const int MaxLines = 6;
        public const int MaxLineLength = 40;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------- States ----------

        /// <summary>Solve finished. Lines are metrics, e.g. Hud.Metric("Points", n).</summary>
        public static void Done(GH_Component c, Stopwatch sw, params string[] lines)
            => Done(c, sw.Elapsed.TotalMilliseconds, lines);

        public static void Done(GH_Component c, double ms, params string[] lines)
            => Set(c, FormatTime(ms), lines);

        /// <summary>Outputs re-emitted from cache (Run off / unchanged inputs).</summary>
        public static void Cached(GH_Component c, double ms, params string[] lines)
            => Set(c, FormatTime(ms) + " (cached)", lines);

        /// <summary>Required input missing. what = "data", "payload", "curves"...</summary>
        public static void Waiting(GH_Component c, string what = "data")
            => Set(c, null, "Awaiting " + what);

        /// <summary>Run / Enable toggle is false and nothing is cached.</summary>
        public static void Off(GH_Component c)
            => Set(c, null, "Off");

        /// <summary>Solve failed. Also raise a runtime Error with the full text.</summary>
        public static void Error(GH_Component c, string message, bool addRuntimeMessage = true)
        {
            if (addRuntimeMessage) c.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, message);
            Set(c, null, "Error: " + message);
        }

        // ---------- Formatting helpers ----------

        public static string FormatTime(double ms)
            => ms >= 1000.0
                ? (ms / 1000.0).ToString("F2", Inv) + " s"
                : ms.ToString("F2", Inv) + " ms";

        /// <summary>"Label: value". Integers get thousands separators.</summary>
        public static string Metric(string label, int value) => label + ": " + value.ToString("N0", Inv);
        public static string Metric(string label, long value) => label + ": " + value.ToString("N0", Inv);
        public static string Metric(string label, string value) => label + ": " + value;

        /// <summary>"Label: 12.3 unit" with the given decimals.</summary>
        public static string Metric(string label, double value, string unit = "", int decimals = 1)
        {
            string v = value.ToString("N" + decimals, Inv);
            return label + ": " + (string.IsNullOrEmpty(unit) ? v : v + " " + unit);
        }

        /// <summary>Builds the HUD string without assigning it (useful for caching).</summary>
        public static string Build(string title, string timeLine, IEnumerable<string> lines)
        {
            var sb = new StringBuilder(title ?? "");
            if (!string.IsNullOrEmpty(timeLine)) sb.Append('\n').Append(timeLine);
            sb.Append('\n').Append(Separator);

            int n = 0;
            if (lines != null)
            {
                foreach (var raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    foreach (var part in raw.Split('\n'))
                    {
                        if (n >= MaxLines) break;
                        if (string.IsNullOrWhiteSpace(part)) continue;
                        string line = part.Length > MaxLineLength ? part.Substring(0, MaxLineLength - 1) + "…" : part;
                        sb.Append('\n').Append(line);
                        n++;
                    }
                }
            }
            return sb.ToString();
        }

        static void Set(GH_Component c, string timeLine, params string[] lines)
        {
            if (c == null) return;
            c.Message = Build(c.NickName, timeLine, lines);
        }
    }
}
