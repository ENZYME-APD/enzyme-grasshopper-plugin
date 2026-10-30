using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Enzyme.Utils;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Geometry;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Enzyme.Components
{
    /// <summary>
    /// Builds a terrain mesh from survey points and 3D breaklines (roads, platforms, decks, plateaus),
    /// trims it to an optional site boundary, extracts the part inside an optional plot boundary,
    /// drapes the plot boundary and tree locations onto it, and optionally colours it by elevation.
    /// </summary>
    public class TerrainMesher : EnzymeComponent
    {
        public TerrainMesher()
          : base("Terrain Mesher", "TerrainMesh",
              "Builds a terrain mesh from survey points and 3D breaklines (roads, platforms, decks, plateaus). Optional site boundary trims the mesh; optional plot boundary (flat 2D curve) is draped onto the terrain and its part of the mesh extracted. Tree locations are dropped onto the ground. Optional colours shade the mesh by elevation.",
              "Enzyme", "Site Analysis")
        { }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "Pts", "Survey points (clean them first with Survey Outlier Cleaner if needed).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Breaklines", "Brk", "3D curves the terrain must follow: road edges, platform and deck outlines, plateau edges. Closed curves replace the survey points inside them (flat platforms); open curves clear survey points within Clear Radius.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Spacing", "Spacing", "Distance between points sampled along breaklines and boundaries (model units). Smaller = tighter fit, heavier mesh.", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Clear Radius", "Clear", "Survey points closer than this (in plan) to an open breakline are ignored, so the road/deck edge is not pulled out of shape.", GH_ParamAccess.item, 0.5);
            pManager.AddCurveParameter("Boundary", "Bnd", "Optional closed curve: the mesh is trimmed to it (tested in plan).", GH_ParamAccess.item);
            pManager.AddCurveParameter("Plot Boundary", "Plot", "Optional closed curve inside the site, normally flat 2D. It is draped onto the terrain and the mesh inside it is output separately.", GH_ParamAccess.item);
            pManager.AddPointParameter("Trees", "Trees", "Optional tree locations (any Z); they are dropped onto the terrain.", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Colors", "Optional elevation gradient (low to high). Leave empty for an uncoloured mesh.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Max Edge", "MaxEdge", "Remove triangles with a plan edge longer than this (bridges across gaps or the convex hull). 0 = off.", GH_ParamAccess.item, 0.0);
            for (int i = 0; i < 9; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "Terrain mesh (trimmed to Boundary if given).", GH_ParamAccess.item);
            pManager.AddCurveParameter("Plot Boundary", "Plot", "Plot boundary draped onto the terrain.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Plot Mesh", "PlotMesh", "Part of the terrain mesh inside the plot boundary.", GH_ParamAccess.item);
            pManager.AddPointParameter("Trees", "Trees", "Tree locations on the terrain (same order as input; trees outside the mesh are snapped to its nearest point).", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "How the component works and how to tune it.", GH_ParamAccess.item);
        }

        protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
        {
            DA.SetData(4, InfoText);

            var survey = new List<Point3d>(); DA.GetDataList(0, survey);
            var breaks = new List<Curve>(); DA.GetDataList(1, breaks);
            double spacing = 1.0; DA.GetData(2, ref spacing);
            double clear = 0.5; DA.GetData(3, ref clear);
            Curve boundary = null; DA.GetData(4, ref boundary);
            Curve plot = null; DA.GetData(5, ref plot);
            var trees = new List<Point3d>(); DA.GetDataList(6, trees);
            var colors = new List<Color>(); DA.GetDataList(7, colors);
            double maxEdge = 0; DA.GetData(8, ref maxEdge);

            survey = survey.Where(p => p.IsValid).ToList();
            breaks = breaks.Where(c => c != null && c.IsValid).ToList();
            if (survey.Count + breaks.Count == 0) { Hud.Waiting(this, "points or breaklines"); return; }

            double tol = Rhino.RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 0.001;
            if (spacing <= tol) { spacing = 1.0; AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Spacing reset to 1.0"); }
            clear = Math.Max(0, clear);

            Polygon2 bndPoly = ClosedPlan(boundary, spacing, "Boundary");
            Polygon2 plotPoly = ClosedPlan(plot, spacing, "Plot Boundary");

            // ---- 1. Breakline samples; survey points inside platforms / near open breaklines are dropped ----
            var breakPts = new List<Point3d>();
            var platforms = new List<Polygon2>();
            var openLines = new List<Polyline>();
            foreach (var c in breaks)
            {
                var pts = Densify(c, spacing);
                if (pts.Count < 2) continue;
                breakPts.AddRange(pts);
                if (c.IsClosed && pts.Count >= 3) platforms.Add(new Polygon2(pts));
                else openLines.Add(new Polyline(pts));
            }

            var keep = new bool[survey.Count];
            Parallel.For(0, survey.Count, i =>
            {
                var p = survey[i];
                foreach (var poly in platforms) if (poly.Contains(p.X, p.Y)) return;
                if (clear > 0)
                    foreach (var pl in openLines)
                        if (PlanDistance(pl, p) < clear) return;
                keep[i] = true;
            });
            int dropped = keep.Count(k => !k);

            var basePts = new List<Point3d>(breakPts);                    // breaklines first: they win ties
            for (int i = 0; i < survey.Count; i++) if (keep[i]) basePts.Add(survey[i]);
            basePts = DedupePlan(basePts, Math.Max(tol, spacing * 0.02));
            if (basePts.Count < 3) { Hud.Waiting(this, "at least 3 points"); return; }

            // ---- 2. Preliminary mesh, used only to give boundary / plot outline points their height ----
            var final = new List<Point3d>(basePts);
            List<Point3d> bndPts = null, plotPts = null;
            if (bndPoly != null || plotPoly != null)
            {
                Mesh prelim = Triangulate(basePts, 0);
                if (prelim == null) { Hud.Error(this, "Triangulation failed (points may be collinear)"); return; }
                MeshPrep.WarmUp(prelim);
                if (bndPoly != null)
                {
                    bndPts = Drape(prelim, bndPoly.Points);
                    final = final.Where(p => bndPoly.Contains(p.X, p.Y)).ToList();
                    final.AddRange(bndPts);
                }
                if (plotPoly != null)
                {
                    plotPts = Drape(prelim, plotPoly.Points);
                    // plot outline points become mesh vertices so the plot mesh has a clean edge
                    final = final.Where(p => plotPoly.DistanceToEdge(p.X, p.Y) > spacing * 0.25).ToList();
                    final.AddRange(plotPts);
                }
                final = DedupePlan(final, Math.Max(tol, spacing * 0.02));
            }

            // ---- 3. Final mesh, trimmed to boundary and Max Edge ----
            Mesh mesh = Triangulate(final, maxEdge, bndPoly);
            if (mesh == null || mesh.Faces.Count == 0) { Hud.Error(this, "No triangles left (check Boundary / Max Edge)"); return; }

            BoundingBox bb = mesh.GetBoundingBox(true);
            if (colors.Count > 0) ColourByHeight(mesh, colors, bb.Min.Z, bb.Max.Z);

            // ---- 4. Plot mesh + draped plot outline ----
            Mesh plotMesh = null; Curve plotOut = null; double plotArea = 0;
            if (plotPoly != null)
            {
                plotMesh = SubMesh(mesh, plotPoly);
                plotArea = PlanArea(plotMesh);
                MeshPrep.WarmUp(mesh);
                var draped = Drape(mesh, plotPoly.Points);
                draped.Add(draped[0]);
                plotOut = new PolylineCurve(draped);
            }

            // ---- 5. Trees onto terrain ----
            var treesOut = new List<Point3d>();
            if (trees.Count > 0)
            {
                MeshPrep.WarmUp(mesh);
                treesOut = Drape(mesh, trees.Where(t => t.IsValid).ToList());
            }

            DA.SetData(0, mesh);
            if (plotOut != null) DA.SetData(1, plotOut);
            if (plotMesh != null) DA.SetData(2, plotMesh);
            DA.SetDataList(3, treesOut);

            Hud.Done(this, sw,
                Hud.Metric("Triangles", mesh.Faces.Count),
                Hud.Metric("Points used", final.Count),
                dropped > 0 ? Hud.Metric("Replaced by breaklines", dropped) : null,
                $"Z: {bb.Min.Z:N1} to {bb.Max.Z:N1} m",
                plotPoly != null ? Hud.Metric("Plot area", plotArea, "m²", 0) : null,
                treesOut.Count > 0 ? Hud.Metric("Trees", treesOut.Count) : null);
        }

        // ---------------- Geometry helpers ----------------

        /// <summary>Closed curve flattened to a plan polygon sampled at spacing, or null.</summary>
        Polygon2 ClosedPlan(Curve c, double spacing, string label)
        {
            if (c == null) return null;
            if (!c.IsClosed) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, label + " must be a closed curve; ignored."); return null; }
            var pts = Densify(c, spacing);
            if (pts.Count < 3) return null;
            return new Polygon2(pts);
        }

        /// <summary>Points along a curve at roughly 'spacing', always keeping corners. Closed curves don't repeat the start.</summary>
        static List<Point3d> Densify(Curve c, double spacing)
        {
            var result = new List<Point3d>();
            Curve[] segs = c.DuplicateSegments();
            if (segs == null || segs.Length == 0) segs = new[] { c };
            foreach (var s in segs)
            {
                double len = s.GetLength();
                int n = Math.Max(1, (int)Math.Ceiling(len / spacing));
                double[] ts = s.DivideByCount(n, true);
                if (ts == null) { result.Add(s.PointAtStart); continue; }
                for (int i = 0; i < ts.Length - 1; i++) result.Add(s.PointAt(ts[i]));   // segment end = next start
            }
            if (!c.IsClosed) result.Add(c.PointAtEnd);
            return result;
        }

        static double PlanDistance(Polyline pl, Point3d p)
        {
            double best = double.MaxValue;
            for (int i = 0; i < pl.Count - 1; i++)
            {
                double ax = pl[i].X, ay = pl[i].Y, bx = pl[i + 1].X, by = pl[i + 1].Y;
                double vx = bx - ax, vy = by - ay, L = vx * vx + vy * vy;
                double t = L > 0 ? Math.Max(0, Math.Min(1, ((p.X - ax) * vx + (p.Y - ay) * vy) / L)) : 0;
                double dx = p.X - (ax + t * vx), dy = p.Y - (ay + t * vy);
                best = Math.Min(best, dx * dx + dy * dy);
            }
            return Math.Sqrt(best);
        }

        /// <summary>Removes points closer than tol in plan (first occurrence wins).</summary>
        static List<Point3d> DedupePlan(List<Point3d> pts, double tol)
        {
            var seen = new HashSet<(long, long)>();
            var outp = new List<Point3d>(pts.Count);
            foreach (var p in pts)
            {
                var key = ((long)Math.Round(p.X / tol), (long)Math.Round(p.Y / tol));
                if (seen.Add(key)) outp.Add(p);
            }
            return outp;
        }

        /// <summary>2D Delaunay in plan (shifted to a local origin so survey coordinates keep precision),
        /// lifted to 3D with double-precision vertices. Optional Max Edge and boundary trims.</summary>
        static Mesh Triangulate(List<Point3d> pts, double maxEdge, Polygon2 clip = null)
        {
            double ox = pts.Min(p => p.X), oy = pts.Min(p => p.Y);
            var nodes = new Node2List();
            foreach (var p in pts) nodes.Append(new Node2(p.X - ox, p.Y - oy));
            var faces = new List<Grasshopper.Kernel.Geometry.Delaunay.Face>();
            Mesh flat = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Mesh(nodes, 1e-6, ref faces);
            if (flat == null || flat.Faces.Count == 0) return null;

            var mesh = new Mesh();
            mesh.Vertices.UseDoublePrecisionVertices = true;
            foreach (var p in pts) mesh.Vertices.Add(p);
            double maxSq = maxEdge > 0 ? maxEdge * maxEdge : double.MaxValue;
            for (int i = 0; i < flat.Faces.Count; i++)
            {
                var f = flat.Faces[i];
                Point3d a = pts[f.A], b = pts[f.B], c = pts[f.C];
                if (PlanSq(a, b) > maxSq || PlanSq(b, c) > maxSq || PlanSq(c, a) > maxSq) continue;
                if (clip != null && !clip.Contains((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3)) continue;
                mesh.Faces.AddFace(f.A, f.B, f.C);
            }
            mesh.Compact();
            mesh.Normals.ComputeNormals();
            return mesh;
        }

        static double PlanSq(Point3d a, Point3d b) { double dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

        /// <summary>Drops points vertically onto the mesh; points outside it snap to the nearest mesh point.</summary>
        static List<Point3d> Drape(Mesh mesh, List<Point3d> pts)
        {
            BoundingBox bb = mesh.GetBoundingBox(true);
            double top = bb.Max.Z + 10.0;
            var outp = new Point3d[pts.Count];
            Parallel.For(0, pts.Count, i =>
            {
                var p = pts[i];
                double t = Intersection.MeshRay(mesh, new Ray3d(new Point3d(p.X, p.Y, top), -Vector3d.ZAxis));
                outp[i] = t >= 0 ? new Point3d(p.X, p.Y, top - t) : mesh.ClosestPoint(p);
            });
            return outp.ToList();
        }

        static Mesh SubMesh(Mesh mesh, Polygon2 poly)
        {
            var sub = mesh.DuplicateMesh();
            var remove = new List<int>();
            for (int i = 0; i < sub.Faces.Count; i++)
            {
                var f = sub.Faces[i];
                Point3d a = sub.Vertices.Point3dAt(f.A), b = sub.Vertices.Point3dAt(f.B), c = sub.Vertices.Point3dAt(f.C);
                if (!poly.Contains((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3)) remove.Add(i);
            }
            sub.Faces.DeleteFaces(remove);
            sub.Compact();
            return sub;
        }

        static double PlanArea(Mesh m)
        {
            if (m == null) return 0;
            double area = 0;
            foreach (var f in m.Faces)
            {
                Point3d a = m.Vertices.Point3dAt(f.A), b = m.Vertices.Point3dAt(f.B), c = m.Vertices.Point3dAt(f.C);
                area += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) * 0.5;
            }
            return area;
        }

        static void ColourByHeight(Mesh mesh, List<Color> gradient, double zMin, double zMax)
        {
            double range = Math.Max(zMax - zMin, 1e-9);
            mesh.VertexColors.Clear();
            for (int i = 0; i < mesh.Vertices.Count; i++)
                mesh.VertexColors.Add(Lerp(gradient, (mesh.Vertices.Point3dAt(i).Z - zMin) / range));
        }

        static Color Lerp(List<Color> g, double t)
        {
            if (g.Count == 1) return g[0];
            t = Math.Max(0, Math.Min(1, t)) * (g.Count - 1);
            int i = Math.Min((int)Math.Floor(t), g.Count - 2);
            double u = t - i;
            Color a = g[i], b = g[i + 1];
            return Color.FromArgb((int)(a.R + (b.R - a.R) * u), (int)(a.G + (b.G - a.G) * u), (int)(a.B + (b.B - a.B) * u));
        }

        /// <summary>Plan polygon with fast point-in-polygon and edge-distance tests.</summary>
        sealed class Polygon2
        {
            public readonly List<Point3d> Points;
            readonly double[] xs, ys;
            readonly double minX, minY, maxX, maxY;

            public Polygon2(List<Point3d> pts)
            {
                Points = pts;
                xs = pts.Select(p => p.X).ToArray(); ys = pts.Select(p => p.Y).ToArray();
                minX = xs.Min(); maxX = xs.Max(); minY = ys.Min(); maxY = ys.Max();
            }

            public bool Contains(double x, double y)
            {
                if (x < minX || x > maxX || y < minY || y > maxY) return false;
                bool inside = false;
                for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
                    if ((ys[i] > y) != (ys[j] > y) && x < (xs[j] - xs[i]) * (y - ys[i]) / (ys[j] - ys[i]) + xs[i])
                        inside = !inside;
                return inside;
            }

            public double DistanceToEdge(double x, double y)
            {
                double best = double.MaxValue;
                for (int i = 0, j = xs.Length - 1; i < xs.Length; j = i++)
                {
                    double vx = xs[i] - xs[j], vy = ys[i] - ys[j], L = vx * vx + vy * vy;
                    double t = L > 0 ? Math.Max(0, Math.Min(1, ((x - xs[j]) * vx + (y - ys[j]) * vy) / L)) : 0;
                    double dx = x - (xs[j] + t * vx), dy = y - (ys[j] + t * vy);
                    best = Math.Min(best, dx * dx + dy * dy);
                }
                return Math.Sqrt(best);
            }
        }

        const string InfoText =
            "TERRAIN MESHER\n\n" +
            "HOW IT WORKS:\n" +
            "1. Breaklines are sampled every Spacing. Closed breaklines (platforms, decks, plateaus) replace the survey " +
            "points inside them, so a flat platform stays flat. Survey points within Clear Radius of an open breakline " +
            "(road edges) are ignored so the edge isn't pulled out of shape.\n" +
            "2. Everything is triangulated in plan (Delaunay) around a local origin, so large survey coordinates keep their precision.\n" +
            "3. Boundary and plot outlines get their heights from a first-pass mesh and are inserted as vertices, giving " +
            "clean edges. Triangles outside the Boundary (and any longer than Max Edge) are removed.\n" +
            "4. The plot outline is draped onto the final mesh and the triangles inside it form Plot Mesh.\n" +
            "5. Trees are dropped vertically onto the terrain; any outside it snap to the nearest point.\n\n" +
            "TUNING:\n" +
            "- Smaller Spacing follows curves more tightly but makes a heavier mesh.\n" +
            "- Long thin triangles at the edges with no Boundary: set Max Edge (e.g. 3-5x the point spacing).\n" +
            "- Spikes in the mesh usually come from bad survey points: run Survey Outlier Cleaner first.";

        // ---------------- Auto-wire ----------------

        protected override void AutoWire(GH_Document document)
        {
            AutoWireHelper.WireSlider(this, document, 2, 0.1, 10.0, 1.0, 260, 0);
            AutoWireHelper.WireSlider(this, document, 3, 0.0, 5.0, 0.5, 260, 25);
            AutoWireHelper.WireSlider(this, document, 8, 0.0, 100.0, 0.0, 260, 50);
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

        protected override Bitmap Icon => IconLoader.Load("TerrainMesher.png");
        public override GH_Exposure Exposure => GH_Exposure.secondary;
        public override Guid ComponentGuid => new Guid("DF827968-A1E6-4AC2-BD46-3B209671C86D");
    }
}
