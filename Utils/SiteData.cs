using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Enzyme.Utils
{
    // =====================================================================================
    // Open data sources for Site Fetch. Rhino-free (geometry is built in the component).
    // =====================================================================================

    public static class SiteSources
    {
        public const string DefaultOverpass = "https://overpass-api.de/api/interpreter";
        public const string UserAgent = "EnzymeGrasshopperPlugin/SiteFetch (+https://github.com/ENZYME-APD/enzyme-grasshopper-plugin)";

        /// <summary>Copernicus GLO-30 DEM tile (1°×1°, named by its south-west corner) on AWS Open Data.</summary>
        public static string CopernicusDemUrl(int latFloor, int lonFloor)
        {
            string ns = latFloor >= 0 ? "N" : "S", ew = lonFloor >= 0 ? "E" : "W";
            string name = string.Format(CultureInfo.InvariantCulture, "Copernicus_DSM_COG_10_{0}{1:00}_00_{2}{3:000}_00_DEM",
                ns, Math.Abs(latFloor), ew, Math.Abs(lonFloor));
            return "https://copernicus-dem-30m.s3.amazonaws.com/" + name + "/" + name + ".tif";
        }

        /// <summary>ESA WorldCover 2021 v200 tile (3°×3°, named by its south-west corner) on AWS Open Data.</summary>
        public static string WorldCoverUrl(int lat3, int lon3)
        {
            string ns = lat3 >= 0 ? "N" : "S", ew = lon3 >= 0 ? "E" : "W";
            return string.Format(CultureInfo.InvariantCulture,
                "https://esa-worldcover.s3.eu-central-1.amazonaws.com/v200/2021/map/ESA_WorldCover_10m_2021_v200_{0}{1:00}{2}{3:000}_Map.tif",
                ns, Math.Abs(lat3), ew, Math.Abs(lon3));
        }

        /// <summary>South-west corners of all tiles of the given size (degrees) touching a lat/lon box.</summary>
        public static List<(int lat, int lon)> Tiles(double s, double w, double n, double e, int size)
        {
            var r = new List<(int, int)>();
            int la0 = FloorTo(s, size), la1 = FloorTo(n, size), lo0 = FloorTo(w, size), lo1 = FloorTo(e, size);
            for (int la = la0; la <= la1; la += size)
                for (int lo = lo0; lo <= lo1; lo += size) r.Add((la, lo));
            return r;
        }

        private static int FloorTo(double v, int size) => (int)Math.Floor(v / size) * size;

        /// <summary>ESA WorldCover classes: code, name, official colour.</summary>
        public static readonly (int Code, string Name, int R, int G, int B)[] WorldCoverClasses =
        {
            (10, "Tree cover", 0, 100, 0),
            (20, "Shrubland", 255, 187, 34),
            (30, "Grassland", 255, 255, 76),
            (40, "Cropland", 240, 150, 255),
            (50, "Built-up", 250, 0, 0),
            (60, "Bare / sparse vegetation", 180, 180, 180),
            (70, "Snow and ice", 240, 240, 240),
            (80, "Permanent water", 0, 100, 200),
            (90, "Herbaceous wetland", 0, 150, 160),
            (95, "Mangroves", 0, 207, 117),
            (100, "Moss and lichen", 250, 230, 160),
        };

        public static string Attribution =>
            "Buildings and trees © OpenStreetMap contributors (ODbL). " +
            "Terrain: Copernicus DEM GLO-30 © DLR e.V. 2010-2014 and © Airbus 2014-2018, provided under COPERNICUS by the EU and ESA. " +
            "Land cover: ESA WorldCover 2021 v200 © ESA (CC BY 4.0).";
    }

    // ------------------------------------------------------------------ raster mosaic

    /// <summary>A georeferenced window read from one or more raster tiles, sampled by lon/lat.</summary>
    public sealed class RasterMosaic
    {
        private sealed class Win
        {
            public GeoTiff Tif; public double[] V; public int X0, Y0, W, H;
        }
        private readonly List<Win> _wins = new List<Win>();
        public int TileCount => _wins.Count;

        /// <summary>Reads the part of <paramref name="tif"/> covering the lon/lat box (with a 2-pixel margin).</summary>
        public void Add(GeoTiff tif, double s, double w, double n, double e)
        {
            tif.ToPixel(w, n, out double c0, out double r0);
            tif.ToPixel(e, s, out double c1, out double r1);
            int x0 = Math.Max(0, (int)Math.Floor(c0) - 2), y0 = Math.Max(0, (int)Math.Floor(r0) - 2);
            int x1 = Math.Min(tif.Width - 1, (int)Math.Ceiling(c1) + 2), y1 = Math.Min(tif.Height - 1, (int)Math.Ceiling(r1) + 2);
            if (x1 < x0 || y1 < y0) return;
            int ww = x1 - x0 + 1, hh = y1 - y0 + 1;
            _wins.Add(new Win { Tif = tif, V = tif.ReadWindow(x0, y0, ww, hh), X0 = x0, Y0 = y0, W = ww, H = hh });
        }

        /// <summary>Bilinear sample (continuous data such as elevation). NaN if outside all tiles.</summary>
        public double Bilinear(double lon, double lat)
        {
            foreach (var win in _wins)
            {
                win.Tif.ToPixel(lon, lat, out double c, out double r);
                c -= win.X0; r -= win.Y0;
                if (c < -0.5 || r < -0.5 || c > win.W - 0.5 || r > win.H - 0.5) continue;
                int i0 = Clamp((int)Math.Floor(c), win.W - 1), j0 = Clamp((int)Math.Floor(r), win.H - 1);
                int i1 = Clamp(i0 + 1, win.W - 1), j1 = Clamp(j0 + 1, win.H - 1);
                double fx = Math.Max(0, Math.Min(1, c - i0)), fy = Math.Max(0, Math.Min(1, r - j0));
                double v00 = win.V[j0 * win.W + i0], v10 = win.V[j0 * win.W + i1];
                double v01 = win.V[j1 * win.W + i0], v11 = win.V[j1 * win.W + i1];
                double v = (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy;
                if (double.IsNaN(v)) v = Nearest(win, c, r);
                return v;
            }
            return double.NaN;
        }

        /// <summary>Nearest-pixel sample (categorical data such as land cover).</summary>
        public double Nearest(double lon, double lat)
        {
            foreach (var win in _wins)
            {
                win.Tif.ToPixel(lon, lat, out double c, out double r);
                c -= win.X0; r -= win.Y0;
                if (c < -0.5 || r < -0.5 || c > win.W - 0.5 || r > win.H - 0.5) continue;
                return Nearest(win, c, r);
            }
            return double.NaN;
        }

        private static double Nearest(Win win, double c, double r)
            => win.V[Clamp((int)Math.Round(r), win.H - 1) * win.W + Clamp((int)Math.Round(c), win.W - 1)];

        private static int Clamp(int v, int max) => v < 0 ? 0 : (v > max ? max : v);
    }

    // ------------------------------------------------------------------ HTTP with disk cache

    /// <summary>HTTP range reads, cached on disk so re-running a site does not download again.</summary>
    public sealed class HttpByteSource : IByteSource
    {
        private readonly string _url, _cacheDir;
        public HttpByteSource(string url, string cacheDir) { _url = url; _cacheDir = cacheDir; }

        public byte[] Read(long offset, int length)
        {
            string key = SiteHttp.Hash(_url + "|" + offset + "|" + length);
            string path = _cacheDir == null ? null : Path.Combine(_cacheDir, key + ".bin");
            if (path != null && File.Exists(path)) return File.ReadAllBytes(path);

            var req = (HttpWebRequest)WebRequest.Create(_url);
            req.UserAgent = SiteSources.UserAgent;
            req.Timeout = 60000;
            req.AddRange(offset, offset + length - 1);
            byte[] data;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var s = resp.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                data = ms.ToArray();
                if (resp.StatusCode == HttpStatusCode.OK && data.Length > length)
                {
                    // Server ignored the Range header and sent the whole file.
                    var part = new byte[Math.Max(0, Math.Min(length, data.Length - (int)offset))];
                    if (part.Length > 0) Array.Copy(data, offset, part, 0, part.Length);
                    data = part;
                }
            }
            if (path != null) SiteHttp.SafeWrite(path, data);
            return data;
        }
    }

    public static class SiteHttp
    {
        static SiteHttp()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        public static string DefaultCacheDir()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
            return Path.Combine(root, "Enzyme", "SiteFetch");
        }

        /// <summary>True if the URL answers (HEAD 200); false on 404 (e.g. an ocean DEM tile).</summary>
        public static bool Exists(string url, string cacheDir)
        {
            string flag = cacheDir == null ? null : Path.Combine(cacheDir, Hash("exists|" + url) + ".txt");
            if (flag != null && File.Exists(flag)) return File.ReadAllText(flag) == "1";
            bool ok;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "HEAD";
                req.UserAgent = SiteSources.UserAgent;
                req.Timeout = 30000;
                using (var resp = (HttpWebResponse)req.GetResponse()) ok = resp.StatusCode == HttpStatusCode.OK;
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse r && ((int)r.StatusCode == 404 || (int)r.StatusCode == 403))
            {
                ok = false;
            }
            if (flag != null) SafeWrite(flag, Encoding.ASCII.GetBytes(ok ? "1" : "0"));
            return ok;
        }

        /// <summary>POSTs an Overpass QL query and returns the JSON text (cached by query).</summary>
        public static string Overpass(string endpoint, string query, string cacheDir)
        {
            string path = cacheDir == null ? null : Path.Combine(cacheDir, Hash(endpoint + "|" + query) + ".json");
            if (path != null && File.Exists(path)) return File.ReadAllText(path);

            var req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.Method = "POST";
            req.UserAgent = SiteSources.UserAgent;
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 180000;
            byte[] body = Encoding.UTF8.GetBytes("data=" + Uri.EscapeDataString(query));
            using (var rs = req.GetRequestStream()) rs.Write(body, 0, body.Length);
            string json;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                json = sr.ReadToEnd();
            if (path != null) SafeWrite(path, Encoding.UTF8.GetBytes(json));
            return json;
        }

        public static string Hash(string s)
        {
            using (var sha = SHA1.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static void SafeWrite(string path, byte[] data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(tmp, data);
                if (File.Exists(path)) File.Delete(tmp); else File.Move(tmp, path);
            }
            catch { /* cache is best-effort */ }
        }
    }

    // ------------------------------------------------------------------ OpenStreetMap

    public sealed class OsmBuilding
    {
        public long Id;
        /// <summary>Rings as lat/lon pairs (closed: first == last). Outer first, then holes.</summary>
        public List<List<(double lat, double lon)>> Outers = new List<List<(double, double)>>();
        public List<List<(double lat, double lon)>> Inners = new List<List<(double, double)>>();
        public double Height = double.NaN, MinHeight;
        public bool HeightFromTags;
        public string Name, Use;
    }

    public sealed class OsmTree
    {
        public long Id; public double Lat, Lon;
        public double Height = double.NaN, CrownDiameter = double.NaN;
        public string Species;
    }

    public static class OsmParse
    {
        public static string Query(double s, double w, double n, double e, bool buildings, bool trees)
        {
            string bb = string.Format(CultureInfo.InvariantCulture, "({0:R},{1:R},{2:R},{3:R})", s, w, n, e);
            var sb = new StringBuilder("[out:json][timeout:120];(");
            if (buildings) sb.Append("way[\"building\"]" + bb + ";relation[\"building\"][\"type\"=\"multipolygon\"]" + bb + ";");
            if (trees) sb.Append("node[\"natural\"=\"tree\"]" + bb + ";");
            sb.Append(");out geom;");
            return sb.ToString();
        }

        public static void Parse(string json, double defaultHeight, double levelHeight,
            List<OsmBuilding> buildings, List<OsmTree> trees)
        {
            var root = JObject.Parse(json);
            var elems = root["elements"] as JArray;
            if (elems == null) return;
            foreach (JObject el in elems)
            {
                string type = (string)el["type"];
                var tags = el["tags"] as JObject;
                if (type == "node")
                {
                    if (tags == null || (string)tags["natural"] != "tree") continue;
                    trees.Add(new OsmTree
                    {
                        Id = (long)el["id"], Lat = (double)el["lat"], Lon = (double)el["lon"],
                        Height = ParseLength((string)tags["height"]),
                        CrownDiameter = ParseLength((string)tags["diameter_crown"]),
                        Species = (string)tags["species"] ?? (string)tags["genus"] ?? (string)tags["leaf_type"],
                    });
                    continue;
                }
                if (tags == null || tags["building"] == null) continue;
                var b = new OsmBuilding { Id = (long)el["id"], Name = (string)tags["name"], Use = (string)tags["building"] };
                if (type == "way")
                {
                    var ring = Ring(el["geometry"] as JArray);
                    if (ring != null && ring.Count >= 4) b.Outers.Add(ring);
                }
                else if (type == "relation")
                {
                    var outer = new List<List<(double, double)>>();
                    var inner = new List<List<(double, double)>>();
                    foreach (JObject m in (el["members"] as JArray) ?? new JArray())
                    {
                        if ((string)m["type"] != "way") continue;
                        var pts = Ring(m["geometry"] as JArray, close: false);
                        if (pts == null || pts.Count < 2) continue;
                        if ((string)m["role"] == "inner") inner.Add(pts); else outer.Add(pts);
                    }
                    b.Outers.AddRange(Stitch(outer));
                    b.Inners.AddRange(Stitch(inner));
                }
                if (b.Outers.Count == 0) continue;

                double h = ParseLength((string)tags["height"]);
                double lv = ParseNumber((string)tags["building:levels"]);
                if (!double.IsNaN(h)) { b.Height = h; b.HeightFromTags = true; }
                else if (!double.IsNaN(lv) && lv > 0)
                {
                    double roofLv = ParseNumber((string)tags["roof:levels"]);
                    b.Height = (lv + (double.IsNaN(roofLv) ? 0 : roofLv * 0.5)) * levelHeight;
                    b.HeightFromTags = true;
                }
                else b.Height = defaultHeight;

                double mh = ParseLength((string)tags["min_height"]);
                double ml = ParseNumber((string)tags["building:min_level"]);
                b.MinHeight = !double.IsNaN(mh) ? mh : (!double.IsNaN(ml) ? ml * levelHeight : 0.0);
                if (b.MinHeight >= b.Height) b.MinHeight = 0;
                buildings.Add(b);
            }
        }

        private static List<(double, double)> Ring(JArray g, bool close = true)
        {
            if (g == null) return null;
            var r = new List<(double, double)>(g.Count + 1);
            foreach (var p in g)
            {
                if (p == null || p.Type != JTokenType.Object || p["lat"] == null) continue;
                r.Add(((double)p["lat"], (double)p["lon"]));
            }
            if (close && r.Count > 2 && !Same(r[0], r[r.Count - 1])) r.Add(r[0]);
            return r;
        }

        /// <summary>Joins multipolygon member ways that share end points into closed rings.</summary>
        public static List<List<(double lat, double lon)>> Stitch(List<List<(double lat, double lon)>> parts)
        {
            var rings = new List<List<(double, double)>>();
            var open = new List<List<(double, double)>>();
            foreach (var p in parts)
            {
                if (p.Count >= 4 && Same(p[0], p[p.Count - 1])) rings.Add(p);
                else open.Add(new List<(double, double)>(p));
            }
            while (open.Count > 0)
            {
                var cur = open[0]; open.RemoveAt(0);
                bool grew = true;
                while (!Same(cur[0], cur[cur.Count - 1]) && grew)
                {
                    grew = false;
                    for (int i = 0; i < open.Count; i++)
                    {
                        var o = open[i];
                        var end = cur[cur.Count - 1];
                        if (Same(end, o[0])) { cur.AddRange(o.GetRange(1, o.Count - 1)); }
                        else if (Same(end, o[o.Count - 1])) { o.Reverse(); cur.AddRange(o.GetRange(1, o.Count - 1)); }
                        else continue;
                        open.RemoveAt(i); grew = true; break;
                    }
                }
                if (cur.Count >= 4 && Same(cur[0], cur[cur.Count - 1])) rings.Add(cur);
            }
            return rings;
        }

        private static bool Same((double, double) a, (double, double) b)
            => Math.Abs(a.Item1 - b.Item1) < 1e-9 && Math.Abs(a.Item2 - b.Item2) < 1e-9;

        /// <summary>Parses OSM lengths: "12", "12 m", "12.5m", "40'", "40 ft", "12;15" (first), "12,5". NaN if unparseable.</summary>
        public static double ParseLength(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return double.NaN;
            s = s.Split(';')[0].Trim().ToLowerInvariant().Replace(',', '.');
            var m = Regex.Match(s, @"^(-?\d+(?:\.\d+)?)\s*(m|metres|meters|ft|feet|')?\s*(?:(\d+(?:\.\d+)?)\s*(""|in))?$");
            if (!m.Success) return double.NaN;
            double v = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            string unit = m.Groups[2].Value;
            if (unit == "ft" || unit == "feet" || unit == "'")
            {
                v *= 0.3048;
                if (m.Groups[3].Success) v += double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) * 0.0254;
            }
            return v;
        }

        public static double ParseNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return double.NaN;
            s = s.Split(';')[0].Trim().Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
        }
    }
}
