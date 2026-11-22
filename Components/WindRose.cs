using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using Enzyme.Utils;
using Grasshopper.Kernel;
using Rhino.Display;
using Rhino.Geometry;

namespace Enzyme.Components
{
    /// <summary>
    /// Wind rose drawn in the Rhino viewport (on site and/or as a screen HUD) from an EPW file or raw
    /// direction/speed lists, with a 0-360 selector that outputs direction, speed and frequency
    /// (e.g. straight into Fast CFD's Direction and WindSpeed inputs).
    /// </summary>
    public class WindRose : EnzymeComponent
    {
        public WindRose()
          : base("Wind Rose", "WindRose",
              "Wind rose from an EPW weather file (or direction/speed lists), drawn in the Rhino viewport on site and/or as a screen HUD. A 0-360 selector picks a direction and outputs its speed and frequency, ready for Fast CFD. Directions are meteorological: where the wind blows FROM, 0 = North, clockwise.",
              "Enzyme", "Site Analysis")
        { }

        protected override bool UsesRunGate => true;

        // ---------------- Parameters ----------------

        const int I_EPW = 0, I_DIRS = 1, I_SPDS = 2, I_SEL = 3, I_SECT = 4, I_CLASSES = 5, I_STAT = 6, I_MONTHS = 7, I_HOURS = 8,
                  I_PLANE = 9, I_RADIUS = 10, I_NORTH = 11, I_COLORS = 12, I_DISPLAY = 13, I_HUDSIZE = 14, I_ANCHOR = 15,
                  I_TEXT = 16, I_CALM = 17, I_RUN = 18;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("EPW", "EPW", "Path to an EPW weather file (e.g. from climate.onebuilding.org).", GH_ParamAccess.item);
            pManager.AddNumberParameter("Directions", "Dirs", "Alternative to EPW: wind directions in degrees (FROM, 0 = N, clockwise).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Speeds", "Speeds", "Alternative to EPW: wind speeds (m/s), same length as Directions.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Selector", "Sel", "Direction to inspect, 0-360 degrees (FROM, 0 = N). Drives the Direction/Speed/Frequency outputs and the highlight.", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("Sectors", "Sectors", "Number of direction sectors (8, 16, 36...).", GH_ParamAccess.item, 16);
            pManager.AddNumberParameter("Speed Classes", "Classes", "Lower edges of the speed bands in m/s (last band is open-ended).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Speed Stat", "Stat", "Speed output for the selected sector: 0 = Mean, 1 = Median, 2 = P90, 3 = Max.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Months", "Months", "Optional months to include (1-12), e.g. summer only. EPW only.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Hours", "Hours", "Optional hours of day to include (0-23), e.g. 8-20 for daytime. EPW only.", GH_ParamAccess.list);
            pManager.AddPlaneParameter("Plane", "Plane", "Where the rose sits in the model (centre and orientation).", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddNumberParameter("Radius", "Radius", "Rose radius in model units; the outermost frequency ring reaches it (the busiest sector rounded up to the next ring step), so the longest petal is usually a little shorter.", GH_ParamAccess.item, 50.0);
            pManager.AddNumberParameter("North", "North", "Angle of true north from the model +Y axis, degrees counter-clockwise (Ladybug convention).", GH_ParamAccess.item, 0.0);
            pManager.AddColourParameter("Colors", "Colors", "Optional colours for the speed bands (low to high), e.g. from Color Palette.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Display", "Display", "0 = on site (world), 1 = screen HUD, 2 = both.", GH_ParamAccess.item, 2);
            pManager.AddIntegerParameter("HUD Size", "HudSize", "Screen HUD rose diameter in pixels.", GH_ParamAccess.item, 220);
            pManager.AddIntegerParameter("HUD Anchor", "Anchor", "Screen HUD corner: 0 = top left, 1 = top right, 2 = bottom left, 3 = bottom right.", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Text Size", "TextSize", "Label size in pixels (viewport labels and HUD).", GH_ParamAccess.item, 12.0);
            pManager.AddNumberParameter("Calm", "Calm", "Speeds below this (m/s) count as calm and are left out of the sectors.", GH_ParamAccess.item, 0.5);
            pManager.AddBooleanParameter("Run", "Run", "Optional. False = keep showing the last rose (cached). Share one toggle with the Analysis Dashboard to switch everything on/off together.", GH_ParamAccess.item, true);
            for (int i = 0; i < pManager.ParamCount; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Direction", "Dir", "Selected direction in degrees (FROM, 0 = N), as given by Selector — plug into Fast CFD Direction. Speed and Frequency are the statistics of the whole sector containing it.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Speed", "Speed", "Wind speed of the selected sector (m/s, statistic chosen by Speed Stat) — plug into Fast CFD WindSpeed.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Frequency", "Freq", "Share of all hours the wind blows from the selected sector (%).", GH_ParamAccess.item);
            pManager.AddMeshParameter("Rose", "Rose", "Coloured rose mesh (bakeable).", GH_ParamAccess.item);
            pManager.AddCurveParameter("Guides", "Guides", "Frequency rings, compass axes, selected-sector outline and wind arrow (bakeable).", GH_ParamAccess.list);
            pManager.AddPointParameter("Label Points", "LabelPts", "Positions for the labels (bake with Text Tag / text).", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Labels", "Label text matching Label Points.", GH_ParamAccess.list);
            pManager.AddTextParameter("Sector Table", "Table", "Frequency (%) per sector and speed band, tab-separated.", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON for the Analysis Dashboard.", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Source, period, calm share and how to read the rose.", GH_ParamAccess.item);
        }

        // ---------------- State for drawing ----------------

        Mesh _rose;
        readonly List<Curve> _guides = new List<Curve>();
        Polyline _highlight;
        Line _arrow = Line.Unset;
        readonly List<Point3d> _labelPts = new List<Point3d>();
        readonly List<string> _labels = new List<string>();
        int _display = 2, _hudSize = 220, _anchor = 1;
        float _textPx = 12f;
        Bitmap _hudBitmap;
        DisplayBitmap _hudDisplay;
        BoundingBox _bbox = BoundingBox.Empty;

        static readonly Color[] DefaultBands =
        {
            Color.FromArgb(198, 219, 239), Color.FromArgb(107, 174, 214), Color.FromArgb(33, 113, 181),
            Color.FromArgb(65, 171, 93), Color.FromArgb(254, 196, 79), Color.FromArgb(217, 72, 1), Color.FromArgb(128, 0, 38)
        };

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            ClearDrawing();

            // ---- Inputs ----
            string epw = null; DA.GetData(I_EPW, ref epw);
            var dirsIn = new List<double>(); DA.GetDataList(I_DIRS, dirsIn);
            var spdsIn = new List<double>(); DA.GetDataList(I_SPDS, spdsIn);
            double sel = 0; DA.GetData(I_SEL, ref sel);
            int nSect = 16; DA.GetData(I_SECT, ref nSect);
            var classes = new List<double>(); DA.GetDataList(I_CLASSES, classes);
            int stat = 0; DA.GetData(I_STAT, ref stat);
            var months = new List<int>(); DA.GetDataList(I_MONTHS, months);
            var hours = new List<int>(); DA.GetDataList(I_HOURS, hours);
            Plane plane = Plane.WorldXY; DA.GetData(I_PLANE, ref plane);
            double radius = 50; DA.GetData(I_RADIUS, ref radius);
            double north = 0; DA.GetData(I_NORTH, ref north);
            var colors = new List<Color>(); DA.GetDataList(I_COLORS, colors);
            DA.GetData(I_DISPLAY, ref _display);
            DA.GetData(I_HUDSIZE, ref _hudSize);
            DA.GetData(I_ANCHOR, ref _anchor);
            double textPx = 12; DA.GetData(I_TEXT, ref textPx); _textPx = (float)Math.Max(6, textPx);
            double calm = 0.5; DA.GetData(I_CALM, ref calm);

            nSect = Math.Max(4, Math.Min(72, nSect));
            if (radius <= 0) radius = 50;
            _hudSize = Math.Max(100, Math.Min(800, _hudSize));
            if (classes.Count == 0) classes = new List<double> { 0, 2, 4, 6, 8, 10 };
            classes = classes.Distinct().OrderBy(c => c).ToList();
            int nBands = classes.Count;
            if (colors.Count == 0) colors = Enumerable.Range(0, nBands).Select(i => DefaultBands[Math.Min(DefaultBands.Length - 1, (int)Math.Round(i * (DefaultBands.Length - 1) / (double)Math.Max(1, nBands - 1)))]).ToList();
            Color BandColor(int b) => colors.Count == 1 ? colors[0] : LerpColors(colors, nBands == 1 ? 0 : b / (double)(nBands - 1));

            // ---- Data ----
            var dirs = new List<double>(); var spds = new List<double>();
            string source;
            if (!string.IsNullOrWhiteSpace(epw))
            {
                if (!File.Exists(epw)) { Hud.Error(this, "EPW file not found"); return; }
                string err = ReadEpw(epw, months, hours, dirs, spds, out source);
                if (err != null) { Hud.Error(this, err); return; }
            }
            else if (dirsIn.Count > 0 && spdsIn.Count > 0)
            {
                if (dirsIn.Count != spdsIn.Count) { Hud.Error(this, "Directions and Speeds must have the same length"); return; }
                for (int i = 0; i < dirsIn.Count; i++)
                    if (!double.IsNaN(dirsIn[i]) && !double.IsNaN(spdsIn[i]) && spdsIn[i] >= 0) { dirs.Add(dirsIn[i]); spds.Add(spdsIn[i]); }
                source = "Direction/speed lists";
            }
            else { Hud.Waiting(this, "EPW or wind data"); return; }

            if (dirs.Count == 0) { Hud.Waiting(this, "hours matching the filters"); return; }

            // ---- Binning: sector x speed band (calm excluded) ----
            double width = 360.0 / nSect;
            var counts = new double[nSect, nBands];
            var sectorSpeeds = new List<double>[nSect];
            for (int s = 0; s < nSect; s++) sectorSpeeds[s] = new List<double>();
            int calmCount = 0, total = dirs.Count;
            for (int i = 0; i < total; i++)
            {
                if (spds[i] < calm) { calmCount++; continue; }
                int s = SectorOf(dirs[i], width, nSect);
                int b = 0;
                for (int k = nBands - 1; k >= 0; k--) if (spds[i] >= classes[k]) { b = k; break; }
                counts[s, b]++;
                sectorSpeeds[s].Add(spds[i]);
            }
            var freq = new double[nSect, nBands];      // % of all hours
            var sectorTotal = new double[nSect];
            for (int s = 0; s < nSect; s++)
                for (int b = 0; b < nBands; b++) { freq[s, b] = 100.0 * counts[s, b] / total; sectorTotal[s] += freq[s, b]; }
            double maxSector = sectorTotal.Max();
            double calmPct = 100.0 * calmCount / total;
            int prevailing = Array.IndexOf(sectorTotal, maxSector);

            // ---- Selection ----
            double selDeg = WindMath.Normalize(sel);
            int selS = SectorOf(selDeg, width, nSect);
            double selSpeed = Stat(sectorSpeeds[selS], stat);
            double selFreq = sectorTotal[selS];
            double selOff = Math.Abs(selDeg - selS * width) % 360.0;
            if (Math.Min(selOff, 360.0 - selOff) > 0.01)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Selector {selDeg.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}° is not a sector centre: Speed and Frequency are for the sector centred at {(selS * width).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}°, while Direction keeps {selDeg.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}°.");

            // ---- Geometry (model space) ----
            double ringStep = NiceStep(maxSector);
            double scaleMax = Math.Max(ringStep, Math.Ceiling(maxSector / ringStep) * ringStep);
            double r0 = radius * 0.12;                                         // calm circle
            Func<double, double> R = pct => r0 + (radius - r0) * pct / scaleMax;
            Func<double, double, Point3d> P = (bearing, r) =>
            {
                Vector3d v = WindMath.BearingToModel(bearing, north);
                return plane.PointAt(v.X * r, v.Y * r);
            };

            var mesh = new Mesh();
            int arcSeg = Math.Max(2, 96 / nSect);
            double half = width * 0.45;                                         // 10% gap between sectors
            for (int s = 0; s < nSect; s++)
            {
                double c = s * width, cum = 0;
                for (int b = 0; b < nBands; b++)
                {
                    if (freq[s, b] <= 0) continue;
                    double rin = R(cum), rout = R(cum + freq[s, b]); cum += freq[s, b];
                    AddWedge(mesh, P, c - half, c + half, rin, rout, arcSeg, BandColor(b));
                }
            }
            mesh.Normals.ComputeNormals();
            mesh.Compact();
            _rose = mesh;

            // rings, axes
            for (double f = ringStep; f <= scaleMax + 1e-9; f += ringStep)
            {
                _guides.Add(RingCurve(P, R(f)));
                _labelPts.Add(P(width * 0.5, R(f))); _labels.Add($"{f:0.#}%");      // ring labels in the gap east of N
            }
            _guides.Insert(0, RingCurve(P, r0));
            for (int q = 0; q < 4; q++)
            {
                double bng = q * 90.0;
                _guides.Add(new LineCurve(P(bng, r0), P(bng, radius * 1.05)));
                _labelPts.Add(P(bng, radius * 1.15)); _labels.Add(new[] { "N", "E", "S", "W" }[q]);
            }

            // selected sector highlight + arrow (wind comes FROM the selector bearing towards the centre)
            double hr = Math.Max(R(sectorTotal[selS]), r0 * 1.5);
            var hl = new Polyline();
            hl.Add(P(selS * width - half, r0));
            for (int k = 0; k <= arcSeg; k++) hl.Add(P(selS * width - half + 2 * half * k / arcSeg, hr));
            hl.Add(P(selS * width + half, r0));
            for (int k = arcSeg; k >= 0; k--) hl.Add(P(selS * width - half + 2 * half * k / arcSeg, r0));
            _highlight = hl;
            _guides.Add(hl.ToPolylineCurve());
            _arrow = new Line(P(selDeg, radius * 1.35), P(selDeg, r0 * 0.6));
            _guides.Add(new LineCurve(_arrow));
            string selLabel = $"{WindMath.CompassName(selDeg)} {selDeg:0}° · {selSpeed:0.0} m/s · {selFreq:0.0}%";
            _labelPts.Add(P(selDeg, radius * 1.45)); _labels.Add(selLabel);

            _bbox = mesh.GetBoundingBox(true);
            foreach (var p in _labelPts) _bbox.Union(p);
            _bbox.Union(_arrow.From);

            // screen HUD bitmap
            if (_display >= 1)
            {
                _hudBitmap?.Dispose();
                _hudBitmap = RenderHud(freq, sectorTotal, nSect, nBands, BandColor, classes, scaleMax, ringStep, selS, selDeg, selLabel, calmPct, source);
                _hudDisplay = new DisplayBitmap(_hudBitmap);
            }

            // ---- Outputs ----
            DA.SetData(0, selDeg);
            DA.SetData(1, selSpeed);
            DA.SetData(2, selFreq);
            DA.SetData(3, _rose);
            DA.SetDataList(4, _guides);
            DA.SetDataList(5, _labelPts);
            DA.SetDataList(6, _labels);
            DA.SetData(7, SectorTable(freq, sectorTotal, nSect, nBands, classes, width));
            DA.SetData(8, DashboardJson(classes, nBands, BandColor, prevailing, width, sectorTotal, selLabel, calmPct));
            DA.SetData(9, ($"WIND ROSE\n\nSource: {source}\nHours used: {total:N0}\nCalm (< {calm:0.0} m/s): {calmPct:0.0}%\n" +
                          $"Prevailing: {WindMath.CompassName(prevailing * width)} ({prevailing * width:0}°) {maxSector:0.0}%\n\n" +
                          "HOW TO READ: each petal shows where the wind blows FROM; its length is the share of hours, colours split it by speed band. " +
                          "Rings mark % of hours. Directions are meteorological (FROM, 0 = N, clockwise) and rotated by North into the model.\n\n" +
                          "DATA: EPW files are typical years from weather stations (often airports). Check the station is representative of the site " +
                          "(distance, elevation, coast or valley effects).") + "\n\n" + Enzyme.Utils.MethodNotes.Info("WindRose"));

            Hud.Done(this, sw,
                Hud.Metric("Hours", total),
                $"Prevailing: {WindMath.CompassName(prevailing * width)} {maxSector:0.0}%",
                $"Selected: {WindMath.CompassName(selDeg)} {selDeg:0}°",
                $"Speed: {selSpeed:0.0} m/s | Freq: {selFreq:0.0}%",
                Hud.Metric("Calm", calmPct, "%"));
        }

        // ---------------- Data helpers ----------------

        static int SectorOf(double deg, double width, int n) => (int)Math.Floor(WindMath.Normalize(deg + width / 2) / width) % n;

        /// <summary>Reads wind direction (field 21) and speed (field 22) from an EPW. Returns an error or null.</summary>
        static string ReadEpw(string path, List<int> months, List<int> hours, List<double> dirs, List<double> spds, out string source)
        {
            source = Path.GetFileName(path);
            string[] lines;
            try { lines = File.ReadAllLines(path); } catch (Exception ex) { return "Cannot read EPW: " + ex.Message; }
            if (lines.Length < 9 || !lines[0].StartsWith("LOCATION", StringComparison.OrdinalIgnoreCase)) return "Not an EPW file (no LOCATION header)";
            var loc = lines[0].Split(',');
            if (loc.Length > 7) source = $"{loc[1].Trim()}, {loc[3].Trim()} ({loc[6].Trim()}, {loc[7].Trim()}) — {Path.GetFileName(path)}";

            var mset = new HashSet<int>(months); var hset = new HashSet<int>(hours);
            var inv = CultureInfo.InvariantCulture;
            for (int i = 8; i < lines.Length; i++)
            {
                var f = lines[i].Split(',');
                if (f.Length < 22) continue;
                if (!int.TryParse(f[1], out int month) || !int.TryParse(f[3], out int hour)) continue;
                if (mset.Count > 0 && !mset.Contains(month)) continue;
                if (hset.Count > 0 && !hset.Contains((hour - 1 + 24) % 24)) continue;     // EPW hour 1-24 = hour ending
                if (!double.TryParse(f[20], NumberStyles.Float, inv, out double d) || !double.TryParse(f[21], NumberStyles.Float, inv, out double v)) continue;
                if (d > 360 || v >= 999 || v < 0) continue;                                // missing-value codes
                dirs.Add(d); spds.Add(v);
            }
            return dirs.Count == 0 && mset.Count == 0 && hset.Count == 0 ? "EPW has no valid wind records" : null;
        }

        static double Stat(List<double> v, int mode)
        {
            if (v.Count == 0) return 0;
            if (mode == 0) return v.Average();
            if (mode == 3) return v.Max();
            var s = v.OrderBy(x => x).ToList();
            double q = mode == 2 ? 0.9 : 0.5;
            double pos = q * (s.Count - 1); int lo = (int)Math.Floor(pos); int hi = Math.Min(lo + 1, s.Count - 1);
            return s[lo] + (s[hi] - s[lo]) * (pos - lo);
        }

        static double NiceStep(double max)
        {
            double raw = Math.Max(max, 1e-6) / 4.0;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            foreach (double m in new[] { 1, 2, 2.5, 5, 10 }) if (m * mag >= raw) return m * mag;
            return 10 * mag;
        }

        static Color LerpColors(List<Color> g, double t)
        {
            t = Math.Max(0, Math.Min(1, t)) * (g.Count - 1);
            int i = Math.Min((int)Math.Floor(t), g.Count - 2); double u = t - i;
            Color a = g[i], b = g[i + 1];
            return Color.FromArgb((int)(a.R + (b.R - a.R) * u), (int)(a.G + (b.G - a.G) * u), (int)(a.B + (b.B - a.B) * u));
        }

        static void AddWedge(Mesh m, Func<double, double, Point3d> P, double a0, double a1, double rin, double rout, int seg, Color c)
        {
            int start = m.Vertices.Count;
            for (int k = 0; k <= seg; k++)
            {
                double a = a0 + (a1 - a0) * k / seg;
                m.Vertices.Add(P(a, rin)); m.VertexColors.Add(c);
                m.Vertices.Add(P(a, rout)); m.VertexColors.Add(c);
            }
            for (int k = 0; k < seg; k++)
            {
                int i0 = start + 2 * k;
                m.Faces.AddFace(i0, i0 + 2, i0 + 3, i0 + 1);   // ordered so the face points up in the rose plane
            }
        }

        static Curve RingCurve(Func<double, double, Point3d> P, double r)
        {
            var pl = new Polyline();
            for (int k = 0; k <= 96; k++) pl.Add(P(360.0 * k / 96, r));
            return pl.ToPolylineCurve();
        }

        static string SectorTable(double[,] freq, double[] tot, int nS, int nB, List<double> classes, double width)
        {
            var sb = new System.Text.StringBuilder("Sector\tFrom°\tTotal%");
            for (int b = 0; b < nB; b++) sb.Append('\t').Append(BandLabel(classes, b));
            for (int s = 0; s < nS; s++)
            {
                sb.Append('\n').Append(WindMath.CompassName(s * width)).Append('\t').Append((s * width).ToString("0.#", CultureInfo.InvariantCulture))
                  .Append('\t').Append(tot[s].ToString("0.00", CultureInfo.InvariantCulture));
                for (int b = 0; b < nB; b++) sb.Append('\t').Append(freq[s, b].ToString("0.00", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        static string BandLabel(List<double> classes, int b)
            => b + 1 < classes.Count ? $"{classes[b]:0.#}-{classes[b + 1]:0.#} m/s" : $"{classes[b]:0.#}+ m/s";

        static string DashboardJson(List<double> classes, int nB, Func<int, Color> col, int prevailing, double width, double[] tot, string selLabel, double calmPct)
        {
            var j = new Newtonsoft.Json.Linq.JObject
            {
                ["Title"] = "WIND ROSE",
                ["Type"] = "Discrete",
                ["Colors"] = new Newtonsoft.Json.Linq.JArray(Enumerable.Range(0, nB).Select(b => new Newtonsoft.Json.Linq.JObject { ["R"] = col(b).R, ["G"] = col(b).G, ["B"] = col(b).B })),
                ["Labels"] = new Newtonsoft.Json.Linq.JArray(Enumerable.Range(0, nB).Select(b => BandLabel(classes, b))),
                ["Metrics"] = new Newtonsoft.Json.Linq.JArray
                {
                    new Newtonsoft.Json.Linq.JObject { ["Name"] = "Prevailing", ["Value"] = $"{WindMath.CompassName(prevailing * width)} {tot[prevailing]:0.0}%" },
                    new Newtonsoft.Json.Linq.JObject { ["Name"] = "Selected", ["Value"] = selLabel },
                    new Newtonsoft.Json.Linq.JObject { ["Name"] = "Calm", ["Value"] = $"{calmPct:0.0}%" }
                }
            };
            return j.ToString(Newtonsoft.Json.Formatting.None);
        }

        // ---------------- Screen HUD (GDI+ bitmap, true north up) ----------------

        Bitmap RenderHud(double[,] freq, double[] tot, int nS, int nB, Func<int, Color> col, List<double> classes,
                         double scaleMax, double ringStep, int selS, double selDeg, string selLabel, double calmPct, string source)
        {
            float px = _textPx, d = _hudSize, pad = px;
            float legendH = nB * (px * 1.4f) + px * 0.5f;
            int W = (int)(d + 2 * pad + px * 2), H = (int)(px * 1.8f + d + px * 3.5f + legendH + 2 * pad);
            var bmp = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var font = new Font("Arial", px, GraphicsUnit.Pixel))
            using (var bold = new Font("Arial", px * 1.1f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var white = new SolidBrush(Color.White))
            using (var grey = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var bg = new SolidBrush(Color.FromArgb(215, 25, 25, 25))) g.FillRectangle(bg, 0, 0, W, H);

                g.DrawString("WIND ROSE", bold, white, pad, pad);
                float cx = W / 2f, cy = pad + px * 1.8f + px * 1.2f + d / 2f, R = d / 2f, r0 = R * 0.12f;
                Func<double, float> rad = pct => (float)(r0 + (R - r0) * pct / scaleMax);
                Func<double, double, PointF> pt = (bearing, r) =>
                {
                    double a = bearing * Math.PI / 180.0;
                    return new PointF(cx + (float)(Math.Sin(a) * r), cy - (float)(Math.Cos(a) * r));
                };

                for (double f = ringStep; f <= scaleMax + 1e-9; f += ringStep)
                {
                    float rr = rad(f); g.DrawEllipse(grey, cx - rr, cy - rr, 2 * rr, 2 * rr);
                }
                double width = 360.0 / nS, half = width * 0.45;
                for (int s = 0; s < nS; s++)
                {
                    double cum = 0;
                    for (int b = 0; b < nB; b++)
                    {
                        if (freq[s, b] <= 0) continue;
                        float rin = rad(cum), rout = rad(cum + freq[s, b]); cum += freq[s, b];
                        using (var path = WedgePath(pt, s * width - half, s * width + half, rin, rout))
                        using (var br = new SolidBrush(col(b))) g.FillPath(br, path);
                    }
                }
                using (var hp = new Pen(Color.White, 2f))
                using (var path = WedgePath(pt, selS * width - half, selS * width + half, r0, Math.Max(rad(tot[selS]), r0 * 1.5f)))
                    g.DrawPath(hp, path);
                using (var ap = new Pen(Color.White, 2f) { CustomEndCap = new AdjustableArrowCap(4, 5) })
                    g.DrawLine(ap, pt(selDeg, R + px * 0.9), pt(selDeg, r0 * 0.6));
                g.DrawEllipse(grey, cx - r0, cy - r0, 2 * r0, 2 * r0);
                var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString($"{calmPct:0}%", font, white, cx, cy, cf);
                string[] nesw = { "N", "E", "S", "W" };
                for (int q = 0; q < 4; q++) g.DrawString(nesw[q], bold, white, pt(q * 90, R + px * 0.9), cf);

                float y = cy + R + px * 1.6f;
                g.DrawString(selLabel, bold, white, cx, y, cf);
                y += px * 1.4f;
                for (int b = 0; b < nB; b++)
                {
                    using (var br = new SolidBrush(col(b))) g.FillRectangle(br, pad, y, px * 1.6f, px);
                    g.DrawString(BandLabel(classes, b), font, white, pad + px * 2.1f, y - px * 0.1f);
                    y += px * 1.4f;
                }
            }
            return bmp;
        }

        static GraphicsPath WedgePath(Func<double, double, PointF> pt, double a0, double a1, float rin, float rout)
        {
            var path = new GraphicsPath();
            const int seg = 8;
            var pts = new List<PointF>();
            for (int k = 0; k <= seg; k++) pts.Add(pt(a0 + (a1 - a0) * k / seg, rout));
            for (int k = seg; k >= 0; k--) pts.Add(pt(a0 + (a1 - a0) * k / seg, rin));
            path.AddPolygon(pts.ToArray());
            return path;
        }

        // ---------------- Viewport drawing ----------------

        void ClearDrawing()
        {
            _rose = null; _guides.Clear(); _labelPts.Clear(); _labels.Clear();
            _highlight = null; _arrow = Line.Unset; _bbox = BoundingBox.Empty;
        }

        public override BoundingBox ClippingBox => _display != 1 && _bbox.IsValid ? _bbox : base.ClippingBox;

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            base.DrawViewportMeshes(args);
            if (Hidden || Locked || _display == 1 || _rose == null) return;
            args.Display.DrawMeshFalseColors(_rose);
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (Hidden || Locked) return;
            if (_display != 1 && _rose != null)
            {
                var guide = Color.FromArgb(150, 90, 90, 90);
                foreach (var c in _guides) args.Display.DrawCurve(c, guide, 1);
                if (_highlight != null && _highlight.IsValid) args.Display.DrawPolyline(_highlight, Color.Black, 3);
                if (_arrow.IsValid) args.Display.DrawArrow(_arrow, Color.Black);
                for (int i = 0; i < _labelPts.Count; i++)
                    args.Display.Draw2dText(_labels[i], Color.Black, _labelPts[i], true, (int)_textPx);
            }
            if (_display >= 1 && _hudDisplay != null && _hudBitmap != null)
            {
                var vp = args.Viewport.Bounds;
                int w = _hudBitmap.Width, h = _hudBitmap.Height, m = 20;
                int x = (_anchor == 1 || _anchor == 3) ? vp.Width - w - m : m;
                int y = (_anchor == 2 || _anchor == 3) ? vp.Height - h - m : m;
                args.Display.DrawBitmap(_hudDisplay, x, y);
            }
        }

        // ---------------- Auto-wire ----------------

        protected override void AutoWire(GH_Document document)
        {
            AutoWireHelper.WireFilePath(this, document, I_EPW, "", 260, 0);
            AutoWireHelper.WireIntegerSlider(this, document, I_SEL, 0, 360, 0, 260, 60);
            AutoWireHelper.WireValueList(this, document, I_STAT, new[] { "Mean", "Median", "P90", "Max" }, new[] { "0", "1", "2", "3" }, 260, 120);
            AutoWireHelper.WireValueList(this, document, I_DISPLAY, new[] { "On site", "Screen HUD", "Both" }, new[] { "0", "1", "2" }, 260, 260);
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc != null) { AutoWire(doc); ExpireSolution(true); }
            });
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            _hudBitmap?.Dispose(); _hudBitmap = null; _hudDisplay = null;
            base.RemovedFromDocument(document);
        }

        protected override Bitmap Icon => IconLoader.Load("WindRose.png");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("6E1D9A3B-5C47-4F2E-9B8D-21A7C0E4F913");
    }
}
