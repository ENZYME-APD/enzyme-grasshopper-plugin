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
    /// One visualiser for every scalar analysis (wind, noise, sun hours, comfort, flood depth...):
    /// flat cells, smooth gradient, filled contour bands with isolines, or named classes (UTCI, Lawson).
    /// Labels along the isolines, peak labels, % area per band and a legend payload for Legend / Dashboard.
    /// </summary>
    public class AnalysisHeatmap : EnzymeComponent
    {
        public AnalysisHeatmap()
          : base("Analysis Heatmap", "Heatmap",
              "Colours any analysis mesh from its values: flat cells, smooth gradient, contour bands with labelled isolines, or comfort classes (UTCI, Lawson, noise). Also reports the % of area in each band.",
              "Enzyme", "Site Analysis")
        { }

        public override Guid ComponentGuid => new Guid("6B2E1F4C-8A3D-4E57-9C61-2F0A7D5B3E91");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => IconLoader.Load("analysis_heatmap.png");

        private static readonly string[] Modes = { "Cells", "Smooth", "Contour", "Classes" };

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddMeshParameter("Mesh", "Mesh", "Analysis mesh, e.g. Fast CFD WindMesh (with its VertexSpeeds as Values and BuildingMask as Mask), Sun Hours or Noise mesh", GH_ParamAccess.item);
            p.AddNumberParameter("Values", "Values", "One value per mesh vertex or per mesh face (detected automatically)", GH_ParamAccess.list);
            p.AddTextParameter("Mode", "Mode", "Cells, Smooth, Contour or Classes (0-3 also accepted)", GH_ParamAccess.item, "Contour");
            p.AddIntegerParameter("Levels", "Levels", "Contour: number of bands (rounded to nice steps when Min/Max are not set)", GH_ParamAccess.item, 10);
            p.AddIntegerParameter("Smooth", "Smooth", "Smoothing passes on the values (0 = none, 1-3 light, 5-10 strong). Masked cells are left out", GH_ParamAccess.item, 0);
            p.AddNumberParameter("Min", "Min", "Colour range minimum (default: data minimum). Set the same Min/Max on every option to compare them fairly", GH_ParamAccess.item);
            p.AddNumberParameter("Max", "Max", "Colour range maximum (default: data maximum)", GH_ParamAccess.item);
            p.AddTextParameter("Palette", "Palette", "Palette name (Viridis, Cividis, Turbo, Ladybug, Thermal, Wind, RdBu...). Add ' r' / '_r' to reverse", GH_ParamAccess.item, "Ladybug");
            p.AddColourParameter("Colors", "Colors", "Custom gradient (overrides Palette)", GH_ParamAccess.list);
            p.AddBooleanParameter("Mask", "Mask", "Optional: one bool per face, True = building / no data (kept grey, excluded from smoothing, contours and stats)", GH_ParamAccess.list);
            p.AddTextParameter("Scheme", "Scheme", "Classes mode: UTCI, Lawson or Noise", GH_ParamAccess.item, "UTCI");
            p.AddTextParameter("Title", "Title", "Legend title, e.g. 'Wind speed (m/s)'", GH_ParamAccess.item, "Analysis");
            p.AddNumberParameter("LabelSpacing", "LabelSpacing", "Distance between isoline labels (0 = no labels, default: 15% of the map size)", GH_ParamAccess.item);
            p.AddNumberParameter("LabelSize", "LabelSize", "Label text height (default: 1.2% of the map size)", GH_ParamAccess.item);
            p.AddIntegerParameter("Peaks", "Peaks", "Number of high-point labels (0 = none). Negative = label low points instead", GH_ParamAccess.item, 0);
            for (int i = 2; i < 15; i++) p[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddMeshParameter("Heatmap", "Heatmap", "Coloured mesh", GH_ParamAccess.item);
            p.AddCurveParameter("Isolines", "Isolines", "Contour lines (Contour / Classes modes)", GH_ParamAccess.list);
            p.AddNumberParameter("IsoValues", "IsoValues", "Value of each isoline", GH_ParamAccess.list);
            p.AddPlaneParameter("LabelPlanes", "LabelPlanes", "Isoline + peak label planes, aligned with the line and always readable (feed Text Tag 3D)", GH_ParamAccess.list);
            p.AddTextParameter("LabelText", "LabelText", "Label strings", GH_ParamAccess.list);
            p.AddNumberParameter("LabelSize", "LabelSize", "Text height used for the labels", GH_ParamAccess.item);
            p.AddTextParameter("BandStats", "BandStats", "% of the (unmasked) area in each band / class, for report tables", GH_ParamAccess.list);
            p.AddNumberParameter("BandAreas", "BandAreas", "Area of each band / class (model units²)", GH_ParamAccess.list);
            p.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON legend payload for Legend Geometry / Analysis Dashboard", GH_ParamAccess.item);
            p.AddTextParameter("Info", "Info", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void AutoWire(GH_Document doc)
        {
            AutoWireHelper.WireValueList(this, doc, 2, Modes, Modes.Select(m => "\"" + m + "\"").ToArray(), 200, 30);
            AutoWireHelper.WireLegendGeometry(this, doc, 8, 260, 0);
        }

        // ---- geometry cache: label-only changes (spacing, size, peaks, title) skip the heavy work
        private string _key;
        private Geo _geo;

        private sealed class Geo
        {
            public Mesh Mesh;
            public List<Isoline> Lines;
            public double[] Bounds;
            public Color[] BandColors;
            public string[] BandNames;
            public double[] BandArea;
            public ScalarMesh Sm;
            public double[] Vv;
            public bool[] FaceMask;
            public double Vmin, Vmax, DataMin, DataMax;
            public int Decimals;
            public BoundingBox Bb;
            public Vector3d Normal;
        }

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            Mesh mesh = null;
            var values = new List<double>();
            if (!DA.GetData(0, ref mesh) || mesh == null || !DA.GetDataList(1, values) || values.Count == 0)
            {
                Hud.Waiting(this, "mesh + values");
                return;
            }
            string modeIn = "Contour"; DA.GetData(2, ref modeIn);
            int levels = 10; DA.GetData(3, ref levels);
            int smooth = 0; DA.GetData(4, ref smooth);
            double? vminIn = null, vmaxIn = null;
            double tmp = 0;
            if (DA.GetData(5, ref tmp)) vminIn = tmp;
            if (DA.GetData(6, ref tmp)) vmaxIn = tmp;
            string palName = "Ladybug"; DA.GetData(7, ref palName);
            var custom = new List<Color>(); DA.GetDataList(8, custom);
            var maskIn = new List<bool>(); DA.GetDataList(9, maskIn);
            string schemeName = "UTCI"; DA.GetData(10, ref schemeName);
            string title = "Analysis"; DA.GetData(11, ref title);
            double spacingIn = double.NaN, sizeIn = double.NaN;
            if (DA.GetData(12, ref tmp)) spacingIn = tmp;
            if (DA.GetData(13, ref tmp)) sizeIn = tmp;
            int peaks = 0; DA.GetData(14, ref peaks);

            int mode = ParseMode(modeIn);
            levels = Math.Max(1, Math.Min(levels, 100));
            smooth = Math.Max(0, Math.Min(smooth, 50));
            Color[] stops = custom.Count >= 2 ? custom.ToArray() : null;
            if (stops == null && !Palettes.TryGet(palName, out stops))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Unknown palette '" + palName + "' - using Ladybug. See Color Palette > Names.");
                stops = Palettes.Get("Ladybug");
            }
            ClassScheme scheme = null;
            if (mode == 3 && !Palettes.TryGetScheme(schemeName, out scheme))
                throw new ArgumentException("Unknown Scheme '" + schemeName + "'. Use UTCI, Lawson or Noise.");

            string key = string.Join("|", mode, levels, smooth, vminIn, vmaxIn, string.Join(",", stops.Select(c => c.ToArgb())),
                scheme?.Name, mesh.Vertices.Count, mesh.Faces.Count, VertexHash(mesh), Hash(values), Hash(maskIn));
            bool cached = key == _key && _geo != null;
            if (!cached)
            {
                _geo = Build(mesh, values, maskIn, mode, levels, smooth, vminIn, vmaxIn, stops, scheme);
                _key = key;
            }
            var g = _geo;

            // ---- labels (cheap, never cached)
            double diag = g.Bb.Diagonal.Length;
            if (diag <= 0) diag = 1;
            double size = !double.IsNaN(sizeIn) && sizeIn > 0 ? sizeIn : 0.012 * diag;
            double spacing = !double.IsNaN(spacingIn) ? spacingIn : 0.15 * diag;
            string fmt = "F" + g.Decimals;
            var planes = new List<Plane>();
            var texts = new List<string>();
            var grid = new Dictionary<long, List<double[]>>();
            double gap = Math.Max(0.35 * spacing, 4.0 * size);

            Vector3d n = g.Normal, right, up;
            ReadingAxes(n, out right, out up);

            if (peaks != 0 && g.Sm != null)
            {
                var pk = FieldMath.Peaks(g.Sm, g.Vv, g.FaceMask, peaks > 0, gap, Math.Abs(peaks));
                foreach (int i in pk)
                {
                    string t = g.Vv[i].ToString(fmt, CultureInfo.InvariantCulture);
                    FieldMath.Free(grid, g.Sm.X[i], g.Sm.Y[i], g.Sm.Z[i], gap, gap * gap);
                    planes.Add(TextPlane(new Point3d(g.Sm.X[i], g.Sm.Y[i], g.Sm.Z[i]), right, up, n, t.Length, size));
                    texts.Add(t);
                }
            }
            if (spacing > 0 && g.Lines.Count > 0)
            {
                foreach (var s in FieldMath.PlaceLabels(g.Lines, spacing, 0, 0.3 * spacing))
                {
                    if (!FieldMath.Free(grid, s.X, s.Y, s.Z, gap, gap * gap)) continue;
                    var tan = new Vector3d(s.TX, s.TY, s.TZ);
                    tan -= n * (tan * n);
                    if (!tan.Unitize()) tan = right;
                    if (tan * right < -1e-9 || (Math.Abs(tan * right) < 1e-9 && tan * up < 0)) tan.Reverse();
                    var yv = Vector3d.CrossProduct(n, tan);
                    string t = s.Value.ToString(fmt, CultureInfo.InvariantCulture);
                    planes.Add(TextPlane(new Point3d(s.X, s.Y, s.Z), tan, yv, n, t.Length, size));
                    texts.Add(t);
                }
            }

            // ---- outputs
            var curves = new List<Curve>(g.Lines.Count);
            var isoVals = new List<double>(g.Lines.Count);
            foreach (var l in g.Lines)
            {
                var pts = new List<Point3d>(l.PointCount);
                for (int i = 0; i < l.PointCount; i++) pts.Add(new Point3d(l.Xyz[3 * i], l.Xyz[3 * i + 1], l.Xyz[3 * i + 2]));
                if (pts.Count < 2) continue;
                curves.Add(new PolylineCurve(pts));
                isoVals.Add(l.Value);
            }

            double totalArea = g.BandArea.Sum();
            var stats = new List<string>();
            for (int b = 0; b < g.BandArea.Length; b++)
            {
                double pct = totalArea > 0 ? 100.0 * g.BandArea[b] / totalArea : 0;
                stats.Add(g.BandNames[b] + ": " + pct.ToString("F1", CultureInfo.InvariantCulture) + "%");
            }

            DA.SetData(0, g.Mesh);
            DA.SetDataList(1, curves);
            DA.SetDataList(2, isoVals);
            DA.SetDataList(3, planes);
            DA.SetDataList(4, texts);
            DA.SetData(5, size);
            DA.SetDataList(6, stats);
            DA.SetDataList(7, g.BandArea);
            DA.SetData(8, LegendJson(g, mode, scheme, stops, title, totalArea));
            DA.SetData(9, InfoText);

            var lines = new List<string>
            {
                Hud.Metric("Mode", Modes[mode]),
                Hud.Metric("Range", g.Vmin.ToString(fmt, CultureInfo.InvariantCulture) + " - " + g.Vmax.ToString(fmt, CultureInfo.InvariantCulture)),
            };
            if (mode >= 2) lines.Add(Hud.Metric("Isolines", curves.Count));
            if (texts.Count > 0) lines.Add(Hud.Metric("Labels", texts.Count));
            if (cached) Hud.Cached(this, sw.Elapsed.TotalMilliseconds, lines.ToArray());
            else Hud.Done(this, sw, lines.ToArray());
        }

        // ================= heavy part =================

        private static Geo Build(Mesh mesh, List<double> values, List<bool> maskIn, int mode, int levels, int smooth,
                                 double? vminIn, double? vmaxIn, Color[] stops, ClassScheme scheme)
        {
            int nvIn = mesh.Vertices.Count, nfIn = mesh.Faces.Count;
            bool perVertex = values.Count == nvIn;
            bool perFace = !perVertex && values.Count == nfIn;
            if (!perVertex && !perFace)
                throw new ArgumentException(values.Count + " values for " + nvIn + " vertices / " + nfIn + " faces. Connect one value per vertex or per face.");

            // weld a per-face mesh so neighbouring cells share vertices (needed for gradients / isolines)
            Mesh work = mesh.DuplicateMesh();
            if (perFace) work.Vertices.CombineIdentical(true, true);
            work.VertexColors.Clear();

            int nv = work.Vertices.Count, nf = work.Faces.Count;
            var X = new double[nv]; var Y = new double[nv]; var Z = new double[nv];
            for (int i = 0; i < nv; i++) { var v = work.Vertices[i]; X[i] = v.X; Y[i] = v.Y; Z[i] = v.Z; }
            var F = new int[4 * nf];
            for (int f = 0; f < nf; f++)
            {
                var mf = work.Faces[f];
                F[4 * f] = mf.A; F[4 * f + 1] = mf.B; F[4 * f + 2] = mf.C; F[4 * f + 3] = mf.D;
            }
            var sm = new ScalarMesh(X, Y, Z, F);

            bool[] faceMask = null;
            if (maskIn.Count == nf) faceMask = maskIn.ToArray();
            else if (maskIn.Count > 0)
                throw new ArgumentException("Mask has " + maskIn.Count + " items for " + nf + " faces.");

            double[] vv;
            if (perVertex)
            {
                vv = values.ToArray();
                if (faceMask != null)
                {
                    // vertices only touched by masked faces carry no data
                    var touched = new bool[nv];
                    for (int f = 0; f < nf; f++)
                    {
                        if (faceMask[f]) continue;
                        for (int k = 0; k < 4; k++) touched[F[4 * f + k]] = true;
                    }
                    for (int i = 0; i < nv; i++) if (!touched[i]) vv[i] = double.NaN;
                }
            }
            else vv = FieldMath.FaceToVertex(F, nv, values, faceMask);
            if (smooth > 0 && mode != 0) vv = FieldMath.Smooth(F, nv, vv, smooth, faceMask);

            // face values for Cells mode and the data range
            double[] fv = perFace && (smooth == 0 || mode == 0) ? values.ToArray() : FieldMath.VertexToFace(F, vv);
            double dmin = double.MaxValue, dmax = double.MinValue;
            for (int f = 0; f < nf; f++)
            {
                if (faceMask != null && faceMask[f]) continue;
                double v = fv[f];
                if (double.IsNaN(v)) continue;
                if (v < dmin) dmin = v;
                if (v > dmax) dmax = v;
            }
            foreach (double v in vv) { if (double.IsNaN(v)) continue; if (v < dmin) dmin = v; if (v > dmax) dmax = v; }
            if (dmin > dmax) throw new ArgumentException("No valid values (all masked or NaN).");

            double vmin = vminIn ?? dmin, vmax = vmaxIn ?? dmax;
            if (vmin > vmax) { double sw = vmin; vmin = vmax; vmax = sw; }   // Min/Max swapped: use them the right way round
            if (vmax - vmin < 1e-12) vmax = vmin + 1e-6;

            // band edges
            double[] bounds;
            Color[] bandCol;
            string[] names;
            int decimals;
            if (mode == 3)
            {
                double lo = Math.Min(dmin, scheme.Bounds[0] - 1), hi = Math.Max(dmax, scheme.Bounds[scheme.Bounds.Length - 1] + 1);
                bounds = new[] { lo }.Concat(scheme.Bounds).Concat(new[] { hi }).ToArray();
                bandCol = scheme.Colors;
                names = scheme.Names;
                decimals = 1;
                vmin = lo; vmax = hi;
            }
            else
            {
                if (vminIn == null && vmaxIn == null)
                {
                    // round to nice steps (e.g. 0.5 m/s) so legend values read cleanly
                    double step = FieldMath.NiceStep(vmax - vmin, levels);
                    double lo = Math.Floor(vmin / step + 1e-9) * step, hi = Math.Ceiling(vmax / step - 1e-9) * step;
                    if (hi - lo < step) hi = lo + step;
                    int nb = (int)Math.Round((hi - lo) / step);
                    bounds = Enumerable.Range(0, nb + 1).Select(i => lo + i * step).ToArray();
                    vmin = lo; vmax = hi;
                    decimals = Decimals(step);
                }
                else
                {
                    bounds = Enumerable.Range(0, levels + 1).Select(i => vmin + (vmax - vmin) * i / levels).ToArray();
                    decimals = Decimals((vmax - vmin) / levels);
                }
                int n = bounds.Length - 1;
                bandCol = Palettes.Resample(stops, n);
                string f = "F" + decimals;
                names = new string[n];
                for (int b = 0; b < n; b++)
                    names[b] = bounds[b].ToString(f, CultureInfo.InvariantCulture) + " - " + bounds[b + 1].ToString(f, CultureInfo.InvariantCulture);
            }

            // contour geometry (also used for band areas in every mode)
            var bands = FieldMath.Bands(sm, vv, bounds, faceMask);
            var area = new double[bounds.Length - 1];
            if (mode == 0)
            {
                // stats must match what is drawn: each cell counts whole in its own band
                for (int f = 0; f < nf; f++)
                {
                    if ((faceMask != null && faceMask[f]) || double.IsNaN(fv[f])) continue;
                    area[FieldMath.BandOf(Math.Max(bounds[0], Math.Min(bounds[bounds.Length - 1], fv[f])), bounds)] += FaceArea(sm, f);
                }
            }
            else foreach (var bp in bands) area[bp.Band] += PolyArea(bp.Xyz);

            Color maskCol = Color.FromArgb(255, 200, 200, 200);
            Mesh outMesh = new Mesh();
            var lut = Palettes.Lut(stops);
            Func<double, Color> grad = v =>
            {
                if (double.IsNaN(v)) return maskCol;
                int i = (int)Math.Round((v - vmin) / (vmax - vmin) * 255);
                return lut[i < 0 ? 0 : (i > 255 ? 255 : i)];
            };

            if (mode == 0)
            {
                // flat colour per cell: unwelded so colours don't blend
                for (int f = 0; f < nf; f++)
                {
                    bool masked = faceMask != null && faceMask[f];
                    Color c = masked ? maskCol : grad(fv[f]);
                    AddFace(outMesh, sm, f, c);
                }
            }
            else if (mode == 1)
            {
                for (int i = 0; i < nv; i++) { outMesh.Vertices.Add(X[i], Y[i], Z[i]); outMesh.VertexColors.Add(grad(vv[i])); }
                for (int f = 0; f < nf; f++)
                {
                    bool masked = faceMask != null && faceMask[f];
                    bool bad = false;
                    for (int k = 0; k < 4; k++) if (double.IsNaN(vv[F[4 * f + k]])) bad = true;
                    if (masked || bad) { AddFace(outMesh, sm, f, maskCol); continue; }
                    outMesh.Faces.AddFace(F[4 * f], F[4 * f + 1], F[4 * f + 2], F[4 * f + 3]);
                }
                outMesh.Compact();
            }
            else
            {
                foreach (var bp in bands)
                {
                    int k = bp.Xyz.Length / 3;
                    int a = outMesh.Vertices.Count;
                    for (int i = 0; i < k; i++)
                    {
                        outMesh.Vertices.Add(bp.Xyz[3 * i], bp.Xyz[3 * i + 1], bp.Xyz[3 * i + 2]);
                        outMesh.VertexColors.Add(bandCol[bp.Band]);
                    }
                    if (k == 4) outMesh.Faces.AddFace(a, a + 1, a + 2, a + 3);
                    else for (int i = 1; i < k - 1; i++) outMesh.Faces.AddFace(a, a + i, a + i + 1);
                }
                for (int f = 0; f < nf; f++)
                {
                    bool bad = faceMask != null && faceMask[f];
                    for (int k = 0; k < 4 && !bad; k++) if (double.IsNaN(vv[F[4 * f + k]])) bad = true;
                    if (bad) AddFace(outMesh, sm, f, maskCol);
                }
            }
            outMesh.Normals.ComputeNormals();

            var levelsList = bounds.Skip(1).Take(bounds.Length - 2).ToArray();
            var lines = mode >= 2 ? FieldMath.Isolines(sm, vv, levelsList, faceMask) : new List<Isoline>();

            work.FaceNormals.ComputeFaceNormals();
            var nrm = Vector3d.Zero;
            for (int f = 0; f < nf; f++) nrm += (Vector3d)work.FaceNormals[f];
            if (!nrm.Unitize()) nrm = Vector3d.ZAxis;
            if (nrm.Z < -0.7) nrm.Reverse();

            return new Geo
            {
                Mesh = outMesh, Lines = lines, Bounds = bounds, BandColors = bandCol, BandNames = names, BandArea = area,
                Sm = sm, Vv = vv, FaceMask = faceMask, Vmin = vmin, Vmax = vmax, DataMin = dmin, DataMax = dmax,
                Decimals = decimals, Bb = work.GetBoundingBox(true), Normal = nrm
            };
        }

        private static void AddFace(Mesh m, ScalarMesh sm, int f, Color c)
        {
            int b = 4 * f;
            int k = sm.F[b + 3] == sm.F[b + 2] ? 3 : 4;
            int a = m.Vertices.Count;
            for (int i = 0; i < k; i++) { int q = sm.F[b + i]; m.Vertices.Add(sm.X[q], sm.Y[q], sm.Z[q]); m.VertexColors.Add(c); }
            if (k == 4) m.Faces.AddFace(a, a + 1, a + 2, a + 3); else m.Faces.AddFace(a, a + 1, a + 2);
        }

        // ================= helpers =================

        private static int ParseMode(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 2;
            s = s.Trim().Trim('"');
            int i;
            if (int.TryParse(s, out i)) return Math.Max(0, Math.Min(3, i));
            for (int k = 0; k < Modes.Length; k++) if (string.Equals(Modes[k], s, StringComparison.OrdinalIgnoreCase)) return k;
            if (s.StartsWith("cell", StringComparison.OrdinalIgnoreCase)) return 0;
            if (s.StartsWith("class", StringComparison.OrdinalIgnoreCase) || s.Equals("utci", StringComparison.OrdinalIgnoreCase)) return 3;
            throw new ArgumentException("Unknown Mode '" + s + "'. Use Cells, Smooth, Contour or Classes.");
        }

        private static int Decimals(double step)
        {
            if (step <= 0 || double.IsNaN(step)) return 1;
            int d = (int)Math.Max(0, Math.Ceiling(-Math.Log10(step) - 1e-9));
            // 2.5 / 0.25 style steps need one more digit
            double scaled = step * Math.Pow(10, d);
            if (Math.Abs(scaled - Math.Round(scaled)) > 1e-6) d++;
            return Math.Min(d, 4);
        }

        private static double PolyArea(double[] xyz)
        {
            // relative to the first vertex: keeps precision at georeferenced (UTM) coordinates
            int n = xyz.Length / 3;
            var s = Vector3d.Zero;
            for (int i = 1; i < n - 1; i++)
            {
                var a = new Vector3d(xyz[3 * i] - xyz[0], xyz[3 * i + 1] - xyz[1], xyz[3 * i + 2] - xyz[2]);
                var b = new Vector3d(xyz[3 * i + 3] - xyz[0], xyz[3 * i + 4] - xyz[1], xyz[3 * i + 5] - xyz[2]);
                s += Vector3d.CrossProduct(a, b);
            }
            return 0.5 * s.Length;
        }

        private static double FaceArea(ScalarMesh sm, int f)
        {
            int b = 4 * f;
            int k = sm.F[b + 3] == sm.F[b + 2] ? 3 : 4;
            var xyz = new double[3 * k];
            for (int i = 0; i < k; i++) { int q = sm.F[b + i]; xyz[3 * i] = sm.X[q]; xyz[3 * i + 1] = sm.Y[q]; xyz[3 * i + 2] = sm.Z[q]; }
            return PolyArea(xyz);
        }

        private static void ReadingAxes(Vector3d n, out Vector3d right, out Vector3d up)
        {
            if (Math.Abs(n.Z) > 0.7) { right = Vector3d.XAxis; up = Vector3d.YAxis; return; }
            up = Vector3d.ZAxis;
            right = Vector3d.CrossProduct(up, n);   // section seen from the side its normal faces
            right.Unitize();
        }

        // plane whose origin is the bottom-left of the text, so the text is centred on p (Text Tag 3D default)
        private static Plane TextPlane(Point3d p, Vector3d x, Vector3d y, Vector3d n, int chars, double size)
        {
            double halfW = 0.3 * size * chars;
            var o = p - x * halfW - y * (0.5 * size) + n * (0.01 * size);
            return new Plane(o, x, y);
        }

        private static string LegendJson(Geo g, int mode, ClassScheme scheme, Color[] stops, string title, double totalArea)
        {
            string f = "F" + g.Decimals;
            var j = new JObject { ["Title"] = title ?? "Analysis" };
            if (mode >= 2)
            {
                j["Type"] = "Blocks";
                j["Colors"] = new JArray(g.BandColors.Select(c => new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B }));
                if (mode == 3) j["Labels"] = new JArray(g.BandNames);
                else j["Labels"] = new JArray(g.Bounds.Select(b => b.ToString(f, CultureInfo.InvariantCulture)));
            }
            else
            {
                j["Type"] = "Gradient";
                j["Colors"] = new JArray(stops.Select(c => new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B }));
                j["Labels"] = new JArray(g.Vmin.ToString(f, CultureInfo.InvariantCulture), g.Vmax.ToString(f, CultureInfo.InvariantCulture));
            }
            var metrics = new JArray
            {
                new JObject { ["Name"] = "Min", ["Value"] = g.DataMin.ToString(f, CultureInfo.InvariantCulture) },
                new JObject { ["Name"] = "Max", ["Value"] = g.DataMax.ToString(f, CultureInfo.InvariantCulture) }
            };
            for (int b = 0; b < g.BandArea.Length && b < 12; b++)
            {
                double pct = totalArea > 0 ? 100.0 * g.BandArea[b] / totalArea : 0;
                if (pct < 0.05) continue;
                metrics.Add(new JObject { ["Name"] = g.BandNames[b], ["Value"] = pct.ToString("F1", CultureInfo.InvariantCulture) + "%" });
            }
            j["Metrics"] = metrics;
            return j.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static int VertexHash(Mesh m)
        {
            unchecked
            {
                int h = m.Vertices.Count;
                for (int i = 0; i < m.Vertices.Count; i++) h = h * 31 + m.Vertices[i].GetHashCode();
                for (int f = 0; f < m.Faces.Count; f++) h = h * 31 + m.Faces[f].GetHashCode();
                return h;
            }
        }

        private static int Hash<T>(List<T> l)
        {
            unchecked
            {
                int h = l.Count;
                foreach (var x in l) h = h * 31 + x.GetHashCode();
                return h;
            }
        }

        private const string InfoText =
            "ANALYSIS HEATMAP\n================\n\n" +
            "HOW IT WORKS:\nTakes any analysis mesh and its values (one per vertex or per face) and colours it in one of four modes:\n" +
            "- Cells: one flat colour per cell (exact values, no interpolation).\n" +
            "- Smooth: colours blended between cells.\n" +
            "- Contour: filled bands between round-number levels, plus isolines. Bands are clipped exactly, so they line up with the isolines.\n" +
            "- Classes: named categories with standard colours: UTCI thermal stress, Lawson (LDDC) wind comfort, or noise bands.\n\n" +
            "LABELS:\nValues are written along the isolines at LabelSpacing, turned to read left-to-right, and never overlap each other. Peaks > 0 labels the highest points (negative: the lowest).\n\n" +
            "MASK:\nBuildings or no-data cells (Mask = True) stay grey and are left out of smoothing, contours and the area statistics.\n\n" +
            "REPORTING:\nBandStats gives the % of the site area in each band/class, ready for a report table. To compare design options fairly, set the same Min/Max (or use Classes) on every option.\n\n" +
            "Method after Meerkat (T.-T. Tran, MIT).";
    }
}
