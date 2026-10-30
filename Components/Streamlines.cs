using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class Streamlines : EnzymeComponent
    {
        public Streamlines()
          : base("Streamlines", "Streamlines",
              "Generates smooth streamlines (particle traces) through any vector field. Regular grids (e.g. Fast CFD) are read directly with bilinear interpolation; scattered points use Inverse Distance Weighting. 4th-order Runge-Kutta integration, optional speed-coloured tubes.",
              "Enzyme", "Site Analysis")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("FieldPoints", "FieldPoints", "The locations of the vector field data", GH_ParamAccess.list);
            pManager.AddVectorParameter("FieldVectors", "FieldVectors", "The vectors corresponding to each FieldPoint", GH_ParamAccess.list);
            pManager.AddPointParameter("SeedPoints", "SeedPoints", "The starting locations for the streamlines", GH_ParamAccess.list);
            pManager.AddNumberParameter("StepSize", "StepSize", "Distance to move per integration step", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("MaxSteps", "MaxSteps", "Maximum number of steps per streamline (per direction)", GH_ParamAccess.item, 500);
            pManager.AddNumberParameter("SearchRadius", "SearchRadius", "Scattered points only: radius to search for blending local vectors (ignored for regular grids)", GH_ParamAccess.item, 6.0);
            pManager.AddNumberParameter("MoveUp", "MoveUp", "Visual Z-offset to prevent clashing with the analysis mesh", GH_ParamAccess.item, 0.5);
            pManager.AddIntegerParameter("Direction", "Direction", "1 = both ways from each seed (default), 0 = downstream only, -1 = upstream only", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("MinSpeed", "MinSpeed", "Lines stop where the field is slower than this (m/s)", GH_ParamAccess.item, 0.05);
            pManager.AddNumberParameter("TubeRadius", "TubeRadius", "Radius of the speed-coloured tube mesh (0 = no tubes)", GH_ParamAccess.item, 0.0);
            pManager.AddTextParameter("Palette", "Palette", "Tube colours by local speed (palette name, e.g. Wind, Turbo, Viridis)", GH_ParamAccess.item, "Wind");

            for (int i = 3; i < 11; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Streamlines", "Streamlines", "The generated streamline curves", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Component documentation and stats", GH_ParamAccess.item);
            pManager.AddNumberParameter("Speeds", "Speeds", "Local field speed at every streamline vertex (one branch per streamline)", GH_ParamAccess.tree);
            pManager.AddMeshParameter("Tubes", "Tubes", "One mesh of tubes coloured by local speed (needs TubeRadius > 0)", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "JSON legend payload for the tube colours", GH_ParamAccess.item);
        }

        protected override void Solve(IGH_DataAccess DA, System.Diagnostics.Stopwatch sw)
        {
            List<Point3d> fPts = new List<Point3d>();
            List<Vector3d> fVecs = new List<Vector3d>();
            List<Point3d> seeds = new List<Point3d>();

            if (!DA.GetDataList(0, fPts) || !DA.GetDataList(1, fVecs) || !DA.GetDataList(2, seeds))
            {
                Hud.Waiting(this, "field + seeds");
                return;
            }

            double stepSize = 1.0; DA.GetData(3, ref stepSize);
            int maxSteps = 500; DA.GetData(4, ref maxSteps);
            double searchRadius = 6.0; DA.GetData(5, ref searchRadius);
            double moveUp = 0.5; DA.GetData(6, ref moveUp);
            // inputs added in v1.11: components placed with older versions don't have them
            int direction = 0; double minSpeed = 0.05, tubeR = 0; string palette = "Wind";
            if (Params.Input.Count > 7) { direction = 1; DA.GetData(7, ref direction); }
            if (Params.Input.Count > 8) DA.GetData(8, ref minSpeed);
            if (Params.Input.Count > 9) DA.GetData(9, ref tubeR);
            if (Params.Input.Count > 10) DA.GetData(10, ref palette);

            if (fPts.Count == 0 || fVecs.Count == 0 || fPts.Count != fVecs.Count)
                throw new ArgumentException("FieldPoints and FieldVectors must have the same number of items.");
            if (stepSize <= 0) throw new ArgumentException("StepSize must be > 0.");
            maxSteps = Math.Max(1, Math.Min(maxSteps, 100000));
            minSpeed = Math.Max(0, minSpeed);

            // ---- field: direct grid lookup when the points form a lattice, IDW otherwise
            var px = fPts.Select(p => p.X).ToList();
            var py = fPts.Select(p => p.Y).ToList();
            var pz = fPts.Select(p => p.Z).ToList();
            IVectorField2D field = GridField2D.TryBuild(px, py, pz, fVecs.Select(v => v.X).ToList(), fVecs.Select(v => v.Y).ToList());
            string fieldMode = field != null ? "Grid" : "IDW";
            if (field == null) field = new IdwField(fPts, fVecs, searchRadius);

            var outCurves = new List<Curve>();
            var speedTree = new Grasshopper.Kernel.Data.GH_Structure<Grasshopper.Kernel.Types.GH_Number>();
            var allPts = new List<List<Point3d>>();
            var allSpd = new List<List<double>>();
            double smin = double.MaxValue, smax = 0, slen = 0;

            foreach (var seed in seeds)
            {
                var fw = new List<double[]>(); var fs = new List<double>();
                var bw = new List<double[]>(); var bs = new List<double>();
                if (direction >= 0) StreamTracer.Trace(field, seed.X, seed.Y, stepSize, maxSteps, 1, minSpeed, fw, fs);
                if (direction != 0) StreamTracer.Trace(field, seed.X, seed.Y, stepSize, maxSteps, -1, minSpeed, bw, bs);

                // upstream part reversed, then downstream (seed point appears once)
                var pts = new List<Point3d>(fw.Count + bw.Count);
                var spd = new List<double>(fw.Count + bw.Count);
                for (int i = bw.Count - 1; i >= 0; i--) { pts.Add(new Point3d(bw[i][0], bw[i][1], bw[i][2] + moveUp)); spd.Add(bs[i]); }
                for (int i = (bw.Count > 0 ? 1 : 0); i < fw.Count; i++) { pts.Add(new Point3d(fw[i][0], fw[i][1], fw[i][2] + moveUp)); spd.Add(fs[i]); }
                if (pts.Count < 2) continue;

                var crv = new PolylineCurve(pts);
                if (!crv.IsValid) continue;
                var path = new Grasshopper.Kernel.Data.GH_Path(outCurves.Count);
                outCurves.Add(crv);
                foreach (double s in spd) { speedTree.Append(new Grasshopper.Kernel.Types.GH_Number(s), path); if (s < smin) smin = s; if (s > smax) smax = s; }
                allPts.Add(pts); allSpd.Add(spd);
                slen += crv.GetLength();
            }
            if (smin > smax) { smin = 0; smax = 1; }

            Mesh tubes = null;
            Color[] stops = Palettes.Get(palette, "Wind");
            if (tubeR > 0 && allPts.Count > 0)
            {
                tubes = new Mesh();
                for (int i = 0; i < allPts.Count; i++) AddTube(tubes, allPts[i], allSpd[i], tubeR, smin, smax, stops);
                tubes.Normals.ComputeNormals();
            }

            DA.SetDataList(0, outCurves);
            DA.SetData(1, InfoText);
            if (Params.Output.Count > 2) DA.SetDataTree(2, speedTree);
            if (Params.Output.Count > 3) DA.SetData(3, tubes);
            if (Params.Output.Count > 4)
            {
                var j = new Newtonsoft.Json.Linq.JObject
                {
                    ["Type"] = "Gradient",
                    ["Title"] = "STREAMLINE SPEED (m/s)",
                    ["Colors"] = new Newtonsoft.Json.Linq.JArray(stops.Select(c => new Newtonsoft.Json.Linq.JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B })),
                    ["Labels"] = new Newtonsoft.Json.Linq.JArray(smin.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), smax.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                };
                DA.SetData(4, j.ToString(Newtonsoft.Json.Formatting.None));
            }

            Hud.Done(this, sw, Hud.Metric("Lines", outCurves.Count), Hud.Metric("Field", fieldMode),
                     Hud.Metric("Length", slen, "m", 0), Hud.Metric("Speed", smin.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " - " + smax.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " m/s"));
        }

        private static void AddTube(Mesh m, List<Point3d> pts, List<double> spd, double r, double smin, double smax, Color[] stops)
        {
            const int sides = 6;
            int n = pts.Count;
            int start = m.Vertices.Count;
            Vector3d prevN = Vector3d.Unset;
            for (int i = 0; i < n; i++)
            {
                Vector3d t = (i < n - 1 ? pts[i + 1] : pts[i]) - (i > 0 ? pts[i - 1] : pts[i]);
                if (!t.Unitize()) t = Vector3d.XAxis;
                // parallel-transported frame: no twisting along the tube
                Vector3d nrm = prevN.IsValid ? prevN - t * (prevN * t) : Vector3d.CrossProduct(t, Math.Abs(t.Z) < 0.9 ? Vector3d.ZAxis : Vector3d.XAxis);
                if (!nrm.Unitize()) nrm = Vector3d.CrossProduct(t, Vector3d.XAxis);
                nrm.Unitize();
                prevN = nrm;
                Vector3d b = Vector3d.CrossProduct(t, nrm);
                Color c = Palettes.Sample(stops, smax > smin ? (spd[i] - smin) / (smax - smin) : 0.5);
                for (int s = 0; s < sides; s++)
                {
                    double a = 2 * Math.PI * s / sides;
                    m.Vertices.Add(pts[i] + (nrm * Math.Cos(a) + b * Math.Sin(a)) * r);
                    m.VertexColors.Add(c);
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = start + i * sides + s, a1 = start + i * sides + (s + 1) % sides;
                    m.Faces.AddFace(a0, a1, a1 + sides, a0 + sides);
                }
        }

        /// <summary>Scattered-point field: 2D inverse-distance weighting over an RTree radius search.</summary>
        private sealed class IdwField : IVectorField2D
        {
            private readonly RTree _tree = new RTree();
            private readonly List<Point3d> _pts;
            private readonly List<Vector3d> _vecs;
            private readonly double _r;

            public IdwField(List<Point3d> pts, List<Vector3d> vecs, double radius)
            {
                _pts = pts; _vecs = vecs; _r = Math.Max(radius, 1e-6);
                // insert as 2D points so the vertical spread of draped grids doesn't matter
                for (int i = 0; i < pts.Count; i++) _tree.Insert(new Point3d(pts[i].X, pts[i].Y, 0), i);
            }

            public bool Sample(double x, double y, out double u, out double v, out double z)
            {
                double su = 0, sv = 0, sz = 0, sw = 0;
                bool exact = false;
                double eu = 0, ev = 0, ez = 0;
                var box = new BoundingBox(x - _r, y - _r, -1, x + _r, y + _r, 1);
                _tree.Search(box, (sender, args) =>
                {
                    if (exact) return;
                    int i = args.Id;
                    double dx = x - _pts[i].X, dy = y - _pts[i].Y;
                    double d2 = dx * dx + dy * dy;
                    if (d2 < 1e-6) { exact = true; eu = _vecs[i].X; ev = _vecs[i].Y; ez = _pts[i].Z; args.Cancel = true; return; }
                    if (d2 > _r * _r) return;
                    double w = 1.0 / d2;
                    su += _vecs[i].X * w; sv += _vecs[i].Y * w; sz += _pts[i].Z * w; sw += w;
                });
                if (exact) { u = eu; v = ev; z = ez; return true; }
                if (sw <= 0) { u = v = z = 0; return false; }
                u = su / sw; v = sv / sw; z = sz / sw;
                return true;
            }
        }

        private const string InfoText =
            "UNIVERSAL STREAMLINES\n" +
            "=====================\n\n" +
            "HOW IT WORKS:\n" +
            "Drops a particle at each SeedPoint and follows the vector field with 4th-order Runge-Kutta steps of StepSize (much more accurate in curved flow and vortices than straight steps). " +
            "If the field points sit on a regular grid (Fast CFD output), values are read directly from the grid with bilinear interpolation - no search per step, so it is much faster. Scattered points fall back to Inverse Distance Weighting within SearchRadius.\n\n" +
            "2.5D SMART DRAPING:\n" +
            "Lines follow the ground height of the field points, so they drape over the terrain like the Fast CFD vectors, without needing the terrain mesh.\n\n" +
            "INPUTS:\n" +
            "- Direction: 1 traces both upstream and downstream from each seed, so seeds can sit anywhere in the site.\n" +
            "- MinSpeed: lines stop in calm zones (wakes, courtyards).\n" +
            "- TubeRadius: > 0 builds one mesh of tubes coloured by local speed (fast to display, renders well).\n\n" +
            "Lines also stop at buildings, at the edge of the field, and when they close a loop.";

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("streamlines.png");

        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid => new Guid("C4E1D2A9-1234-4ABC-8D9E-F98765432101");
    }
}
