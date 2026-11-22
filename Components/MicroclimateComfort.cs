using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class MicroclimateComfort : EnzymeComponent
    {
        protected override bool UsesRunGate => true;

        public MicroclimateComfort()
          : base("Microclimate Comfort", "FeelsLike",
              "Calculates the perceived 'Feels Like' temperature using Fast Apparent Temperature or UTCI (full Broede et al. 2012 polynomial). In UTCI mode the mean radiant temperature is not computed: it is approximated as air temperature + 15 C x Solar Factor, which dominates the uncertainty.",
              "Enzyme", "Site Analysis")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Analysis points.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Air Temp", "T", "Air temperature in Celsius.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Humidity", "RH", "Relative humidity in %.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Wind Speed", "v", "Wind speed in m/s at 10m height.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Solar Factor", "S", "Solar exposure from 0.0 (shade) to 1.0 (sun).", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Method", "M", "0 = Fast Apparent Temp, 1 = UTCI (mean radiant temperature approximated from Solar Factor)", GH_ParamAccess.item, 0);

            // Optional defaults if single values are provided
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager.AddBooleanParameter("Run", "Run", "Optional. False = skip the analysis and keep showing the last results (cached). Share one toggle with the Analysis Dashboard to switch everything on/off together.", GH_ParamAccess.item, true);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Analysis points.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Feels Like", "FL", "Perceived temperature in Celsius.", GH_ParamAccess.list);
            pManager.AddTextParameter("Category", "C", "Comfort category label.", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Col", "Gradient color mapping.", GH_ParamAccess.list);
            pManager.AddTextParameter("Dashboard Data", "Dashboard", "JSON Legend Data", GH_ParamAccess.item);
            pManager.AddTextParameter("Info", "Info", "Method, assumptions, simplifications and references.", GH_ParamAccess.item);
        }

        protected override void Solve(IGH_DataAccess DA, System.Diagnostics.Stopwatch hudSw)
        {
            if (DA.Iteration == 0 && Params.Output.Count > 5) DA.SetData(5, Enzyme.Utils.MethodNotes.Info("MicroclimateComfort"));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            List<Point3d> points = new List<Point3d>();
            if (!DA.GetDataList(0, points)) return;

            List<double> temps = new List<double>();
            DA.GetDataList(1, temps);
            if (temps.Count == 0) temps.Add(25.0);

            List<double> hums = new List<double>();
            DA.GetDataList(2, hums);
            if (hums.Count == 0) hums.Add(50.0);

            List<double> winds = new List<double>();
            DA.GetDataList(3, winds);
            if (winds.Count == 0) winds.Add(1.0);

            List<double> sol = new List<double>();
            DA.GetDataList(4, sol);
            if (sol.Count == 0) sol.Add(1.0);

            int method = 0;
            DA.GetData(5, ref method);

            if (method == 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "UTCI Mode: Mean Radiant Temperature (MRT) is automatically approximated based on the Solar Factor input (+15°C penalty in full sun).");
            }

            List<double> feelsLike = new List<double>(points.Count);
            List<string> categories = new List<string>(points.Count);
            List<Color> colors = new List<Color>(points.Count);
            int clampedCount = 0;

            for (int i = 0; i < points.Count; i++)
            {
                double t = temps[Math.Min(i, temps.Count - 1)];
                double rh = hums[Math.Min(i, hums.Count - 1)];
                double v = winds[Math.Min(i, winds.Count - 1)];
                double s = sol[Math.Min(i, sol.Count - 1)];

                double fl = 0.0;
                string cat = "";

                if (method == 0)
                {
                    // Fast Apparent Temperature
                    // e = vapor pressure
                    double e = (rh / 100.0) * 6.105 * Math.Exp(17.27 * t / (237.7 + t));
                    fl = t + 0.33 * e - 0.70 * v - 4.0;
                    
                    // Direct solar penalty approximation
                    fl += (s * 4.0); // +4C if in full sun

                    if (fl >= 40) cat = "Extreme Heat Stress";
                    else if (fl >= 32) cat = "Strong Heat Stress";
                    else if (fl >= 27) cat = "Moderate Heat Stress";
                    else if (fl >= 20) cat = "Comfortable";
                    else if (fl >= 10) cat = "Cool";
                    else if (fl >= 0) cat = "Cold";
                    else cat = "Extreme Cold";
                }
                else
                {
                    // Precise UTCI
                    double mrt = t + (s * 15.0); // Approximate MRT based on sun exposure
                    bool clamped;
                    fl = CalculateUTCI(t, rh, v, mrt, out clamped);
                    if (clamped) clampedCount++;

                    if (fl > 46) cat = "Extreme Heat Stress";
                    else if (fl >= 38) cat = "Very Strong Heat Stress";
                    else if (fl >= 32) cat = "Strong Heat Stress";
                    else if (fl >= 26) cat = "Moderate Heat Stress";
                    else if (fl >= 9) cat = "Comfortable (No Thermal Stress)";
                    else if (fl >= 0) cat = "Slight Cold Stress";
                    else if (fl >= -13) cat = "Moderate Cold Stress";
                    else if (fl >= -27) cat = "Strong Cold Stress";
                    else if (fl >= -40) cat = "Very Strong Cold Stress";
                    else cat = "Extreme Cold Stress";
                }

                feelsLike.Add(fl);
                categories.Add(cat);
                colors.Add(GetComfortColor(fl));
            }

            if (clampedCount > 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, clampedCount + " point(s) are outside the UTCI validity range (Ta -50..50 C, Tmrt-Ta -30..70 C, wind 0.5..17 m/s, vapour pressure 0..5 kPa); inputs were clamped to the range, so those results are only indicative.");

            var jColors = new Newtonsoft.Json.Linq.JArray();
            var stops = new double[] { -10, 5, 20, 30, 40 };
            var baseColors = new System.Drawing.Color[] { 
                System.Drawing.Color.FromArgb(0, 0, 139), 
                System.Drawing.Color.FromArgb(0, 191, 255), 
                System.Drawing.Color.FromArgb(34, 139, 34), 
                System.Drawing.Color.FromArgb(255, 215, 0), 
                System.Drawing.Color.FromArgb(139, 0, 0) 
            };
            foreach (var c in baseColors) jColors.Add(new Newtonsoft.Json.Linq.JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
            
            var legendObj = new Newtonsoft.Json.Linq.JObject
            {
                ["Type"] = "Blocks",
                ["Title"] = "Microclimate Comfort",
                ["Colors"] = jColors,
                ["Labels"] = new Newtonsoft.Json.Linq.JArray("-10°C", "40°C"),
                ["SubLabels"] = new Newtonsoft.Json.Linq.JArray(method == 0 ? "Fast Apparent Temp" : "Precise UTCI")
            };

            DA.SetDataList(0, points);
            DA.SetDataList(1, feelsLike);
            DA.SetDataList(2, categories);
            DA.SetDataList(3, colors);
            DA.SetData(4, legendObj.ToString());
        
            stopwatch.Stop();
            Hud.Done(this, stopwatch.Elapsed.TotalMilliseconds);
        }

        private Color GetComfortColor(double temp)
        {
            // Map roughly from -10 (Dark Blue) to 40 (Dark Red)
            if (temp <= -10) return Color.FromArgb(0, 0, 139);
            if (temp >= 40) return Color.FromArgb(139, 0, 0);

            // 5 stops: -10, 5, 20, 30, 40
            double[] stops = { -10, 5, 20, 30, 40 };
            Color[] colors = { 
                Color.FromArgb(0, 0, 139),    // Dark Blue
                Color.FromArgb(0, 191, 255),  // Light Blue (DeepSkyBlue)
                Color.FromArgb(34, 139, 34),  // Green (ForestGreen)
                Color.FromArgb(255, 215, 0),  // Yellow (Gold)
                Color.FromArgb(139, 0, 0)     // Dark Red
            };

            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (temp >= stops[i] && temp <= stops[i+1])
                {
                    double t = (temp - stops[i]) / (stops[i+1] - stops[i]);
                    int r = (int)(colors[i].R + t * (colors[i+1].R - colors[i].R));
                    int g = (int)(colors[i].G + t * (colors[i+1].G - colors[i].G));
                    int b = (int)(colors[i].B + t * (colors[i+1].B - colors[i].B));
                    return Color.FromArgb(r, g, b);
                }
            }
            return Color.Gray;
        }

        private double CalculateUTCI(double ta, double rh, double vel, double mrt, out bool clamped)
        {
            // Operational UTCI regression polynomial (Broede et al. 2012, Int J Biometeorol 56:481-494; official
            // program UTCI_a002.f90, utci.org), all 210 terms. Coefficients and the saturation vapour pressure
            // formula are ported from pythermalcomfort (CenterForTheBuiltEnvironment/pythermalcomfort,
            // pythermalcomfort/models/utci.py, MIT License, Copyright (c) 2019 Federico Tartarini) and checked
            // against that project's reference values (validation-data-comfort-models v1.1.0, ts_utci.json).
            // Inputs are clamped to the validity range of the polynomial: -50 <= Ta <= 50 C,
            // -30 <= Tmrt - Ta <= 70 C, 0.5 <= va <= 17 m/s, 0 <= Pa <= 5 kPa (outside it the polynomial
            // extrapolates to meaningless values).
            double rawTa = ta, rawVel = vel, rawRh = rh;
            ta = Math.Max(-50.0, Math.Min(50.0, ta));
            vel = Math.Max(0.5, Math.Min(17.0, vel));
            rh = Math.Max(0.0, Math.Min(100.0, rh));

            double d_tr = mrt - ta;
            double rawDtr = d_tr;
            d_tr = Math.Max(-30.0, Math.Min(70.0, d_tr));

            // Saturation vapour pressure over water (Hardy 1998, ITS-90), as in the UTCI program, in hPa
            double[] g = { -2836.5744, -6028.076559, 19.54263612, -0.02737830188, 0.000016261698, 7.0229056e-10, -1.8680009e-13 };
            double tk = ta + 273.15;
            double es = 2.7150305 * Math.Log(tk);
            for (int k = 0; k < g.Length; k++) es += g[k] * Math.Pow(tk, k - 2);
            double ehPa = Math.Exp(es) * 0.01;
            double pa_pr = (rh / 100.0) * ehPa / 10.0; // vapour pressure in kPa
            double rawPa = pa_pr;
            pa_pr = Math.Max(0.0, Math.Min(5.0, pa_pr));

            clamped = rawTa != ta || rawVel != vel || rawRh != rh || rawDtr != d_tr || rawPa != pa_pr;

            double utci =
                ta
                + 0.607562052
                + -0.0227712343 * ta
                + 8.06470249e-4 * ta * ta
                + -1.54271372e-4 * ta * ta * ta
                + -3.24651735e-6 * ta * ta * ta * ta
                + 7.32602852e-8 * ta * ta * ta * ta * ta
                + 1.35959073e-9 * ta * ta * ta * ta * ta * ta
                + -2.25836520 * vel
                + 0.0880326035 * ta * vel
                + 0.00216844454 * ta * ta * vel
                + -1.53347087e-5 * ta * ta * ta * vel
                + -5.72983704e-7 * ta * ta * ta * ta * vel
                + -2.55090145e-9 * ta * ta * ta * ta * ta * vel
                + -0.751269505 * vel * vel
                + -0.00408350271 * ta * vel * vel
                + -5.21670675e-5 * ta * ta * vel * vel
                + 1.94544667e-6 * ta * ta * ta * vel * vel
                + 1.14099531e-8 * ta * ta * ta * ta * vel * vel
                + 0.158137256 * vel * vel * vel
                + -6.57263143e-5 * ta * vel * vel * vel
                + 2.22697524e-7 * ta * ta * vel * vel * vel
                + -4.16117031e-8 * ta * ta * ta * vel * vel * vel
                + -0.0127762753 * vel * vel * vel * vel
                + 9.66891875e-6 * ta * vel * vel * vel * vel
                + 2.52785852e-9 * ta * ta * vel * vel * vel * vel
                + 4.56306672e-4 * vel * vel * vel * vel * vel
                + -1.74202546e-7 * ta * vel * vel * vel * vel * vel
                + -5.91491269e-6 * vel * vel * vel * vel * vel * vel
                + 0.398374029 * d_tr
                + 1.83945314e-4 * ta * d_tr
                + -1.73754510e-4 * ta * ta * d_tr
                + -7.60781159e-7 * ta * ta * ta * d_tr
                + 3.77830287e-8 * ta * ta * ta * ta * d_tr
                + 5.43079673e-10 * ta * ta * ta * ta * ta * d_tr
                + -0.0200518269 * vel * d_tr
                + 8.92859837e-4 * ta * vel * d_tr
                + 3.45433048e-6 * ta * ta * vel * d_tr
                + -3.77925774e-7 * ta * ta * ta * vel * d_tr
                + -1.69699377e-9 * ta * ta * ta * ta * vel * d_tr
                + 1.69992415e-4 * vel * vel * d_tr
                + -4.99204314e-5 * ta * vel * vel * d_tr
                + 2.47417178e-7 * ta * ta * vel * vel * d_tr
                + 1.07596466e-8 * ta * ta * ta * vel * vel * d_tr
                + 8.49242932e-5 * vel * vel * vel * d_tr
                + 1.35191328e-6 * ta * vel * vel * vel * d_tr
                + -6.21531254e-9 * ta * ta * vel * vel * vel * d_tr
                + -4.99410301e-6 * vel * vel * vel * vel * d_tr
                + -1.89489258e-8 * ta * vel * vel * vel * vel * d_tr
                + 8.15300114e-8 * vel * vel * vel * vel * vel * d_tr
                + 7.55043090e-4 * d_tr * d_tr
                + -5.65095215e-5 * ta * d_tr * d_tr
                + -4.52166564e-7 * ta * ta * d_tr * d_tr
                + 2.46688878e-8 * ta * ta * ta * d_tr * d_tr
                + 2.42674348e-10 * ta * ta * ta * ta * d_tr * d_tr
                + 1.54547250e-4 * vel * d_tr * d_tr
                + 5.24110970e-6 * ta * vel * d_tr * d_tr
                + -8.75874982e-8 * ta * ta * vel * d_tr * d_tr
                + -1.50743064e-9 * ta * ta * ta * vel * d_tr * d_tr
                + -1.56236307e-5 * vel * vel * d_tr * d_tr
                + -1.33895614e-7 * ta * vel * vel * d_tr * d_tr
                + 2.49709824e-9 * ta * ta * vel * vel * d_tr * d_tr
                + 6.51711721e-7 * vel * vel * vel * d_tr * d_tr
                + 1.94960053e-9 * ta * vel * vel * vel * d_tr * d_tr
                + -1.00361113e-8 * vel * vel * vel * vel * d_tr * d_tr
                + -1.21206673e-5 * d_tr * d_tr * d_tr
                + -2.18203660e-7 * ta * d_tr * d_tr * d_tr
                + 7.51269482e-9 * ta * ta * d_tr * d_tr * d_tr
                + 9.79063848e-11
                * ta
                * ta
                * ta
                * d_tr
                * d_tr
                * d_tr
                + 1.25006734e-6 * vel * d_tr * d_tr * d_tr
                + -1.81584736e-9 * ta * vel * d_tr * d_tr * d_tr
                + -3.52197671e-10
                * ta
                * ta
                * vel
                * d_tr
                * d_tr
                * d_tr
                + -3.36514630e-8 * vel * vel * d_tr * d_tr * d_tr
                + 1.35908359e-10
                * ta
                * vel
                * vel
                * d_tr
                * d_tr
                * d_tr
                + 4.17032620e-10
                * vel
                * vel
                * vel
                * d_tr
                * d_tr
                * d_tr
                + -1.30369025e-9
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + 4.13908461e-10
                * ta
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + 9.22652254e-12
                * ta
                * ta
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + -5.08220384e-9
                * vel
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + -2.24730961e-11
                * ta
                * vel
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + 1.17139133e-10
                * vel
                * vel
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + 6.62154879e-10
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + 4.03863260e-13
                * ta
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + 1.95087203e-12
                * vel
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + -4.73602469e-12
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                + 5.12733497 * pa_pr
                + -0.312788561 * ta * pa_pr
                + -0.0196701861 * ta * ta * pa_pr
                + 9.99690870e-4 * ta * ta * ta * pa_pr
                + 9.51738512e-6 * ta * ta * ta * ta * pa_pr
                + -4.66426341e-7 * ta * ta * ta * ta * ta * pa_pr
                + 0.548050612 * vel * pa_pr
                + -0.00330552823 * ta * vel * pa_pr
                + -0.00164119440 * ta * ta * vel * pa_pr
                + -5.16670694e-6 * ta * ta * ta * vel * pa_pr
                + 9.52692432e-7 * ta * ta * ta * ta * vel * pa_pr
                + -0.0429223622 * vel * vel * pa_pr
                + 0.00500845667 * ta * vel * vel * pa_pr
                + 1.00601257e-6 * ta * ta * vel * vel * pa_pr
                + -1.81748644e-6 * ta * ta * ta * vel * vel * pa_pr
                + -1.25813502e-3 * vel * vel * vel * pa_pr
                + -1.79330391e-4 * ta * vel * vel * vel * pa_pr
                + 2.34994441e-6 * ta * ta * vel * vel * vel * pa_pr
                + 1.29735808e-4 * vel * vel * vel * vel * pa_pr
                + 1.29064870e-6 * ta * vel * vel * vel * vel * pa_pr
                + -2.28558686e-6 * vel * vel * vel * vel * vel * pa_pr
                + -0.0369476348 * d_tr * pa_pr
                + 0.00162325322 * ta * d_tr * pa_pr
                + -3.14279680e-5 * ta * ta * d_tr * pa_pr
                + 2.59835559e-6 * ta * ta * ta * d_tr * pa_pr
                + -4.77136523e-8 * ta * ta * ta * ta * d_tr * pa_pr
                + 8.64203390e-3 * vel * d_tr * pa_pr
                + -6.87405181e-4 * ta * vel * d_tr * pa_pr
                + -9.13863872e-6 * ta * ta * vel * d_tr * pa_pr
                + 5.15916806e-7 * ta * ta * ta * vel * d_tr * pa_pr
                + -3.59217476e-5 * vel * vel * d_tr * pa_pr
                + 3.28696511e-5 * ta * vel * vel * d_tr * pa_pr
                + -7.10542454e-7 * ta * ta * vel * vel * d_tr * pa_pr
                + -1.24382300e-5 * vel * vel * vel * d_tr * pa_pr
                + -7.38584400e-9 * ta * vel * vel * vel * d_tr * pa_pr
                + 2.20609296e-7 * vel * vel * vel * vel * d_tr * pa_pr
                + -7.32469180e-4 * d_tr * d_tr * pa_pr
                + -1.87381964e-5 * ta * d_tr * d_tr * pa_pr
                + 4.80925239e-6 * ta * ta * d_tr * d_tr * pa_pr
                + -8.75492040e-8 * ta * ta * ta * d_tr * d_tr * pa_pr
                + 2.77862930e-5 * vel * d_tr * d_tr * pa_pr
                + -5.06004592e-6 * ta * vel * d_tr * d_tr * pa_pr
                + 1.14325367e-7 * ta * ta * vel * d_tr * d_tr * pa_pr
                + 2.53016723e-6 * vel * vel * d_tr * d_tr * pa_pr
                + -1.72857035e-8 * ta * vel * vel * d_tr * d_tr * pa_pr
                + -3.95079398e-8 * vel * vel * vel * d_tr * d_tr * pa_pr
                + -3.59413173e-7 * d_tr * d_tr * d_tr * pa_pr
                + 7.04388046e-7 * ta * d_tr * d_tr * d_tr * pa_pr
                + -1.89309167e-8
                * ta
                * ta
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                + -4.79768731e-7 * vel * d_tr * d_tr * d_tr * pa_pr
                + 7.96079978e-9
                * ta
                * vel
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                + 1.62897058e-9
                * vel
                * vel
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                + 3.94367674e-8
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                + -1.18566247e-9
                * ta
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                + 3.34678041e-10
                * vel
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                + -1.15606447e-10
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                + -2.80626406 * pa_pr * pa_pr
                + 0.548712484 * ta * pa_pr * pa_pr
                + -0.00399428410 * ta * ta * pa_pr * pa_pr
                + -9.54009191e-4 * ta * ta * ta * pa_pr * pa_pr
                + 1.93090978e-5 * ta * ta * ta * ta * pa_pr * pa_pr
                + -0.308806365 * vel * pa_pr * pa_pr
                + 0.0116952364 * ta * vel * pa_pr * pa_pr
                + 4.95271903e-4 * ta * ta * vel * pa_pr * pa_pr
                + -1.90710882e-5 * ta * ta * ta * vel * pa_pr * pa_pr
                + 0.00210787756 * vel * vel * pa_pr * pa_pr
                + -6.98445738e-4 * ta * vel * vel * pa_pr * pa_pr
                + 2.30109073e-5 * ta * ta * vel * vel * pa_pr * pa_pr
                + 4.17856590e-4 * vel * vel * vel * pa_pr * pa_pr
                + -1.27043871e-5 * ta * vel * vel * vel * pa_pr * pa_pr
                + -3.04620472e-6 * vel * vel * vel * vel * pa_pr * pa_pr
                + 0.0514507424 * d_tr * pa_pr * pa_pr
                + -0.00432510997 * ta * d_tr * pa_pr * pa_pr
                + 8.99281156e-5 * ta * ta * d_tr * pa_pr * pa_pr
                + -7.14663943e-7 * ta * ta * ta * d_tr * pa_pr * pa_pr
                + -2.66016305e-4 * vel * d_tr * pa_pr * pa_pr
                + 2.63789586e-4 * ta * vel * d_tr * pa_pr * pa_pr
                + -7.01199003e-6 * ta * ta * vel * d_tr * pa_pr * pa_pr
                + -1.06823306e-4 * vel * vel * d_tr * pa_pr * pa_pr
                + 3.61341136e-6 * ta * vel * vel * d_tr * pa_pr * pa_pr
                + 2.29748967e-7 * vel * vel * vel * d_tr * pa_pr * pa_pr
                + 3.04788893e-4 * d_tr * d_tr * pa_pr * pa_pr
                + -6.42070836e-5 * ta * d_tr * d_tr * pa_pr * pa_pr
                + 1.16257971e-6 * ta * ta * d_tr * d_tr * pa_pr * pa_pr
                + 7.68023384e-6 * vel * d_tr * d_tr * pa_pr * pa_pr
                + -5.47446896e-7 * ta * vel * d_tr * d_tr * pa_pr * pa_pr
                + -3.59937910e-8 * vel * vel * d_tr * d_tr * pa_pr * pa_pr
                + -4.36497725e-6 * d_tr * d_tr * d_tr * pa_pr * pa_pr
                + 1.68737969e-7
                * ta
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                * pa_pr
                + 2.67489271e-8
                * vel
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                * pa_pr
                + 3.23926897e-9
                * d_tr
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                * pa_pr
                + -0.0353874123 * pa_pr * pa_pr * pa_pr
                + -0.221201190 * ta * pa_pr * pa_pr * pa_pr
                + 0.0155126038 * ta * ta * pa_pr * pa_pr * pa_pr
                + -2.63917279e-4 * ta * ta * ta * pa_pr * pa_pr * pa_pr
                + 0.0453433455 * vel * pa_pr * pa_pr * pa_pr
                + -0.00432943862 * ta * vel * pa_pr * pa_pr * pa_pr
                + 1.45389826e-4 * ta * ta * vel * pa_pr * pa_pr * pa_pr
                + 2.17508610e-4 * vel * vel * pa_pr * pa_pr * pa_pr
                + -6.66724702e-5 * ta * vel * vel * pa_pr * pa_pr * pa_pr
                + 3.33217140e-5 * vel * vel * vel * pa_pr * pa_pr * pa_pr
                + -0.00226921615 * d_tr * pa_pr * pa_pr * pa_pr
                + 3.80261982e-4 * ta * d_tr * pa_pr * pa_pr * pa_pr
                + -5.45314314e-9 * ta * ta * d_tr * pa_pr * pa_pr * pa_pr
                + -7.96355448e-4 * vel * d_tr * pa_pr * pa_pr * pa_pr
                + 2.53458034e-5 * ta * vel * d_tr * pa_pr * pa_pr * pa_pr
                + -6.31223658e-6 * vel * vel * d_tr * pa_pr * pa_pr * pa_pr
                + 3.02122035e-4 * d_tr * d_tr * pa_pr * pa_pr * pa_pr
                + -4.77403547e-6 * ta * d_tr * d_tr * pa_pr * pa_pr * pa_pr
                + 1.73825715e-6 * vel * d_tr * d_tr * pa_pr * pa_pr * pa_pr
                + -4.09087898e-7
                * d_tr
                * d_tr
                * d_tr
                * pa_pr
                * pa_pr
                * pa_pr
                + 0.614155345 * pa_pr * pa_pr * pa_pr * pa_pr
                + -0.0616755931 * ta * pa_pr * pa_pr * pa_pr * pa_pr
                + 0.00133374846 * ta * ta * pa_pr * pa_pr * pa_pr * pa_pr
                + 0.00355375387 * vel * pa_pr * pa_pr * pa_pr * pa_pr
                + -5.13027851e-4 * ta * vel * pa_pr * pa_pr * pa_pr * pa_pr
                + 1.02449757e-4 * vel * vel * pa_pr * pa_pr * pa_pr * pa_pr
                + -0.00148526421 * d_tr * pa_pr * pa_pr * pa_pr * pa_pr
                + -4.11469183e-5 * ta * d_tr * pa_pr * pa_pr * pa_pr * pa_pr
                + -6.80434415e-6 * vel * d_tr * pa_pr * pa_pr * pa_pr * pa_pr
                + -9.77675906e-6 * d_tr * d_tr * pa_pr * pa_pr * pa_pr * pa_pr
                + 0.0882773108 * pa_pr * pa_pr * pa_pr * pa_pr * pa_pr
                + -0.00301859306 * ta * pa_pr * pa_pr * pa_pr * pa_pr * pa_pr
                + 0.00104452989 * vel * pa_pr * pa_pr * pa_pr * pa_pr * pa_pr
                + 2.47090539e-4 * d_tr * pa_pr * pa_pr * pa_pr * pa_pr * pa_pr
                + 0.00148348065 * pa_pr * pa_pr * pa_pr * pa_pr * pa_pr * pa_pr;

            return utci;
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                var vl = new Grasshopper.Kernel.Special.GH_ValueList();
                vl.CreateAttributes();
                vl.Attributes.Pivot = new PointF(this.Attributes.Pivot.X - 250, this.Attributes.Pivot.Y + 80);
                vl.ListItems.Clear();
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Fast Apparent Temp", "0"));
                vl.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem("Precise UTCI", "1"));
                vl.SelectItem(0);

                document.AddObject(vl, false);
                this.Params.Input[5].AddSource(vl);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("MicroclimateComfort.png");
        public override Guid ComponentGuid => new Guid("4A7E0065-D94F-4987-96D6-C925345DC377");
    }
}
