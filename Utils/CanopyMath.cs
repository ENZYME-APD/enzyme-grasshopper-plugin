using System;
using System.Collections.Generic;

namespace Enzyme.Utils
{
    /// <summary>
    /// Rhino-free maths for treating tree crowns as porous volumes instead of solid shadow casters.
    ///
    /// Light passing through foliage is attenuated with the Beer–Lambert law:
    ///     T = exp(-k · LAD · L)
    /// where L is the path length through the crown (m), LAD the leaf area density (m²/m³) and
    /// k the extinction coefficient (0.5 for a spherical leaf-angle distribution).
    /// Same approach as VoxCity (Fujiwara et al., 2025, CEUS 102366) and ENVI-met / PALM; here it is
    /// applied to exact crown meshes rather than voxels, so there is no stair-stepping.
    /// </summary>
    public static class CanopyMath
    {
        /// <summary>Default extinction coefficient (spherical leaf-angle distribution).</summary>
        public const double DefaultK = 0.5;

        /// <summary>Fraction of a beam that passes through a crown path of the given optical depth.</summary>
        public static double Transmittance(double opticalDepth)
        {
            if (double.IsNaN(opticalDepth) || opticalDepth <= 0) return 1.0;
            return Math.Exp(-opticalDepth);
        }

        /// <summary>Optical depth k · LAD · L for one crown.</summary>
        public static double OpticalDepth(double pathLength, double lad, double k)
        {
            if (pathLength <= 0 || lad <= 0 || k <= 0) return 0.0;
            return k * lad * pathLength;
        }

        /// <summary>
        /// Length of a ray that lies inside a closed volume, from the distances (along the ray,
        /// measured from its origin) at which the ray crosses the volume's boundary.
        /// Crossings are sorted and near-duplicates (a ray through a shared mesh edge reports the
        /// same crossing twice) are merged. An odd count means the origin is inside the volume,
        /// so the first segment starts at 0. Crossings behind the origin are ignored and the
        /// result is clipped to maxDist.
        /// </summary>
        public static double PathLengthInside(IList<double> crossings, double maxDist, double mergeTol = 1e-6)
        {
            if (crossings == null || crossings.Count == 0) return 0.0;

            var d = new List<double>(crossings.Count);
            foreach (double c in crossings)
                if (!double.IsNaN(c) && c >= -mergeTol) d.Add(Math.Max(0.0, c));
            if (d.Count == 0) return 0.0;
            d.Sort();

            var u = new List<double>(d.Count) { d[0] };
            for (int i = 1; i < d.Count; i++)
                if (d[i] - u[u.Count - 1] > mergeTol) u.Add(d[i]);

            double total = 0.0;
            int start = 0;
            if (u.Count % 2 == 1)
            {
                // Origin inside the crown: first segment runs from the origin to the first crossing.
                total += Math.Min(u[0], maxDist);
                start = 1;
            }
            for (int i = start; i + 1 < u.Count; i += 2)
                total += Math.Min(u[i + 1], maxDist) - Math.Min(u[i], maxDist);
            return total;
        }
    }
}
