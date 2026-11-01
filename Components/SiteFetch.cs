using Enzyme.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;

namespace Enzyme.Components
{
    /// <summary>
    /// Downloads open site context for a lat/lon and builds it as Rhino geometry in local metres
    /// (UTM of the site centre, centre at 0,0): terrain (Copernicus GLO-30), land cover (ESA WorldCover),
    /// OSM buildings with heights and OSM trees as closed crowns ready for Sun Hours' Trees input.
    /// Inspired by VoxCity's generator (Fujiwara et al., 2025) but kept as exact geometry, not voxels.
    /// </summary>
    public class SiteFetch : EnzymeComponent
    {
        protected override bool UsesRunGate => true;
        private const double MaxSize = 5000.0;

        public SiteFetch()
          : base("Site Fetch", "SiteFetch",
              "Downloads open data for a site (terrain, land cover, OSM buildings and trees) and builds it as geometry in local metres. Site centre = 0,0 in UTM; see Georeference.",
              "Enzyme", "Site Analysis")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Latitude", "Lat", "Site centre latitude (WGS84 degrees).", GH_ParamAccess.item, 40.4168);
            pManager.AddNumberParameter("Longitude", "Lon", "Site centre longitude (WGS84 degrees).", GH_ParamAccess.item, -3.7038);
            pManager.AddNumberParameter("Size", "Size", "Side of the square site in metres (max 5000).", GH_ParamAccess.item, 500.0);
            pManager.AddNumberParameter("Cell Size", "Cell", "Terrain and land cover mesh cell size in metres. Terrain source is 30 m, land cover 10 m.", GH_ParamAccess.item, 10.0);
            pManager.AddBooleanParameter("Buildings", "Bldg", "Fetch OSM buildings.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Trees", "Trees", "Fetch OSM individual trees (natural=tree).", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Land Cover", "LC", "Fetch ESA WorldCover land cover.", GH_ParamAccess.item, true);
            pManager.AddNumberParameter("Default Height", "DefH", "Height for buildings with no height or levels tag (m).", GH_ParamAccess.item, 9.0);
            pManager.AddNumberParameter("Level Height", "LvlH", "Storey height used to convert building:levels to metres.", GH_ParamAccess.item, 3.0);
            pManager.AddNumberParameter("Tree Height", "TreeH", "Height for trees with no height tag (m).", GH_ParamAccess.item, 8.0);
            pManager.AddNumberParameter("Crown Diameter", "CrownD", "Crown diameter for trees with no diameter_crown tag (m).", GH_ParamAccess.item, 6.0);
            pManager.AddTextParameter("Cache Folder", "Cache", "Optional folder for downloaded data. Default: %LOCALAPPDATA%/Enzyme/SiteFetch. Re-runs read from here.", GH_ParamAccess.item);
            pManager.AddTextParameter("Overpass URL", "Overpass", "Optional Overpass API endpoint for OSM data.", GH_ParamAccess.item, SiteSources.DefaultOverpass);
            pManager.AddBooleanParameter("Run", "Run", "Download and build. Use a button or toggle: nothing is downloaded until Run is true.", GH_ParamAccess.item, false);
            for (int i = 11; i <= 12; i++) pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "Terrain", "Terrain mesh (Copernicus GLO-30, EGM2008 heights in m).", GH_ParamAccess.item);
            pManager.AddMeshParameter("Land Cover", "LC", "Terrain mesh coloured by ESA WorldCover class (one colour per cell).", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Land Cover Class", "LCClass", "WorldCover class code per land cover cell (10 tree cover, 50 built-up, 80 water, ...).", GH_ParamAccess.list);
            pManager.AddBrepParameter("Buildings", "Bldg", "OSM buildings as closed extrusions standing on the terrain.", GH_ParamAccess.list);
            pManager.AddMeshParameter("Building Mesh", "BldgMesh", "All buildings joined in one mesh: plug into Context of Sun Hours, Fast CFD, etc.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Building Heights", "Heights", "Height of each building (m, eaves to ground, from OSM height / levels or the default).", GH_ParamAccess.list);
            pManager.AddMeshParameter("Tree Crowns", "Crowns", "Closed crown meshes: plug into Trees of Sun Hours.", GH_ParamAccess.list);
            pManager.AddPointParameter("Tree Points", "TreePts", "Trunk base points on the terrain.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Boundary", "Boundary", "Site boundary square.", GH_ParamAccess.item);
            pManager.AddTextParameter("Georeference", "Geo", "JSON: UTM zone, EPSG, easting/northing of the local origin, lat/lon.", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON legend, site metrics and land cover areas (for Report Metrics).", GH_ParamAccess.item);
            pManager.AddTextParameter("Sources", "Sources", "Data sources and required attribution.", GH_ParamAccess.item);
        }

        protected override void Solve(IGH_DataAccess DA, System.Diagnostics.Stopwatch sw)
        {
            double lat = 0, lon = 0, size = 500, cell = 10, defH = 9, lvlH = 3, treeH = 8, crownD = 6;
            bool getB = true, getT = true, getLC = true;
            string cacheDir = null, overpass = SiteSources.DefaultOverpass;
            DA.GetData(0, ref lat); DA.GetData(1, ref lon); DA.GetData(2, ref size); DA.GetData(3, ref cell);
            DA.GetData(4, ref getB); DA.GetData(5, ref getT); DA.GetData(6, ref getLC);
            DA.GetData(7, ref defH); DA.GetData(8, ref lvlH); DA.GetData(9, ref treeH); DA.GetData(10, ref crownD);
            DA.GetData(11, ref cacheDir); DA.GetData(12, ref overpass);

            if (lat < -80 || lat > 84) throw new ArgumentException("Latitude must be between -80 and 84 (UTM range).");
            if (size <= 0) throw new ArgumentException("Size must be positive.");
            if (size > MaxSize) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Size capped at {MaxSize:0} m."); size = MaxSize; }
            cell = Math.Max(1.0, Math.Min(cell, size / 2));
            if (string.IsNullOrWhiteSpace(cacheDir)) cacheDir = SiteHttp.DefaultCacheDir();
            if (string.IsNullOrWhiteSpace(overpass)) overpass = SiteSources.DefaultOverpass;

            var frame = new LocalFrame(lat, lon);
            double half = size / 2.0;
            // Fetch a margin around the site so edge buildings / trees and bilinear sampling are complete.
            frame.Bounds(half + 2 * cell, half + 2 * cell, out double s, out double w, out double n, out double e);

            // ---------------- terrain
            var dem = new RasterMosaic();
            int missingDem = 0;
            foreach (var t in SiteSources.Tiles(s, w, n, e, 1))
            {
                string url = SiteSources.CopernicusDemUrl(t.lat, t.lon);
                if (!SiteHttp.Exists(url, cacheDir)) { missingDem++; continue; }
                dem.Add(new GeoTiff(new HttpByteSource(url, cacheDir)), s, w, n, e);
            }
            if (missingDem > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"{missingDem} terrain tile(s) do not exist (open sea or not released); elevation 0 used there.");

            Func<double, double, double> ground = (x, y) =>
            {
                frame.ToLatLon(x, y, out double la, out double lo);
                double z = dem.Bilinear(lo, la);
                return double.IsNaN(z) ? 0.0 : z;
            };

            int nx = Math.Max(1, (int)Math.Round(size / cell)), ny = nx;
            double step = size / nx;
            var zs = new double[(nx + 1) * (ny + 1)];
            System.Threading.Tasks.Parallel.For(0, ny + 1, j =>
            {
                for (int i = 0; i <= nx; i++) zs[j * (nx + 1) + i] = ground(-half + i * step, -half + j * step);
            });
            var terrain = new Mesh();
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++) terrain.Vertices.Add(-half + i * step, -half + j * step, zs[j * (nx + 1) + i]);
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i;
                    terrain.Faces.AddFace(a, a + 1, a + nx + 2, a + nx + 1);
                }
            terrain.Normals.ComputeNormals();
            terrain.Compact();
            double zMin = zs.Min(), zMax = zs.Max();

