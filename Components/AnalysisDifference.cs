using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    /// <summary>
    /// Option A minus option B on the same analysis grid, with a symmetric diverging scale and
    /// area-weighted statistics for reports ("Option B cuts the area above 5 m/s from 18% to 9%").
    /// </summary>
    public class AnalysisDifference : EnzymeComponent
    {
        public AnalysisDifference()
          : base("Analysis Difference", "Diff",
              "Compares two design options (or two hours) analysed on the same mesh: A - B map on a diverging scale centred on zero, with area-weighted % increased / decreased and the share of area above a level in each option.",
              "Enzyme", "Site Analysis")
        { }

        public override Guid ComponentGuid => new Guid("A41C7E25-3B9F-4D08-8E6A-91D25F0C7B34");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => IconLoader.Load("analysis_difference.png");

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddMeshParameter("Mesh", "Mesh", "Analysis mesh shared by both options (same grid)", GH_ParamAccess.item);
            p.AddNumberParameter("ValuesA", "A", "Values of option A (one per vertex or per face)", GH_ParamAccess.list);
            p.AddNumberParameter("ValuesB", "B", "Values of option B, same count and order as A", GH_ParamAccess.list);
            p.AddNumberParameter("Threshold", "Threshold", "Changes smaller than this count as 'no change' (e.g. 0.5 m/s, 1 dB)", GH_ParamAccess.item, 0.0);
            p.AddNumberParameter("Range", "Range", "Colour scale +-Range (default: largest absolute difference). Use the same Range for every comparison", GH_ParamAccess.item);
            p.AddNumberParameter("Level", "Level", "Optional: report the % of area above this level in A and in B (e.g. 5 m/s comfort limit, 55 dB)", GH_ParamAccess.item);
            p.AddTextParameter("BetterWhen", "BetterWhen", "Lower, Higher or None: names the change as improvement / worsening in the stats", GH_ParamAccess.item, "None");
            p.AddTextParameter("Palette", "Palette", "Diverging palette (Balance, RdBu, Delta, Vlag, PuOr, BrBG...)", GH_ParamAccess.item, "Balance");
            p.AddBooleanParameter("Mask", "Mask", "Optional: one bool per face, True = building / no data", GH_ParamAccess.list);
            for (int i = 3; i < 9; i++) p[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddNumberParameter("Difference", "Diff", "A - B for every vertex / face (feed Analysis Heatmap with Min = -Range, Max = Range for contours)", GH_ParamAccess.list);
            p.AddMeshParameter("Heatmap", "Heatmap", "Difference map on the diverging scale", GH_ParamAccess.item);
            p.AddNumberParameter("Range", "Range", "Symmetric colour range used", GH_ParamAccess.item);
            p.AddTextParameter("Stats", "Stats", "Area-weighted comparison, ready for a report table", GH_ParamAccess.list);
            p.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON legend payload for Legend Geometry / Analysis Dashboard", GH_ParamAccess.item);
            p.AddTextParameter("Info", "Info", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void AutoWire(GH_Document doc)
        {
            string[] k = { "None", "Lower", "Higher" };
            AutoWireHelper.WireValueList(this, doc, 6, k, k.Select(x => "\"" + x + "\"").ToArray(), 200, 60);
        }

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            Mesh mesh = null;
            var a = new List<double>();
            var b = new List<double>();
            if (!DA.GetData(0, ref mesh) || mesh == null || !DA.GetDataList(1, a) || !DA.GetDataList(2, b))
            {
                Hud.Waiting(this, "mesh + A + B");
                return;
            }
            double thr = 0; DA.GetData(3, ref thr);
            double range = double.NaN, level = double.NaN, tmp = 0;
            if (DA.GetData(4, ref tmp)) range = Math.Abs(tmp);
            if (DA.GetData(5, ref tmp)) level = tmp;
            string better = "None"; DA.GetData(6, ref better);
            string pal = "Balance"; DA.GetData(7, ref pal);
            var maskIn = new List<bool>(); DA.GetDataList(8, maskIn);

            if (a.Count != b.Count) throw new ArgumentException("A has " + a.Count + " values, B has " + b.Count + ". Both options must use the same grid.");
            int nv = mesh.Vertices.Count, nf = mesh.Faces.Count;
            bool perVertex = a.Count == nv;
            if (!perVertex && a.Count != nf) throw new ArgumentException(a.Count + " values for " + nv + " vertices / " + nf + " faces.");
            bool[] mask = maskIn.Count == nf ? maskIn.ToArray() : null;
            if (maskIn.Count > 0 && mask == null) throw new ArgumentException("Mask has " + maskIn.Count + " items for " + nf + " faces.");
            thr = Math.Abs(thr);

            int n = a.Count;
            var d = new double[n];
            for (int i = 0; i < n; i++) d[i] = a[i] - b[i];

            // area weight of each value: face area, or a share of the surrounding faces for vertices
            var w = new double[n];
            var valid = new bool[n];
            for (int f = 0; f < nf; f++)
            {
                if (mask != null && mask[f]) continue;
                var mf = mesh.Faces[f];
                double ar = FaceArea(mesh, mf);
                if (perVertex)
                {
                    int k = mf.IsQuad ? 4 : 3;
                    int[] ids = { mf.A, mf.B, mf.C, mf.D };
                    for (int q = 0; q < k; q++) { w[ids[q]] += ar / k; valid[ids[q]] = true; }
                }
                else { w[f] = ar; valid[f] = true; }
            }

            double maxAbs = 0, sumW = 0, sumWD = 0, up = 0, down = 0, same = 0, aAbove = 0, bAbove = 0;
            double maxUp = double.MinValue, maxDown = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!valid[i] || double.IsNaN(d[i])) continue;
                double ad = Math.Abs(d[i]);
                if (ad > maxAbs) maxAbs = ad;
                sumW += w[i]; sumWD += w[i] * d[i];
                if (d[i] > thr) up += w[i]; else if (d[i] < -thr) down += w[i]; else same += w[i];
                if (d[i] > maxUp) maxUp = d[i];
                if (d[i] < maxDown) maxDown = d[i];
                if (!double.IsNaN(level)) { if (a[i] > level) aAbove += w[i]; if (b[i] > level) bAbove += w[i]; }
            }
            if (sumW <= 0) throw new ArgumentException("No valid (unmasked) area to compare.");
            if (double.IsNaN(range) || range <= 0) range = maxAbs > 0 ? maxAbs : 1;

            Color[] stops;
            if (!Palettes.TryGet(pal, out stops))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Unknown palette '" + pal + "' - using Balance.");
                stops = Palettes.Get("Balance");
            }
            Color maskCol = Color.FromArgb(255, 200, 200, 200);
            Func<double, Color> col = v => double.IsNaN(v) ? maskCol : Palettes.Sample(stops, 0.5 + 0.5 * Math.Max(-1, Math.Min(1, v / range)));

            Mesh outMesh;
            if (perVertex)
            {
                // shared vertices carry the gradient; masked faces are appended separately in grey
                outMesh = new Mesh();
                for (int i = 0; i < nv; i++) { outMesh.Vertices.Add(mesh.Vertices[i]); outMesh.VertexColors.Add(valid[i] && !double.IsNaN(d[i]) ? col(d[i]) : maskCol); }
                for (int f = 0; f < nf; f++)
                {
                    var mf = mesh.Faces[f];
                    if (mask != null && mask[f]) continue;
                    if (mf.IsQuad) outMesh.Faces.AddFace(mf.A, mf.B, mf.C, mf.D); else outMesh.Faces.AddFace(mf.A, mf.B, mf.C);
                }
                if (mask != null)
                    for (int f = 0; f < nf; f++)
                    {
                        if (!mask[f]) continue;
                        var mf = mesh.Faces[f];
                        int s0 = outMesh.Vertices.Count, k = mf.IsQuad ? 4 : 3;
                        int[] ids = { mf.A, mf.B, mf.C, mf.D };
                        for (int q = 0; q < k; q++) { outMesh.Vertices.Add(mesh.Vertices[ids[q]]); outMesh.VertexColors.Add(maskCol); }
                        if (k == 4) outMesh.Faces.AddFace(s0, s0 + 1, s0 + 2, s0 + 3); else outMesh.Faces.AddFace(s0, s0 + 1, s0 + 2);
                    }
                outMesh.Compact();
                outMesh.Normals.ComputeNormals();
            }
            else
            {
                outMesh = new Mesh();
                for (int f = 0; f < nf; f++)
                {
                    var mf = mesh.Faces[f];
                    Color c = mask != null && mask[f] ? maskCol : col(d[f]);
                    int s = outMesh.Vertices.Count;
                    int k = mf.IsQuad ? 4 : 3;
                    int[] ids = { mf.A, mf.B, mf.C, mf.D };
                    for (int q = 0; q < k; q++) { outMesh.Vertices.Add(mesh.Vertices[ids[q]]); outMesh.VertexColors.Add(c); }
                    if (k == 4) outMesh.Faces.AddFace(s, s + 1, s + 2, s + 3); else outMesh.Faces.AddFace(s, s + 1, s + 2);
                }
                outMesh.Normals.ComputeNormals();
            }

            var inv = CultureInfo.InvariantCulture;
            string upName = "A higher than B", downName = "A lower than B";
            if (better.Trim().StartsWith("L", StringComparison.OrdinalIgnoreCase)) { upName = "Worse in A (higher)"; downName = "Better in A (lower)"; }
            else if (better.Trim().StartsWith("H", StringComparison.OrdinalIgnoreCase)) { upName = "Better in A (higher)"; downName = "Worse in A (lower)"; }
            var stats = new List<string>
            {
                "Mean change (A - B): " + (sumWD / sumW).ToString("+0.00;-0.00;0.00", inv),
                upName + ": " + (100 * up / sumW).ToString("F1", inv) + "%",
                downName + ": " + (100 * down / sumW).ToString("F1", inv) + "%",
                "No change (within +-" + thr.ToString("0.##", inv) + "): " + (100 * same / sumW).ToString("F1", inv) + "%",
                "Largest increase: " + maxUp.ToString("+0.00;-0.00;0.00", inv),
                "Largest decrease: " + maxDown.ToString("+0.00;-0.00;0.00", inv)
            };
            if (!double.IsNaN(level))
            {
                stats.Add("Area above " + level.ToString("0.##", inv) + " in A: " + (100 * aAbove / sumW).ToString("F1", inv) + "%");
                stats.Add("Area above " + level.ToString("0.##", inv) + " in B: " + (100 * bAbove / sumW).ToString("F1", inv) + "%");
            }

            var j = new JObject
            {
                ["Type"] = "Gradient",
                ["Title"] = "DIFFERENCE (A - B)",
                ["Colors"] = new JArray(stops.Select(c => new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B })),
                ["Labels"] = new JArray((-range).ToString("0.##", inv), "+" + range.ToString("0.##", inv)),
                ["Metrics"] = new JArray(stats.Select(s => { int i = s.LastIndexOf(": ", StringComparison.Ordinal); return new JObject { ["Name"] = s.Substring(0, i), ["Value"] = s.Substring(i + 2) }; }))
            };

            DA.SetDataList(0, d);
            DA.SetData(1, outMesh);
            DA.SetData(2, range);
            DA.SetDataList(3, stats);
            DA.SetData(4, j.ToString(Newtonsoft.Json.Formatting.None));
            DA.SetData(5, InfoText);

            Hud.Done(this, sw,
                Hud.Metric("Mean", (sumWD / sumW).ToString("+0.00;-0.00;0.00", inv)),
                Hud.Metric("Higher", 100 * up / sumW, "%", 1),
                Hud.Metric("Lower", 100 * down / sumW, "%", 1),
                Hud.Metric("Range", "+-" + range.ToString("0.##", inv)));
        }

        private static double FaceArea(Mesh m, MeshFace f)
        {
            Point3d a = m.Vertices[f.A], b = m.Vertices[f.B], c = m.Vertices[f.C];
            double ar = 0.5 * Vector3d.CrossProduct(b - a, c - a).Length;
            if (f.IsQuad) { Point3d d = m.Vertices[f.D]; ar += 0.5 * Vector3d.CrossProduct(c - a, d - a).Length; }
            return ar;
        }

        private const string InfoText =
            "ANALYSIS DIFFERENCE\n===================\n\n" +
            "HOW IT WORKS:\nSubtracts option B from option A cell by cell (both analysed on the same grid) and colours the result on a diverging scale centred on zero: one side shows where A is higher, the other where it is lower.\n\n" +
            "STATS (area-weighted, masked cells excluded):\n- Mean change and the largest increase / decrease.\n- % of the site that got higher, lower, or stayed within Threshold.\n- With Level set: % of the site above that level in each option, e.g. area above the 5 m/s comfort limit, or above 55 dB.\n\n" +
            "TIPS:\n- Set BetterWhen (Lower for wind discomfort or noise, Higher for sun hours) so the stats read as improvements / worsenings.\n- Use the same Range on every comparison so the maps can be compared side by side.\n- For contour bands, feed Difference into Analysis Heatmap with Min = -Range and Max = Range and a diverging palette.";
    }
}
