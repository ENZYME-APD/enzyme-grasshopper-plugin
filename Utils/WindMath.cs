using System;
using Rhino.Geometry;

namespace Enzyme.Utils
{
    /// <summary>
    /// Wind conventions shared by Fast CFD and Wind Rose.
    ///  * Direction = meteorological bearing the wind blows FROM, degrees, 0 = North, clockwise (EPW / weather data).
    ///  * North     = angle of true north from the model +Y axis, degrees, counter-clockwise (Ladybug convention).
    /// </summary>
    public static class WindMath
    {
        /// <summary>Unit vector in the model XY plane pointing along a compass bearing (0 = true N, clockwise).</summary>
        public static Vector3d BearingToModel(double bearingDeg, double northDeg)
        {
            double b = bearingDeg * Math.PI / 180.0;
            double x = Math.Sin(b), y = Math.Cos(b);          // true frame: N = +Y, E = +X
            double n = northDeg * Math.PI / 180.0;            // rotate CCW by north
            return new Vector3d(x * Math.Cos(n) - y * Math.Sin(n), x * Math.Sin(n) + y * Math.Cos(n), 0);
        }

        /// <summary>Unit flow vector (where the air goes) for wind blowing FROM fromBearingDeg.</summary>
        public static Vector3d FlowVector(double fromBearingDeg, double northDeg)
            => BearingToModel(fromBearingDeg + 180.0, northDeg);

        public static double Normalize(double deg)
        {
            deg %= 360.0;
            return deg < 0 ? deg + 360.0 : deg;
        }

        static readonly string[] Names16 = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

        /// <summary>Nearest 16-point compass name for a bearing.</summary>
        public static string CompassName(double deg)
            => Names16[(int)Math.Round(Normalize(deg) / 22.5) % 16];
    }
}