            // ---------------- land cover
            Mesh lcMesh = null;
            var lcClass = new List<int>();
            var classArea = new SortedDictionary<int, double>();
            if (getLC)
            {
                try
                {
                    var lc = new RasterMosaic();
                    foreach (var t in SiteSources.Tiles(s, w, n, e, 3))
                    {
                        string url = SiteSources.WorldCoverUrl(t.lat, t.lon);
                        if (SiteHttp.Exists(url, cacheDir)) lc.Add(new GeoTiff(new HttpByteSource(url, cacheDir)), s, w, n, e);
                    }
                    lcMesh = new Mesh();
                    var colours = SiteSources.WorldCoverClasses.ToDictionary(c => c.Code, c => Color.FromArgb(c.R, c.G, c.B));
                    for (int j = 0; j < ny; j++)
                        for (int i = 0; i < nx; i++)
                        {
                            double cx = -half + (i + 0.5) * step, cy = -half + (j + 0.5) * step;
                            frame.ToLatLon(cx, cy, out double la, out double lo);
                            double v = lc.Nearest(lo, la);
                            int code = double.IsNaN(v) ? 0 : (int)v;
                            lcClass.Add(code);
                            classArea.TryGetValue(code, out double ar);
                            classArea[code] = ar + step * step;
                            Color col = colours.TryGetValue(code, out Color cc) ? cc : Color.Black;
                            int a = j * (nx + 1) + i;
                            int v0 = lcMesh.Vertices.Count;
                            foreach (int idx in new[] { a, a + 1, a + nx + 2, a + nx + 1 })
                            {
                                lcMesh.Vertices.Add(terrain.Vertices[idx]);
                                lcMesh.VertexColors.Add(col);
                            }
                            lcMesh.Faces.AddFace(v0, v0 + 1, v0 + 2, v0 + 3);
                        }
                    lcMesh.Normals.ComputeNormals();
                    if (lc.TileCount == 0) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No land cover tile found for this site.");
                }
                catch (Exception ex)
                {
                    lcMesh = null; lcClass.Clear(); classArea.Clear();
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Land cover download failed: " + ex.Message);
                }
            }

