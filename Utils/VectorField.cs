using System;
using System.Collections.Generic;

namespace Enzyme.Utils
{
    /// <summary>Samples a 2.5D vector field: (x, y) -> (u, v, ground z). Returns false outside / in obstacles.</summary>
    public interface IVectorField2D
    {
        bool Sample(double x, double y, out double u, out double v, out double z);
    }

    /// <summary>
    /// Regular XY grid field (e.g. Fast CFD output: one point per cell, obstacle cells missing).
    /// Lookup is O(1) by index with bilinear interpolation, instead of a radius search per step.
    /// </summary>
    public sealed class GridField2D : IVectorField2D
    {
        private readonly double _x0, _y0, _dx, _dy;
        private readonly int _nx, _ny;
        private readonly double[] _u, _v, _z;
        private readonly bool[] _ok;

        public int Nx => _nx;
        public int Ny => _ny;
        public double CellX => _dx;
        public double CellY => _dy;

        private GridField2D(double x0, double y0, double dx, double dy, int nx, int ny)
        {
            _x0 = x0; _y0 = y0; _dx = dx; _dy = dy; _nx = nx; _ny = ny;
            int n = nx * ny;
            _u = new double[n]; _v = new double[n]; _z = new double[n]; _ok = new bool[n];
        }

        /// <summary>
        /// Builds a grid if the points sit on a regular XY lattice (holes allowed). Returns null otherwise,
        /// so the caller can fall back to scattered interpolation.
        /// </summary>
        public static GridField2D TryBuild(IList<double> px, IList<double> py, IList<double> pz, IList<double> u, IList<double> v)
        {
            int n = px.Count;
            if (n < 4) return null;
            double dx = MinSpacing(px), dy = MinSpacing(py);
            if (dx <= 0 || dy <= 0) return null;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (px[i] < x0) x0 = px[i]; if (px[i] > x1) x1 = px[i];
                if (py[i] < y0) y0 = py[i]; if (py[i] > y1) y1 = py[i];
            }
            int nx = (int)Math.Round((x1 - x0) / dx) + 1, ny = (int)Math.Round((y1 - y0) / dy) + 1;
            if (nx < 2 || ny < 2) return null;
            if ((long)nx * ny > 50_000_000L || (long)nx * ny < n) return null;
            var g = new GridField2D(x0, y0, dx, dy, nx, ny);
            double tolx = 0.05 * dx, toly = 0.05 * dy;
            for (int k = 0; k < n; k++)
            {
                double fi = (px[k] - x0) / dx, fj = (py[k] - y0) / dy;
                int i = (int)Math.Round(fi), j = (int)Math.Round(fj);
                if (Math.Abs(fi - i) * dx > tolx || Math.Abs(fj - j) * dy > toly) return null;   // not a lattice
                int q = j * nx + i;
                if (g._ok[q]) return null;                                                     // two points in one cell
                g._ok[q] = true; g._u[q] = u[k]; g._v[q] = v[k]; g._z[q] = pz[k];
            }
            return g;
        }

        // most frequent gap between sorted distinct coordinates = lattice spacing
        private static double MinSpacing(IList<double> c)
        {
            var s = new double[c.Count];
            c.CopyTo(s, 0);
            Array.Sort(s);
            double span = s[s.Length - 1] - s[0];
            if (span <= 0) return 0;
            double eps = 1e-6 * span;
            var gaps = new List<double>();
            for (int i = 1; i < s.Length; i++)
            {
                double d = s[i] - s[i - 1];
                if (d > eps) gaps.Add(d);
            }
            if (gaps.Count == 0) return 0;
            gaps.Sort();
            double best = 0; int bestCount = 0;
            for (int i = 0; i < gaps.Count;)
            {
                int j = i;
                while (j < gaps.Count && gaps[j] - gaps[i] <= 0.01 * gaps[i]) j++;
                if (j - i > bestCount) { bestCount = j - i; best = gaps[(i + j) / 2]; }
                i = j;
            }
            return best;
        }

