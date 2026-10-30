using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    /// <summary>
    /// Reads analysis values at chosen points (entrances, seating, play areas, facades) for one or
    /// many result sets, with labels and a CSV table for reports.
    /// </summary>
    public class PointProbe : EnzymeComponent
    {
        public PointProbe()
          : base("Point Probe", "Probe",
              "Reads analysis values at any points (entrances, seating, play areas...). Interpolates within the mesh face, works for several result sets at once (options, hours) and writes a CSV table for reports.",
              "Enzyme", "Site Analysis")
        { }

        public override Guid ComponentGuid => new Guid("3E8D5A17-C64B-4F2A-B39E-7D10A6C52F88");
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        protected override Bitmap Icon => IconLoader.Load("point_probe.png");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "Analysis mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Values", "Values", "One value per vertex or per face. Several branches = several result sets (options, hours); each becomes a CSV column", GH_ParamAccess.tree);
            pManager.AddPointParameter("Points", "Points", "Points to probe", GH_ParamAccess.list);
            pManager.AddTextParameter("Names", "Names", "Optional name per point (default P1, P2...)", GH_ParamAccess.list);
            pManager.AddTextParameter("SetNames", "SetNames", "Optional name per result set / branch (default from branch path)", GH_ParamAccess.list);
            pManager.AddNumberParameter("MaxDistance", "MaxDistance", "Points further than this from the mesh get no value (0 = no limit)", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("Decimals", "Decimals", "Decimals in labels and CSV", GH_ParamAccess.item, 1);
            pManager.AddTextParameter("Unit", "Unit", "Unit shown in the labels, e.g. m/s, dB, h", GH_ParamAccess.item, "");
            for (int i = 3; i < 8; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Values", "Values", "Probed value per point (one branch per result set, same order as Points). Empty where the point is off the mesh", GH_ParamAccess.tree);
            pManager.AddPointParameter("MeshPoints", "MeshPoints", "Closest point on the mesh for each probe (label anchors)", GH_ParamAccess.list);
            pManager.AddTextParameter("Labels", "Labels", "Name: value text per point (first result set; all sets when there are several)", GH_ParamAccess.list);
            pManager.AddTextParameter("CSV", "CSV", "Table Name,X,Y,Z,<set 1>,<set 2>... (paste into Excel or write with a file writer)", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void AutoWire(GH_Document doc)
        {
            AutoWireHelper.WireMultilinePanel(this, doc, 7, "m/s", 200, 150, 60, 20);
        }

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            Mesh mesh = null;
            GH_Structure<GH_Number> tree;
            var pts = new List<Point3d>();
            if (!DA.GetData(0, ref mesh) || mesh == null || !DA.GetDataTree(1, out tree) || tree == null || tree.DataCount == 0 || !DA.GetDataList(2, pts))
            {
                Hud.Waiting(this, "mesh + values + points");
                return;
            }
            var names = new List<string>(); DA.GetDataList(3, names);
            var setNames = new List<string>(); DA.GetDataList(4, setNames);
            double maxD = 0; DA.GetData(5, ref maxD);
            int dec = 1; DA.GetData(6, ref dec);
            string unit = ""; DA.GetData(7, ref unit);
            dec = Math.Max(0, Math.Min(dec, 6));
            string f = "F" + dec;
            var inv = CultureInfo.InvariantCulture;
            unit = string.IsNullOrWhiteSpace(unit) ? "" : " " + unit.Trim();

            int nv = mesh.Vertices.Count, nf = mesh.Faces.Count;
            var sets = new List<double[]>();
            var setLabels = new List<string>();
            for (int bi = 0; bi < tree.PathCount; bi++)
            {
                var br = tree.Branches[bi];
                if (br.Count == 0) continue;
                if (br.Count != nv && br.Count != nf)
                    throw new ArgumentException("Branch " + tree.Paths[bi] + " has " + br.Count + " values for " + nv + " vertices / " + nf + " faces.");
                sets.Add(br.Select(x => x == null ? double.NaN : x.Value).ToArray());
                setLabels.Add(sets.Count <= setNames.Count ? setNames[sets.Count - 1] : (tree.PathCount == 1 ? "Value" : "Set " + tree.Paths[bi]));
            }

            // closest point once per probe, reused for every result set
            var mps = new MeshPoint[pts.Count];
            var anchors = new List<Point3d>(pts.Count);
            int onMesh = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var mp = mesh.ClosestMeshPoint(pts[i], maxD > 0 ? maxD : 0.0);
                mps[i] = mp;
                anchors.Add(mp != null ? mp.Point : pts[i]);
                if (mp != null) onMesh++;
            }

            var outTree = new GH_Structure<GH_Number>();
            var table = new double[pts.Count, sets.Count];
            for (int s = 0; s < sets.Count; s++)
            {
                var vals = sets[s];
                bool perVertex = vals.Length == nv;
                var path = new GH_Path(s);
                for (int i = 0; i < pts.Count; i++)
                {
                    double v = Sample(mesh, mps[i], vals, perVertex);
                    table[i, s] = v;
                    outTree.Append(double.IsNaN(v) ? null : new GH_Number(v), path);
                }
            }

            var labels = new List<string>(pts.Count);
            var csv = new StringBuilder();
            csv.Append("Name,X,Y,Z");
            foreach (var sl in setLabels) csv.Append(',').Append(Csv(sl));
            csv.AppendLine();
            double vmin = double.MaxValue, vmax = double.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                string nm = i < names.Count && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : "P" + (i + 1);
                var sb = new StringBuilder(nm + ":");
                csv.Append(Csv(nm)).Append(',').Append(anchors[i].X.ToString("F2", inv)).Append(',').Append(anchors[i].Y.ToString("F2", inv)).Append(',').Append(anchors[i].Z.ToString("F2", inv));
                for (int s = 0; s < sets.Count; s++)
                {
                    double v = table[i, s];
                    string t = double.IsNaN(v) ? "-" : v.ToString(f, inv);
                    if (!double.IsNaN(v)) { if (v < vmin) vmin = v; if (v > vmax) vmax = v; }
                    sb.Append(sets.Count > 1 ? (s == 0 ? " " : " / ") : " ").Append(t);
                    csv.Append(',').Append(double.IsNaN(v) ? "" : v.ToString(f, inv));
                }
                sb.Append(unit);
                labels.Add(sb.ToString());
                csv.AppendLine();
            }

            DA.SetDataTree(0, outTree);
            DA.SetDataList(1, anchors);
            DA.SetDataList(2, labels);
            DA.SetData(3, csv.ToString());
            DA.SetData(4, InfoText);

            var lines = new List<string> { Hud.Metric("Points", pts.Count), Hud.Metric("Sets", sets.Count) };
            if (onMesh < pts.Count) lines.Add(Hud.Metric("Off mesh", pts.Count - onMesh));
            if (vmin <= vmax) lines.Add(Hud.Metric("Range", vmin.ToString(f, inv) + " - " + vmax.ToString(f, inv) + unit));
            if (onMesh < pts.Count) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, (pts.Count - onMesh) + " point(s) are further than MaxDistance from the mesh.");
            Hud.Done(this, sw, lines.ToArray());
        }

        // barycentric interpolation inside the hit face (per vertex) or the face value (per face)
        private static double Sample(Mesh m, MeshPoint mp, double[] vals, bool perVertex)
        {
            if (mp == null || mp.FaceIndex < 0) return double.NaN;
            if (!perVertex) return vals[mp.FaceIndex];
            var f = m.Faces[mp.FaceIndex];
            double[] t = mp.T;
            int[] ids = { f.A, f.B, f.C, f.D };
            double s = 0, w = 0;
            for (int k = 0; k < 4; k++)
            {
                if (t[k] == 0) continue;   // triangles: ids[3] == ids[2], so any split of the weight is still right
                double v = vals[ids[k]];
                if (double.IsNaN(v)) continue;
                s += t[k] * v; w += t[k];
            }
            return w > 0 ? s / w : double.NaN;
        }

        private static string Csv(string s)
        {
            if (s == null) return "";
            return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        private const string InfoText =
            "POINT PROBE\n===========\n\n" +
            "HOW IT WORKS:\nFinds the closest point on the analysis mesh for each probe and reads the value there, interpolated between the face's vertices (or the face value for per-face results).\n\n" +
            "SEVERAL RESULT SETS:\nGraft or entwine the Values of several options or hours into one tree (one branch each). Every branch becomes a column in the CSV, so one table compares all options at the key points.\n\n" +
            "USE IN REPORTS:\nPlace points at entrances, seating, play areas or facade openings, name them, and paste the CSV into the report or Excel. Labels + MeshPoints go straight into Text Tag 3D for the drawings.";
    }
}