            // ---------------- OSM buildings and trees
            var osmB = new List<OsmBuilding>();
            var osmT = new List<OsmTree>();
            if (getB || getT)
            {
                try
                {
                    string json = SiteHttp.Overpass(overpass, OsmParse.Query(s, w, n, e, getB, getT), cacheDir);
                    OsmParse.Parse(json, defH, lvlH, osmB, osmT);
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "OpenStreetMap download failed (Overpass may be busy, try again or set another Overpass URL): " + ex.Message);
                }
            }

            var breps = new List<Brep>();
            var heights = new List<double>();
            var bMesh = new Mesh();
            double footprint = 0, heightSum = 0;
            int tagged = 0;
            var mp = MeshingParameters.FastRenderMesh;
            foreach (var b in osmB)
            {
                var outers = b.Outers.Select(r => ToLocal(frame, r)).Where(r => r != null).ToList();
                if (outers.Count == 0) continue;
                // Keep buildings whose largest outer ring is centred inside the site.
                var main = outers.OrderByDescending(r => Math.Abs(SignedArea(r))).First();
                var c = Centroid(main);
                if (Math.Abs(c.X) > half || Math.Abs(c.Y) > half) continue;
                var inners = b.Inners.Select(r => ToLocal(frame, r)).Where(r => r != null).ToList();

                double zb = double.MaxValue;
                foreach (var r in outers) foreach (var p in r) zb = Math.Min(zb, ground(p.X, p.Y));

                foreach (var outer in outers)
                {
                    var holes = inners.Where(h => PointInPolygon(h[0], outer)).ToList();
                    Brep brep = Extrude(outer, holes, zb + b.MinHeight, zb + b.Height);
                    if (brep == null) continue;
                    breps.Add(brep);
                    heights.Add(b.Height);
                    var ms = Mesh.CreateFromBrep(brep, mp);
                    if (ms != null) foreach (var m in ms) bMesh.Append(m);
                    footprint += Math.Abs(SignedArea(outer)) - holes.Sum(h => Math.Abs(SignedArea(h)));
                    heightSum += b.Height;
                    if (b.HeightFromTags) tagged++;
                }
            }
            bMesh.Weld(Math.PI);
            bMesh.Compact();

