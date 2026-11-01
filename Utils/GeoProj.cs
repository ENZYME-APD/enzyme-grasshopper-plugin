using System;

namespace Enzyme.Utils
{
    /// <summary>
    /// WGS84 ⇄ UTM, Krüger series to n³ (sub-millimetre inside a zone). Rhino-free.
    /// </summary>
    public static class GeoProj
    {
        private const double A = 6378137.0;
        private const double F = 1.0 / 298.257223563;
        private const double K0 = 0.9996;
        private const double E0 = 500000.0;
        private static readonly double N = F / (2 - F);
        private static readonly double Ecc = Math.Sqrt(F * (2 - F));
        private static readonly double AA = A / (1 + N) * (1 + N * N / 4 + N * N * N * N / 64);
        private static readonly double[] Alpha =
        {
            N / 2 - 2 * N * N / 3 + 5 * N * N * N / 16,
            13 * N * N / 48 - 3 * N * N * N / 5,
            61 * N * N * N / 240
        };
        private static readonly double[] Beta =
        {
            N / 2 - 2 * N * N / 3 + 37 * N * N * N / 96,
            N * N / 48 + N * N * N / 15,
            17 * N * N * N / 480
        };
        private static readonly double[] Delta =
        {
            2 * N - 2 * N * N / 3 - 2 * N * N * N,
            7 * N * N / 3 - 8 * N * N * N / 5,
            56 * N * N * N / 15
        };

        /// <summary>UTM zone for a location, including the Norway and Svalbard exceptions.</summary>
        public static int UtmZone(double lat, double lon)
        {
            lon = ((lon + 180) % 360 + 360) % 360 - 180;
            int zone = (int)Math.Floor((lon + 180) / 6) + 1;
            if (zone > 60) zone = 60;
            if (lat >= 56 && lat < 64 && lon >= 3 && lon < 12) zone = 32;
            if (lat >= 72 && lat < 84)
            {
                if (lon >= 0 && lon < 9) zone = 31;
                else if (lon >= 9 && lon < 21) zone = 33;
                else if (lon >= 21 && lon < 33) zone = 35;
                else if (lon >= 33 && lon < 42) zone = 37;
            }
            return zone;
        }

        /// <summary>EPSG code of a WGS84 UTM zone (326xx north, 327xx south).</summary>
        public static int UtmEpsg(int zone, bool north) => (north ? 32600 : 32700) + zone;

        private static double CentralMeridian(int zone) => (zone - 1) * 6 - 180 + 3;

        public static void ToUtm(double lat, double lon, int zone, bool north, out double easting, out double northing)
        {
            double phi = lat * Math.PI / 180.0;
            double lam = (lon - CentralMeridian(zone)) * Math.PI / 180.0;
            double s = Math.Sin(phi);
            double t = Math.Sinh(Atanh(s) - Ecc * Atanh(Ecc * s));
            double xiP = Math.Atan2(t, Math.Cos(lam));
            double etaP = Atanh(Math.Sin(lam) / Math.Sqrt(1 + t * t));
            double xi = xiP, eta = etaP;
            for (int j = 1; j <= 3; j++)
            {
                xi += Alpha[j - 1] * Math.Sin(2 * j * xiP) * Math.Cosh(2 * j * etaP);
                eta += Alpha[j - 1] * Math.Cos(2 * j * xiP) * Math.Sinh(2 * j * etaP);
            }
            easting = E0 + K0 * AA * eta;
            northing = (north ? 0.0 : 10000000.0) + K0 * AA * xi;
        }

        public static void FromUtm(double easting, double northing, int zone, bool north, out double lat, out double lon)
        {
            double xi = (northing - (north ? 0.0 : 10000000.0)) / (K0 * AA);
            double eta = (easting - E0) / (K0 * AA);
            double xiP = xi, etaP = eta;
            for (int j = 1; j <= 3; j++)
            {
                xiP -= Beta[j - 1] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta);
                etaP -= Beta[j - 1] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta);
            }
            double chi = Math.Asin(Math.Sin(xiP) / Math.Cosh(etaP));
            double phi = chi;
            for (int j = 1; j <= 3; j++) phi += Delta[j - 1] * Math.Sin(2 * j * chi);
            lat = phi * 180.0 / Math.PI;
            lon = CentralMeridian(zone) + Math.Atan2(Math.Sinh(etaP), Math.Cos(xiP)) * 180.0 / Math.PI;
        }

        private static double Atanh(double x) => 0.5 * Math.Log((1 + x) / (1 - x));
    }

    /// <summary>
    /// A square site in a local metric frame: UTM of the site centre, shifted so the centre is at 0,0
    /// (keeps Rhino precision; add Easting/Northing back to georeference).
    /// </summary>
    public sealed class LocalFrame
    {
        public double Lat { get; }
        public double Lon { get; }
        public int Zone { get; }
        public bool North { get; }
        public double Easting { get; }
        public double Northing { get; }
        public int Epsg => GeoProj.UtmEpsg(Zone, North);

        public LocalFrame(double lat, double lon)
        {
            Lat = lat; Lon = lon;
            Zone = GeoProj.UtmZone(lat, lon);
            North = lat >= 0;
            GeoProj.ToUtm(lat, lon, Zone, North, out double e, out double n);
            Easting = e; Northing = n;
        }

        public void ToLocal(double lat, double lon, out double x, out double y)
        {
            GeoProj.ToUtm(lat, lon, Zone, North, out double e, out double n);
            x = e - Easting; y = n - Northing;
        }

        public void ToLatLon(double x, double y, out double lat, out double lon)
            => GeoProj.FromUtm(x + Easting, y + Northing, Zone, North, out lat, out lon);

        /// <summary>Lat/lon bounds (south, west, north, east) of a local rectangle, sampled along its edges.</summary>
        public void Bounds(double halfX, double halfY, out double s, out double w, out double n, out double e)
        {
            s = w = double.MaxValue; n = e = double.MinValue;
            for (int i = 0; i <= 8; i++)
            {
                double t = -1 + i / 4.0;
                foreach (var p in new[] { (t * halfX, -halfY), (t * halfX, halfY), (-halfX, t * halfY), (halfX, t * halfY) })
                {
                    ToLatLon(p.Item1, p.Item2, out double la, out double lo);
                    s = Math.Min(s, la); n = Math.Max(n, la); w = Math.Min(w, lo); e = Math.Max(e, lo);
                }
            }
        }
    }
}