        /// <summary>Bilinear sample. Needs at least 3 of the 4 surrounding cells (stops at walls).</summary>
        public bool Sample(double x, double y, out double u, out double v, out double z)
        {
            u = v = 0; z = 0;
            double fx = (x - _x0) / _dx, fy = (y - _y0) / _dy;
            if (fx < 0 || fy < 0 || fx > _nx - 1 || fy > _ny - 1) return false;
            int i = Math.Min((int)fx, _nx - 2), j = Math.Min((int)fy, _ny - 2);
            if (_nx == 1) i = 0;
            if (_ny == 1) j = 0;
            double tx = fx - i, ty = fy - j;
            double wsum = 0; int cnt = 0;
            for (int c = 0; c < 4; c++)
            {
                int ii = i + (c & 1), jj = j + (c >> 1);
                if (ii >= _nx || jj >= _ny) continue;
                int q = jj * _nx + ii;
                if (!_ok[q]) continue;
                double w = ((c & 1) == 1 ? tx : 1 - tx) * ((c >> 1) == 1 ? ty : 1 - ty);
                u += w * _u[q]; v += w * _v[q]; z += w * _z[q];
                wsum += w; cnt++;
            }
            if (cnt < 3 || wsum < 1e-12) return false;
            u /= wsum; v /= wsum; z /= wsum;
            return true;
        }
    }

    /// <summary>Streamline integration (RK4 on the unit direction field, i.e. arc-length steps).</summary>
    public static class StreamTracer
    {
        /// <summary>
        /// Traces from (x0, y0). direction = +1 downstream, -1 upstream. Output points carry the field's
        /// ground z; speeds are the local field magnitudes at each point. Stops when leaving the field,
        /// below minSpeed, on a closed loop, or after maxSteps.
        /// </summary>
        public static void Trace(IVectorField2D field, double x0, double y0, double step, int maxSteps,
                                 double direction, double minSpeed, List<double[]> pts, List<double> speeds)
        {
            double u, v, z;
            if (!field.Sample(x0, y0, out u, out v, out z)) return;
            double sp = Math.Sqrt(u * u + v * v);
            if (sp < minSpeed) return;
            pts.Add(new[] { x0, y0, z });
            speeds.Add(sp);
            double x = x0, y = y0;
            double h = step * Math.Sign(direction == 0 ? 1 : direction);
            double closeTol2 = (0.5 * step) * (0.5 * step);

            for (int s = 0; s < maxSteps; s++)
            {
                double k1x, k1y, k2x, k2y, k3x, k3y, k4x, k4y;
                if (!Dir(field, x, y, minSpeed, out k1x, out k1y)) break;
                if (!Dir(field, x + 0.5 * h * k1x, y + 0.5 * h * k1y, minSpeed, out k2x, out k2y)) break;
                if (!Dir(field, x + 0.5 * h * k2x, y + 0.5 * h * k2y, minSpeed, out k3x, out k3y)) break;
                if (!Dir(field, x + h * k3x, y + h * k3y, minSpeed, out k4x, out k4y)) break;
                double nx = x + h / 6.0 * (k1x + 2 * k2x + 2 * k3x + k4x);
                double ny = y + h / 6.0 * (k1y + 2 * k2y + 2 * k3y + k4y);
                if (!field.Sample(nx, ny, out u, out v, out z)) break;
                sp = Math.Sqrt(u * u + v * v);
                if (sp < minSpeed) break;
                double mx = nx - x, my = ny - y;
                if (mx * mx + my * my < 1e-6 * step * step) break;             // stuck (stagnation point)
                x = nx; y = ny;
                pts.Add(new[] { x, y, z });
                speeds.Add(sp);
                if (s > 8)
                {
                    double cx = x - x0, cy = y - y0;
                    if (cx * cx + cy * cy < closeTol2) break;                      // closed vortex loop
                }
            }
        }

        private static bool Dir(IVectorField2D f, double x, double y, double minSpeed, out double dx, out double dy)
        {
            dx = dy = 0;
            double u, v, z;
            if (!f.Sample(x, y, out u, out v, out z)) return false;
            double s = Math.Sqrt(u * u + v * v);
            if (s < minSpeed || s < 1e-12) return false;
            dx = u / s; dy = v / s;
            return true;
        }
    }
}