            var crowns = new List<Mesh>();
            var trunks = new List<Point3d>();
            foreach (var t in osmT)
            {
                frame.ToLocal(t.Lat, t.Lon, out double x, out double y);
                if (Math.Abs(x) > half || Math.Abs(y) > half) continue;
                double h = double.IsNaN(t.Height) || t.Height <= 0 ? treeH : t.Height;
                double d = double.IsNaN(t.CrownDiameter) || t.CrownDiameter <= 0 ? crownD : t.CrownDiameter;
                double r = Math.Min(d / 2.0, h * 0.45);
                double z = ground(x, y);
                trunks.Add(new Point3d(x, y, z));
                var crown = Mesh.CreateIcoSphere(new Sphere(new Point3d(x, y, z + h - r), r), 1);
                if (crown != null && crown.IsValid) crowns.Add(crown);
            }

            // ---------------- outputs
            var boundary = new Polyline(new[]
            {
                new Point3d(-half, -half, 0), new Point3d(half, -half, 0), new Point3d(half, half, 0),
                new Point3d(-half, half, 0), new Point3d(-half, -half, 0)
            }).ToNurbsCurve();

            var geo = new JObject
            {
                ["crs"] = "WGS 84 / UTM zone " + frame.Zone + (frame.North ? "N" : "S"),
                ["epsg"] = frame.Epsg,
                ["utm_zone"] = frame.Zone,
                ["hemisphere"] = frame.North ? "N" : "S",
                ["origin_easting"] = Math.Round(frame.Easting, 3),
                ["origin_northing"] = Math.Round(frame.Northing, 3),
                ["origin_lat"] = lat,
                ["origin_lon"] = lon,
                ["size_m"] = size,
                ["cell_m"] = step,
                ["height_datum"] = "EGM2008 (Copernicus GLO-30)",
                ["note"] = "Rhino X/Y = UTM easting/northing minus origin; Y is grid north, not true north."
            };

            double siteArea = size * size;
            var metrics = new JArray
            {
                M("Buildings", breps.Count.ToString(CultureInfo.InvariantCulture)),
                M("Building coverage", (footprint / siteArea * 100).ToString("F1", CultureInfo.InvariantCulture) + "%"),
                M("Mean building height", (breps.Count > 0 ? heightSum / breps.Count : 0).ToString("F1", CultureInfo.InvariantCulture) + " m"),
                M("Buildings with height data", (breps.Count > 0 ? tagged * 100.0 / breps.Count : 0).ToString("F0", CultureInfo.InvariantCulture) + "%"),
                M("Mapped trees", crowns.Count.ToString(CultureInfo.InvariantCulture)),
                M("Elevation min", zMin.ToString("F1", CultureInfo.InvariantCulture) + " m"),
                M("Elevation max", zMax.ToString("F1", CultureInfo.InvariantCulture) + " m"),
                M("Relief", (zMax - zMin).ToString("F1", CultureInfo.InvariantCulture) + " m"),
            };
            var colorsJ = new JArray(); var labelsJ = new JArray(); var bandsJ = new JArray();
            foreach (var c in SiteSources.WorldCoverClasses)
            {
                if (!classArea.TryGetValue(c.Code, out double ar)) continue;
                colorsJ.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                labelsJ.Add(c.Name);
                bandsJ.Add(new JObject { ["Name"] = c.Name, ["Area"] = Math.Round(ar, 1), ["Pct"] = Math.Round(ar / siteArea * 100, 2) });
            }
            var dash = new JObject
            {
                ["Type"] = "Blocks",
                ["Title"] = "Land Cover (ESA WorldCover 2021)",
                ["Colors"] = colorsJ,
                ["Labels"] = labelsJ,
                ["Metrics"] = metrics,
                ["BandAreas"] = bandsJ,
            };

