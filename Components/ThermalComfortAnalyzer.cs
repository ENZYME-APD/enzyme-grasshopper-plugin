using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Rhino.Geometry;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class ThermalComfortAnalyzer : GH_Component
    {
        // Apparent Temperature (Steadman 1994), as used by the Australian Bureau of
        // Meteorology - a real, documented "feels like" formula combining air temperature,
        // humidity, and wind speed into a single value, rather than an invented score.
        // e = vapor pressure (hPa); AT = apparent temperature (deg C).
        // Reference: http://www.bom.gov.au/info/thermal_stress/
        private const double VAPOR_PRESSURE_COEFFICIENT = 6.105;
        private const double VAPOR_PRESSURE_EXP_A = 17.27;
        private const double VAPOR_PRESSURE_EXP_B = 237.7;
        private const double AT_HUMIDITY_COEFFICIENT = 0.33;
        private const double AT_WIND_COEFFICIENT = 0.70;
        private const double AT_CONSTANT = 4.00;

        private const double DEFAULT_IDEAL_TEMPERATURE = 22.0;
        private const double DEFAULT_COMFORT_TOLERANCE = 0.1;

        public ThermalComfortAnalyzer()
            : base("Thermal Comfort Analyzer", "ThermalComfort",
                "Maps wind-engine velocity samples onto apparent (\"feels like\") temperature and locates the best/worst comfort points, without requiring a regular grid.",
                "Enzyme", "Terrain")
        {
            this.Message = "ThermalComfort\n-- WAITING --";
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("ThermalComfortAnalyzer.png");
            }
        }

        public override Guid ComponentGuid => new Guid("df36188d-f244-4395-9b05-6927c0ca5dab");

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                PerformAutoWire(document);
            }
        }

                private void PerformAutoWire(GH_Document document)
        {
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 0, false, 362, -159);
            Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 1, "mesh", 252, -108);
            Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 2, "data", 251, -74);
            Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 3, "point", 252, -34);
            
            Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 4, 0, 100, 50, 398, -11);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, -10.0, 45.0, 20.0, 416, 29);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, -10.0, 45.0, DEFAULT_IDEAL_TEMPERATURE, 439, 69);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 1.0, DEFAULT_COMFORT_TOLERANCE, 441, 109);
            
            Enzyme.Utils.AutoWireHelper.WireGeneratedColorPalette(this, document, 8, 313, 228);
            
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "point", 189, -72);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "point", 189, -36);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "mesh", 188, 0);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "number", 187, 36);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Global execution toggle switch", GH_ParamAccess.item, false);
            pManager.AddMeshParameter("TerrainMesh", "TerrainMesh", "The underlying site topography - its own vertices/connectivity are reused directly for ComfortMesh, no separate grid is built", GH_ParamAccess.item);
            pManager.AddNumberParameter("VelocityValues", "VelocityValues", "Raw wind speeds (m/s), aligned index-for-index with TagPoints - e.g. from the wind engine's VelocityValues output", GH_ParamAccess.list);
            pManager.AddPointParameter("TagPoints", "TagPoints", "Sample points matching VelocityValues index-for-index - e.g. from the wind engine's TagPoints output", GH_ParamAccess.list);
            pManager.AddNumberParameter("Humidity", "Humidity", "Relative humidity (%), currently a single constant across the whole site", GH_ParamAccess.item, 50.0);
            pManager.AddNumberParameter("Temperature", "Temperature", "Ambient dry-bulb air temperature (deg C), currently a single constant across the whole site", GH_ParamAccess.item, 20.0);
            pManager.AddNumberParameter("IdealTemperature", "IdealTemp", "The apparent temperature (deg C) considered ideal/neutral comfort - deviation from this defines best/worst", GH_ParamAccess.item, DEFAULT_IDEAL_TEMPERATURE);
            pManager.AddNumberParameter("ComfortTolerance", "Tolerance", "Fraction (0-1) of the observed deviation-from-ideal range within which a point still counts as Best (near the minimum) or Worst (near the maximum)", GH_ParamAccess.item, DEFAULT_COMFORT_TOLERANCE);
            pManager.AddColourParameter("CustomColors", "CustomColors", "Custom color spectrum override for the comfort mesh", GH_ParamAccess.list);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("BestComfortPoints", "Best", "Point(s) with apparent temperature closest to IdealTemperature, within ComfortTolerance of the single best", GH_ParamAccess.list);
            pManager.AddPointParameter("WorstComfortPoints", "Worst", "Point(s) with apparent temperature furthest from IdealTemperature, within ComfortTolerance of the single worst", GH_ParamAccess.list);
            pManager.AddMeshParameter("ComfortMesh", "ComfortMesh", "The input terrain mesh, vertex-colored by comfort deviation - same connectivity as TerrainMesh, no new grid/UVs", GH_ParamAccess.item);
            pManager.AddNumberParameter("ComfortValues", "ComfortValues", "Raw apparent temperature (deg C) per terrain vertex, aligned with ComfortMesh's vertex order", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }

        private static double ApparentTemperature(double tempC, double humidityPct, double windSpeedMs)
        {
            double vaporPressure = (humidityPct / 100.0) * VAPOR_PRESSURE_COEFFICIENT
                * Math.Exp((VAPOR_PRESSURE_EXP_A * tempC) / (VAPOR_PRESSURE_EXP_B + tempC));
            return tempC + (AT_HUMIDITY_COEFFICIENT * vaporPressure) - (AT_WIND_COEFFICIENT * windSpeedMs) - AT_CONSTANT;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool execute = false;
            DA.GetData(0, ref execute);

            Mesh terrain = null;
            DA.GetData(1, ref terrain);

            List<double> velocityValues = new List<double>();
            DA.GetDataList(2, velocityValues);

            List<Point3d> tagPoints = new List<Point3d>();
            DA.GetDataList(3, tagPoints);

            double humidity = 50.0;
            DA.GetData(4, ref humidity);

            double temperature = 20.0;
            DA.GetData(5, ref temperature);

            double idealTemperature = DEFAULT_IDEAL_TEMPERATURE;
            DA.GetData(6, ref idealTemperature);

            double comfortTolerance = DEFAULT_COMFORT_TOLERANCE;
            DA.GetData(7, ref comfortTolerance);
            comfortTolerance = Math.Max(0.0, Math.Min(1.0, comfortTolerance));

            List<Color> userColors = new List<Color>();
            DA.GetDataList(8, userColors);

            List<Point3d> bestPoints = new List<Point3d>();
            List<Point3d> worstPoints = new List<Point3d>();
            Mesh comfortMesh = new Mesh();
            List<double> comfortValues = new List<double>();

            if (execute && terrain != null && tagPoints.Count > 0 && velocityValues.Count == tagPoints.Count)
            {
                // --- Best/Worst: evaluated directly on the TagPoints/VelocityValues pairs
                // themselves, since they're already one-to-one - no mesh mapping needed here.
                int n = tagPoints.Count;
                double[] deviation = new double[n];
                double minDev = double.MaxValue;
                double maxDev = double.MinValue;

                for (int i = 0; i < n; i++)
                {
                    double at = ApparentTemperature(temperature, humidity, velocityValues[i]);
                    deviation[i] = Math.Abs(at - idealTemperature);
                    if (deviation[i] < minDev) minDev = deviation[i];
                    if (deviation[i] > maxDev) maxDev = deviation[i];
                }

                double devRange = maxDev - minDev;
                double tol = devRange < 1e-9 ? 0.0 : devRange * comfortTolerance;

                for (int i = 0; i < n; i++)
                {
                    if (deviation[i] <= minDev + tol) bestPoints.Add(tagPoints[i]);
                    if (deviation[i] >= maxDev - tol) worstPoints.Add(tagPoints[i]);
                }

                // --- ComfortMesh: reuse the terrain mesh's own vertices/faces directly (no new
                // grid, no UVs) - each vertex finds its nearest TagPoint via an O(1) hash-grid
                // lookup (same technique as the wind engine's streamline routing), not a brute
                // O(vertices x points) scan, per the project's own spatial-search guidance.
                comfortMesh = terrain.DuplicateMesh();
                comfortMesh.VertexColors.Clear();

                BoundingBox tagBounds = new BoundingBox(tagPoints);
                double bboxArea = Math.Max((tagBounds.Max.X - tagBounds.Min.X) * (tagBounds.Max.Y - tagBounds.Min.Y), 1e-6);
                double cellSize = Math.Max(Math.Sqrt(bboxArea / n), 0.01);

                Dictionary<(int, int), List<int>> cellLookup = new Dictionary<(int, int), List<int>>(n);
                Func<Point3d, (int, int)> cellOf = (p) => (
                    (int)Math.Floor((p.X - tagBounds.Min.X) / cellSize),
                    (int)Math.Floor((p.Y - tagBounds.Min.Y) / cellSize));

                for (int i = 0; i < n; i++)
                {
                    var key = cellOf(tagPoints[i]);
                    List<int> bucket;
                    if (!cellLookup.TryGetValue(key, out bucket))
                    {
                        bucket = new List<int>();
                        cellLookup[key] = bucket;
                    }
                    bucket.Add(i);
                }

                int vertCount = comfortMesh.Vertices.Count;
                double[] vertexAT = new double[vertCount];

                Parallel.For(0, vertCount, vi =>
                {
                    Point3d vPt = comfortMesh.Vertices[vi];
                    var (cx, cy) = cellOf(vPt);

                    int bestIdx = -1;
                    double bestDist = double.MaxValue;
                    // Widen the ring until a candidate is found - handles terrain vertices that
                    // fall outside the TagPoints' own footprint (e.g. beyond the analysis grid).
                    for (int ring = 0; ring <= 5 && bestIdx == -1; ring++)
                    {
                        for (int dx = -ring; dx <= ring; dx++)
                        {
                            for (int dy = -ring; dy <= ring; dy++)
                            {
                                if (ring > 0 && Math.Abs(dx) != ring && Math.Abs(dy) != ring) continue; // only the new outer shell
                                List<int> bucket;
                                if (cellLookup.TryGetValue((cx + dx, cy + dy), out bucket))
                                {
                                    foreach (int idx in bucket)
                                    {
                                        double d = vPt.DistanceToSquared(tagPoints[idx]);
                                        if (d < bestDist)
                                        {
                                            bestDist = d;
                                            bestIdx = idx;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (bestIdx == -1)
                    {
                        // Extremely sparse TagPoints relative to cell size - fall back to a direct
                        // scan for this one vertex only, rather than leaving it unmapped.
                        for (int i = 0; i < n; i++)
                        {
                            double d = vPt.DistanceToSquared(tagPoints[i]);
                            if (d < bestDist) { bestDist = d; bestIdx = i; }
                        }
                    }

                    vertexAT[vi] = ApparentTemperature(temperature, humidity, velocityValues[bestIdx]);
                });

                double meshMinDev = double.MaxValue, meshMaxDev = double.MinValue;
                double[] vertexDeviation = new double[vertCount];
                for (int vi = 0; vi < vertCount; vi++)
                {
                    vertexDeviation[vi] = Math.Abs(vertexAT[vi] - idealTemperature);
                    if (vertexDeviation[vi] < meshMinDev) meshMinDev = vertexDeviation[vi];
                    if (vertexDeviation[vi] > meshMaxDev) meshMaxDev = vertexDeviation[vi];
                }
                double meshDevRange = meshMaxDev - meshMinDev;
                if (meshDevRange < 0.01) meshDevRange = 1.0;

                for (int vi = 0; vi < vertCount; vi++)
                {
                    double intensity = (vertexDeviation[vi] - meshMinDev) / meshDevRange;
                    intensity = Math.Min(1.0, Math.Max(0.0, intensity));
                    Color mappedColor;

                    if (userColors.Count >= 2)
                    {
                        double position = intensity * (userColors.Count - 1);
                        int lowIdx = (int)Math.Floor(position);
                        int highIdx = (int)Math.Ceiling(position);
                        double blend = position - lowIdx;

                        Color c1 = userColors[lowIdx];
                        Color c2 = userColors[highIdx];

                        int r = (int)(c1.R * (1.0 - blend) + c2.R * blend);
                        int g = (int)(c1.G * (1.0 - blend) + c2.G * blend);
                        int b = (int)(c1.B * (1.0 - blend) + c2.B * blend);
                        mappedColor = Color.FromArgb(255, r, g, b);
                    }
                    else if (userColors.Count == 1)
                    {
                        mappedColor = userColors[0];
                    }
                    else
                    {
                        // Default ramp: green (comfortable, low deviation) -> red (uncomfortable, high deviation)
                        int r = (int)(20 * (1.0 - intensity) + 220 * intensity);
                        int g = (int)(180 * (1.0 - intensity) + 30 * intensity);
                        int b = (int)(60 * (1.0 - intensity) + 30 * intensity);
                        mappedColor = Color.FromArgb(255, r, g, b);
                    }

                    comfortMesh.VertexColors.Add(mappedColor);
                    comfortValues.Add(vertexAT[vi]);
                }
            }

            DA.SetDataList(0, bestPoints);
            DA.SetDataList(1, worstPoints);
            DA.SetData(2, comfortMesh);
            DA.SetDataList(3, comfortValues);

            DA.SetData(4, "THERMAL COMFORT ANALYZER\n"
                + "\n"
                + "HOW IT WORKS:\n"
                + "Combines wind-engine velocity samples with a constant humidity and temperature into Apparent Temperature (Steadman 1994 / Australian BOM formula), then maps that back onto the terrain mesh's own vertices via nearest-point lookup - no separate grid or UVs required.\n\n"
                + "INTERPRETATION & IMPORTANCE:\n"
                + "Best/Worst points flag where pedestrian comfort is strongest or weakest relative to IdealTemperature. Use this after wind analysis to check whether high-speed corridors or sheltered wakes actually help or hurt outdoor comfort once temperature and humidity are factored in.");

            if (execute)
            {
                Message = bestPoints.Count > 0 || worstPoints.Count > 0
                    ? $"{this.NickName}\nBest: {bestPoints.Count} pt(s)\nWorst: {worstPoints.Count} pt(s)"
                    : $"{this.NickName}\nNo valid input data";
            }
            else
            {
                Message = $"{this.NickName}\nSTATUS: SLEEPING";
            }
        }
    }
}
