using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Enzyme.Utils
{
    /// <summary>A named colour gradient (stops are evenly spaced from min to max).</summary>
    public sealed class PaletteDef
    {
        public string Name { get; }
        public bool Diverging { get; }
        public Color[] Stops { get; }

        public PaletteDef(string name, bool diverging, byte[,] rgb)
        {
            Name = name;
            Diverging = diverging;
            int n = rgb.GetLength(0);
            Stops = new Color[n];
            for (int i = 0; i < n; i++) Stops[i] = Color.FromArgb(255, rgb[i, 0], rgb[i, 1], rgb[i, 2]);
        }
    }

    /// <summary>A set of named value classes (e.g. UTCI stress, Lawson comfort) with fixed colours.</summary>
    public sealed class ClassScheme
    {
        public string Name { get; }
        public string Unit { get; }
        /// <summary>Upper bound of each class except the last (length = Names.Length - 1), ascending.</summary>
        public double[] Bounds { get; }
        public string[] Names { get; }
        public Color[] Colors { get; }

        public ClassScheme(string name, string unit, double[] bounds, string[] names, Color[] colors)
        {
            Name = name; Unit = unit; Bounds = bounds; Names = names; Colors = colors;
        }

        /// <summary>Index of the class a value falls in (values on a bound go to the upper class).</summary>
        public int ClassOf(double v)
        {
            int i = 0;
            while (i < Bounds.Length && v >= Bounds[i]) i++;
            return i;
        }
    }

    /// <summary>
    /// Shared colour palettes and class schemes for every analysis component.
    /// Palette stops are re-implemented from public sources: matplotlib viridis/magma/inferno/plasma (CC0),
    /// cividis (Nunez et al. 2018), Turbo (Google, Apache 2.0), ColorBrewer (Apache 2.0), cmocean (MIT),
    /// seaborn rocket/mako/vlag (BSD), Ladybug legend colours; list as compiled by Meerkat (T.-T. Tran, MIT).
    /// </summary>
    public static class Palettes
    {
        public static readonly PaletteDef[] All =
        {
            new PaletteDef("Ladybug", false, new byte[,] { {75,107,169}, {115,147,202}, {170,200,247}, {193,213,208}, {245,239,103}, {252,230,74}, {239,156,21}, {234,123,0}, {234,74,0}, {234,38,0} }),
            new PaletteDef("Viridis", false, new byte[,] { {68,1,84}, {72,36,117}, {65,68,135}, {53,95,141}, {42,120,142}, {33,145,140}, {34,168,132}, {68,191,112}, {122,209,81}, {189,223,38}, {253,231,37} }),
            new PaletteDef("Cividis", false, new byte[,] { {0,34,78}, {8,51,112}, {53,69,108}, {79,87,108}, {102,105,112}, {125,124,120}, {148,142,119}, {174,163,113}, {200,184,102}, {229,207,82}, {254,232,56} }),
            new PaletteDef("Magma", false, new byte[,] { {0,0,4}, {20,14,54}, {59,15,112}, {100,26,128}, {140,41,129}, {183,55,121}, {222,73,104}, {247,112,92}, {254,159,109}, {254,207,146}, {252,253,191} }),
            new PaletteDef("Inferno", false, new byte[,] { {0,0,4}, {22,11,57}, {66,10,104}, {106,23,110}, {147,38,103}, {188,55,84}, {221,81,58}, {243,120,25}, {252,165,10}, {246,215,70}, {252,255,164} }),
            new PaletteDef("Plasma", false, new byte[,] { {13,8,135}, {65,4,157}, {106,0,168}, {143,13,164}, {177,42,144}, {204,71,120}, {225,100,98}, {242,132,75}, {252,166,54}, {252,206,37}, {240,249,33} }),
            new PaletteDef("Turbo", false, new byte[,] { {48,18,59}, {69,89,203}, {62,155,254}, {25,213,205}, {70,248,132}, {164,252,60}, {225,221,55}, {254,164,49}, {240,91,18}, {195,37,3}, {122,4,3} }),
            new PaletteDef("Thermal", false, new byte[,] { {23,55,94}, {40,98,145}, {72,148,170}, {146,197,190}, {232,226,196}, {244,190,124}, {229,128,77}, {196,64,56}, {128,20,46} }),
            new PaletteDef("Wind", false, new byte[,] { {245,250,250}, {200,232,230}, {140,205,205}, {70,165,180}, {35,120,160}, {30,75,130}, {25,40,90} }),
            new PaletteDef("YlOrRd", false, new byte[,] { {255,255,204}, {255,241,169}, {254,225,135}, {254,202,102}, {254,171,73}, {253,140,60}, {252,91,46}, {237,46,33}, {212,16,32}, {176,0,38}, {128,0,38} }),
            new PaletteDef("Blues", false, new byte[,] { {247,251,255}, {227,238,249}, {208,225,242}, {183,212,234}, {148,196,223}, {106,174,214}, {74,152,201}, {46,126,188}, {23,100,171}, {8,74,145}, {8,48,107} }),
            new PaletteDef("Greens", false, new byte[,] { {247,252,245}, {233,247,229}, {211,238,205}, {184,227,178}, {152,213,148}, {115,196,118}, {75,176,98}, {47,151,78}, {21,127,59}, {0,100,40}, {0,68,27} }),
            new PaletteDef("Reds", false, new byte[,] { {255,245,240}, {254,229,216}, {253,202,181}, {252,171,143}, {252,138,106}, {251,105,74}, {241,68,50}, {217,37,35}, {188,20,26}, {152,12,19}, {103,0,13} }),
            new PaletteDef("Greys", false, new byte[,] { {255,255,255}, {243,243,243}, {226,226,226}, {206,206,206}, {181,181,181}, {149,149,149}, {122,122,122}, {95,95,95}, {64,64,64}, {29,29,29}, {0,0,0} }),
            new PaletteDef("Rocket", false, new byte[,] { {3,5,26}, {37,20,51}, {76,29,75}, {117,31,88}, {161,26,91}, {203,27,79}, {232,63,63}, {243,113,77}, {246,156,115}, {247,198,166}, {250,235,221} }),
            new PaletteDef("Mako", false, new byte[,] { {11,4,5}, {37,23,41}, {56,42,84}, {65,63,128}, {57,93,156}, {53,123,163}, {52,151,169}, {62,180,173}, {96,206,172}, {169,225,189}, {222,245,229} }),
            new PaletteDef("Ocean Thermal", false, new byte[,] { {4,35,51}, {15,50,106}, {64,52,159}, {103,67,150}, {139,83,141}, {177,95,130}, {214,108,108}, {243,130,77}, {252,166,60}, {247,208,69}, {232,250,91} }),
            new PaletteDef("Ocean Speed", false, new byte[,] { {255,253,205}, {238,223,152}, {216,197,95}, {184,178,47}, {142,162,11}, {95,146,12}, {50,128,31}, {15,107,43}, {16,84,44}, {25,59,35}, {23,35,19} }),
            new PaletteDef("Ocean Dense", false, new byte[,] { {230,241,241}, {188,220,229}, {150,197,226}, {123,173,228}, {115,144,227}, {120,113,213}, {121,84,187}, {116,58,152}, {104,36,113}, {83,21,70}, {54,14,36} }),
            new PaletteDef("Ocean Haline", false, new byte[,] { {42,24,108}, {41,39,162}, {13,78,150}, {24,102,140}, {45,124,137}, {60,147,135}, {74,170,129}, {101,193,114}, {148,211,93}, {208,224,109}, {253,239,154} }),
            new PaletteDef("Spectral", true, new byte[,] { {94,79,162}, {51,135,188}, {102,194,165}, {170,220,164}, {230,245,152}, {255,254,190}, {254,224,139}, {253,173,96}, {244,109,67}, {212,61,79}, {158,1,66} }),
            new PaletteDef("RdYlBu", true, new byte[,] { {49,54,149}, {69,116,179}, {116,173,209}, {170,216,233}, {224,243,248}, {255,254,190}, {254,224,144}, {253,173,96}, {244,109,67}, {214,47,39}, {165,0,38} }),
            new PaletteDef("Coolwarm", true, new byte[,] { {59,76,192}, {89,119,227}, {123,159,249}, {158,190,255}, {192,212,245}, {221,220,220}, {242,203,183}, {247,172,142}, {238,132,104}, {214,82,68}, {180,4,38} }),
            new PaletteDef("RdBu", true, new byte[,] { {5,48,97}, {32,101,171}, {67,147,195}, {144,196,221}, {209,229,240}, {247,246,246}, {253,219,199}, {243,164,129}, {214,96,77}, {177,24,43}, {103,0,31} }),
            new PaletteDef("Delta", true, new byte[,] { {17,72,110}, {39,116,156}, {98,166,190}, {170,210,220}, {232,242,244}, {250,248,245}, {250,224,210}, {240,170,140}, {214,106,82}, {168,50,48}, {110,18,34} }),
            new PaletteDef("Vlag", true, new byte[,] { {35,105,189}, {91,132,189}, {135,160,196}, {175,188,209}, {217,220,229}, {250,245,244}, {236,211,210}, {219,171,168}, {204,133,130}, {187,94,93}, {169,55,59} }),
            new PaletteDef("PuOr", true, new byte[,] { {45,0,75}, {83,38,135}, {128,115,172}, {177,170,209}, {216,218,235}, {247,247,246}, {254,224,182}, {252,183,97}, {224,130,20}, {178,87,6}, {127,59,8} }),
            new PaletteDef("BrBG", true, new byte[,] { {0,60,48}, {1,101,93}, {53,151,143}, {127,204,192}, {199,234,229}, {245,245,244}, {246,232,195}, {222,193,123}, {191,129,45}, {139,80,10}, {84,48,5} }),
            new PaletteDef("PiYG", true, new byte[,] { {142,1,82}, {196,26,124}, {222,119,174}, {241,181,217}, {253,224,239}, {247,247,246}, {230,245,208}, {183,224,133}, {127,188,65}, {76,145,33}, {39,100,25} }),
            new PaletteDef("Balance", true, new byte[,] { {24,28,67}, {41,62,151}, {22,112,188}, {89,156,187}, {170,194,203}, {241,236,235}, {220,178,164}, {202,118,91}, {179,56,38}, {126,14,41}, {60,9,18} }),
            new PaletteDef("Curl", true, new byte[,] { {21,29,68}, {28,77,96}, {17,125,121}, {92,167,134}, {182,203,176}, {253,245,244}, {230,184,162}, {211,118,105}, {174,64,96}, {117,25,93}, {52,13,53} }),
            new PaletteDef("Urban Heat", false, new byte[,] { {52,73,94}, {84,110,122}, {130,150,150}, {190,190,170}, {235,215,170}, {240,180,120}, {225,130,80}, {190,80,60}, {130,40,45} }),

        };

        public static IEnumerable<string> Names => All.Select(p => p.Name);
        public static IEnumerable<string> DivergingNames => All.Where(p => p.Diverging).Select(p => p.Name);

        /// <summary>Find a palette by name (case/space-insensitive). A trailing " r" or "_r" reverses it.</summary>
        public static bool TryGet(string name, out Color[] stops)
        {
            stops = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            string key = Norm(name.Replace("(diverging)", ""));
            bool rev = false;
            if (key.EndsWith("_r") || key.EndsWith("reversed"))
            {
                rev = true;
                key = key.EndsWith("_r") ? key.Substring(0, key.Length - 2) : key.Substring(0, key.Length - 8);
            }
            foreach (var p in All)
            {
                if (Norm(p.Name) != key) continue;
                stops = (Color[])p.Stops.Clone();
                if (rev) Array.Reverse(stops);
                return true;
            }
            int idx;
            if (int.TryParse(key, out idx) && All.Length > 0)
            {
                stops = (Color[])All[((idx % All.Length) + All.Length) % All.Length].Stops.Clone();
                return true;
            }
            return false;
        }

        public static Color[] Get(string name, string fallback = "Ladybug")
        {
            Color[] s;
            if (TryGet(name, out s)) return s;
            TryGet(fallback, out s);
            return s;
        }

        private static string Norm(string s) => s.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");

        /// <summary>Colour at t (0..1) along a gradient, linear in RGB between evenly spaced stops.</summary>
        public static Color Sample(IList<Color> stops, double t)
        {
            if (stops == null || stops.Count == 0) return Color.Gray;
            if (stops.Count == 1 || double.IsNaN(t)) return stops[0];
            if (t <= 0) return stops[0];
            if (t >= 1) return stops[stops.Count - 1];
            double f = t * (stops.Count - 1);
            int i = (int)f;
            double u = f - i;
            Color a = stops[i], b = stops[i + 1];
            return Color.FromArgb(255,
                (int)Math.Round(a.R + (b.R - a.R) * u),
                (int)Math.Round(a.G + (b.G - a.G) * u),
                (int)Math.Round(a.B + (b.B - a.B) * u));
        }

        /// <summary>256-entry lookup table (fast colouring of large meshes).</summary>
        public static Color[] Lut(IList<Color> stops, int n = 256)
        {
            var lut = new Color[n];
            for (int i = 0; i < n; i++) lut[i] = Sample(stops, i / (double)(n - 1));
            return lut;
        }

        /// <summary>n evenly spaced colours along the gradient (for legends / bands).</summary>
        public static Color[] Resample(IList<Color> stops, int n)
        {
            var o = new Color[Math.Max(n, 1)];
            for (int i = 0; i < o.Length; i++) o[i] = Sample(stops, o.Length == 1 ? 0.5 : i / (double)(o.Length - 1));
            return o;
        }

        // ---------------- class schemes ----------------

        /// <summary>UTCI thermal stress categories (Blazejczyk et al. 2012), degrees C.</summary>
        public static readonly ClassScheme Utci = new ClassScheme("UTCI", "°C",
            new double[] { -40, -27, -13, 0, 9, 26, 32, 38, 46 },
            new[] { "Extreme cold stress", "Very strong cold stress", "Strong cold stress", "Moderate cold stress",
                    "Slight cold stress", "No thermal stress", "Moderate heat stress", "Strong heat stress",
                    "Very strong heat stress", "Extreme heat stress" },
            new[] { C(23,36,107), C(38,72,160), C(50,120,200), C(110,170,225), C(175,215,240),
                    C(160,210,140), C(253,205,95), C(247,140,45), C(218,55,35), C(130,10,25) });

        /// <summary>Lawson (LDDC) pedestrian wind comfort, gust-equivalent mean speed in m/s.</summary>
        public static readonly ClassScheme Lawson = new ClassScheme("Lawson LDDC", "m/s",
            new double[] { 4, 6, 8, 10 },
            new[] { "Sitting", "Standing", "Strolling", "Business walking", "Uncomfortable" },
            new[] { C(43,131,186), C(171,221,164), C(255,255,191), C(253,174,97), C(215,25,28) });

        /// <summary>Environmental noise bands (WHO / END reporting style), dB(A).</summary>
        public static readonly ClassScheme Noise = new ClassScheme("Noise", "dB(A)",
            new double[] { 45, 50, 55, 60, 65, 70, 75 },
            new[] { "< 45", "45-50", "50-55", "55-60", "60-65", "65-70", "70-75", ">= 75" },
            new[] { C(160,186,191), C(184,214,209), C(206,228,204), C(226,242,191), C(243,198,131),
                    C(232,126,77), C(205,70,62), C(161,26,77) });

        public static readonly ClassScheme[] Schemes = { Utci, Lawson, Noise };

        public static bool TryGetScheme(string name, out ClassScheme scheme)
        {
            scheme = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            string k = name.Trim().ToLowerInvariant();
            foreach (var s in Schemes)
                if (s.Name.ToLowerInvariant().StartsWith(k) || k.StartsWith(s.Name.ToLowerInvariant().Split(' ')[0]))
                { scheme = s; return true; }
            return false;
        }

        private static Color C(int r, int g, int b) => Color.FromArgb(255, r, g, b);
    }
}