            Hud.Done(this, sw,
                Hud.Metric("Grid", nx + " x " + ny),
                Hud.Metric("Relief", zMax - zMin, "m", 1),
                Hud.Metric("Buildings", breps.Count),
                Hud.Metric("Trees", crowns.Count),
                Hud.Metric("UTM", frame.Zone + (frame.North ? "N" : "S")));

            DA.SetData(0, terrain);
            if (lcMesh != null) DA.SetData(1, lcMesh);
            DA.SetDataList(2, lcClass);
            DA.SetDataList(3, breps);
            if (bMesh.Faces.Count > 0) DA.SetData(4, bMesh);
            DA.SetDataList(5, heights);
            DA.SetDataList(6, crowns);
            DA.SetDataList(7, trunks);
            DA.SetData(8, boundary);
            DA.SetData(9, geo.ToString());
            DA.SetData(10, dash.ToString());
            DA.SetData(11, SiteSources.Attribution);
        }

        // ---------------- helpers

        private static JObject M(string name, string value) => new JObject { ["Name"] = name, ["Value"] = value };

        private static List<Point3d> ToLocal(LocalFrame f, List<(double lat, double lon)> ring)
        {
            if (ring == null || ring.Count < 4) return null;
            var pts = new List<Point3d>(ring.Count);
            foreach (var p in ring)
            {
                f.ToLocal(p.lat, p.lon, out double x, out double y);
                if (pts.Count == 0 || pts[pts.Count - 1].DistanceTo(new Point3d(x, y, 0)) > 1e-3) pts.Add(new Point3d(x, y, 0));
            }
            if (pts.Count < 4 || pts[0].DistanceTo(pts[pts.Count - 1]) > 1e-3) return null;
            pts[pts.Count - 1] = pts[0];
            return Math.Abs(SignedArea(pts)) < 0.5 ? null : pts;
        }

        private static Brep Extrude(List<Point3d> outer, List<List<Point3d>> holes, double z0, double z1)
        {
            if (z1 - z0 < 0.1) return null;
            var ext = new Extrusion();
            if (!ext.SetPathAndUp(new Point3d(0, 0, z0), new Point3d(0, 0, z1), Vector3d.YAxis)) return null;
            var oc = new PolylineCurve(SignedArea(outer) > 0 ? outer : Reversed(outer));
            if (!ext.SetOuterProfile(oc, true)) return null;
            foreach (var h in holes)
            {
                var hc = new PolylineCurve(SignedArea(h) < 0 ? h : Reversed(h));
                ext.AddInnerProfile(hc);
            }
            var brep = ext.ToBrep(true);
            return brep != null && brep.IsValid ? brep : null;
        }

        private static List<Point3d> Reversed(List<Point3d> p) { var r = new List<Point3d>(p); r.Reverse(); return r; }

        private static double SignedArea(List<Point3d> p)
        {
            double s = 0;
            for (int i = 0; i + 1 < p.Count; i++) s += p[i].X * p[i + 1].Y - p[i + 1].X * p[i].Y;
            return s / 2.0;
        }

        private static Point3d Centroid(List<Point3d> p)
        {
            double x = 0, y = 0; int n = p.Count - 1;
            for (int i = 0; i < n; i++) { x += p[i].X; y += p[i].Y; }
            return new Point3d(x / n, y / n, 0);
        }

        private static bool PointInPolygon(Point3d q, List<Point3d> poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 2; i < poly.Count - 1; j = i++)
            {
                if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y) &&
                    q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                    inside = !inside;
            }
            return inside;
        }

        protected override Bitmap Icon => Enzyme.IconLoader.Load("SiteFetch.png");
        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid => new Guid("7B3E2F41-9C5D-4A8E-B1F6-2D4C8E9A0F35");
    }
}
