using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class MicroclimateComfort : GH_Component
    {
        public MicroclimateComfort()
          : base("Microclimate Comfort", "FeelsLike",
              "Calculates the perceived 'Feels Like' temperature using Fast Apparent Temperature or Precise UTCI.",
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
            pManager.AddIntegerParameter("Method", "M", "0 = Fast Apparent Temp, 1 = Precise UTCI", GH_ParamAccess.item, 0);

            // Optional defaults if single values are provided
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Analysis points.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Feels Like", "FL", "Perceived temperature in Celsius.", GH_ParamAccess.list);
            pManager.AddTextParameter("Category", "C", "Comfort category label.", GH_ParamAccess.list);
            pManager.AddColourParameter("Colors", "Col", "Gradient color mapping.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
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
                    fl = CalculateUTCI(t, rh, v, mrt);

                    if (fl >= 38) cat = "Very Strong Heat Stress";
                    else if (fl >= 32) cat = "Strong Heat Stress";
                    else if (fl >= 26) cat = "Moderate Heat Stress";
                    else if (fl >= 9) cat = "Comfortable (No Thermal Stress)";
                    else if (fl >= 0) cat = "Slight Cold Stress";
                    else if (fl >= -13) cat = "Moderate Cold Stress";
                    else if (fl >= -27) cat = "Strong Cold Stress";
                    else cat = "Very Strong Cold Stress";
                }

                feelsLike.Add(fl);
                categories.Add(cat);
                colors.Add(GetComfortColor(fl));
            }

            DA.SetDataList(0, points);
            DA.SetDataList(1, feelsLike);
            DA.SetDataList(2, categories);
            DA.SetDataList(3, colors);
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

        private double CalculateUTCI(double ta, double rh, double vel, double mrt)
        {
            // Bounds clamping as per standard UTCI limits to avoid polynomial explosions
            ta = Math.Max(-50.0, Math.Min(50.0, ta));
            vel = Math.Max(0.5, Math.Min(30.0, vel)); // Wind at 10m usually capped, minimum 0.5m/s
            
            double d_tr = mrt - ta;
            
            // Vapor pressure (pa_pr) in kPa
            double ehPa = 6.112 * Math.Exp((17.62 * ta) / (243.12 + ta));
            double pa_pr = (rh / 100.0) * ehPa;
            pa_pr = pa_pr / 10.0; // Convert hPa to kPa
            
            // Limit vapor pressure (UTCI standard constraint)
            pa_pr = Math.Max(0.0, Math.Min(5.0, pa_pr));

            double ta2 = ta * ta;
            double ta3 = ta2 * ta;
            double ta4 = ta3 * ta;
            double ta5 = ta4 * ta;
            double ta6 = ta5 * ta;
            double vel2 = vel * vel;
            double vel3 = vel2 * vel;
            double vel4 = vel3 * vel;
            double vel5 = vel4 * vel;
            double vel6 = vel5 * vel;
            double d_tr2 = d_tr * d_tr;
            double d_tr3 = d_tr2 * d_tr;
            double d_tr4 = d_tr3 * d_tr;
            double d_tr5 = d_tr4 * d_tr;
            double d_tr6 = d_tr5 * d_tr;
            double pa_pr2 = pa_pr * pa_pr;
            double pa_pr3 = pa_pr2 * pa_pr;
            double pa_pr4 = pa_pr3 * pa_pr;
            double pa_pr5 = pa_pr4 * pa_pr;
            double pa_pr6 = pa_pr5 * pa_pr;
            double utci = ta + 
                0.607562052 +
                -0.0227712343 * ta +
                8.06470249e-4 * ta2 +
                -1.54271372e-4 * ta3 +
                -3.24651735e-6 * ta4 +
                7.32602852e-8 * ta5 +
                1.35959073e-9 * ta6 +
                -2.25836520 * vel +
                0.0880326035 * ta * vel +
                0.00216844454 * ta2 * vel +
                -1.53347087e-5 * ta3 * vel +
                -5.72983704e-7 * ta4 * vel +
                -2.55090145e-9 * ta5 * vel +
                -0.751269505 * vel2 +
                -0.00408350271 * ta * vel2 +
                -5.21670675e-5 * ta2 * vel2 +
                1.94544667e-6 * ta3 * vel2 +
                1.14099531e-8 * ta4 * vel2 +
                0.158137256 * vel3 +
                -6.57263143e-5 * ta * vel3 +
                2.22697524e-7 * ta2 * vel3 +
                -4.16117031e-8 * ta3 * vel3 +
                -0.0127762753 * vel4 +
                9.66891875e-6 * ta * vel4 +
                2.52785852e-9 * ta2 * vel4 +
                4.56306672e-4 * vel5 +
                -1.74202546e-7 * ta * vel5 +
                -5.91491269e-6 * vel6 +
                0.398374029 * d_tr +
                1.83945314e-4 * ta * d_tr +
                -1.73754510e-4 * ta2 * d_tr +
                -7.60781159e-7 * ta3 * d_tr +
                3.77830287e-8 * ta4 * d_tr +
                5.43079673e-10 * ta5 * d_tr +
                -0.0200518269 * vel * d_tr +
                8.92859837e-4 * ta * vel * d_tr +
                3.45433048e-6 * ta2 * vel * d_tr +
                -3.77925774e-7 * ta3 * vel * d_tr +
                -1.69699377e-9 * ta4 * vel * d_tr +
                1.69992415e-4 * vel2 * d_tr +
                -4.99204314e-5 * ta * vel2 * d_tr +
                2.47417178e-7 * ta2 * vel2 * d_tr +
                1.07596466e-8 * ta3 * vel2 * d_tr +
                8.49242932e-5 * vel3 * d_tr +
                1.35191328e-6 * ta * vel3 * d_tr +
                -6.21531254e-9 * ta2 * vel3 * d_tr +
                -4.99410301e-6 * vel4 * d_tr +
                -1.89489258e-8 * ta * vel4 * d_tr +
                8.15300114e-8 * vel5 * d_tr +
                7.55043090e-4 * d_tr2 +
                -5.65095215e-5 * ta * d_tr2 +
                -4.52166564e-7 * ta2 * d_tr2 +
                2.46688878e-8 * ta3 * d_tr2 +
                2.42674348e-10 * ta4 * d_tr2 +
                1.54547250e-4 * vel * d_tr2 +
                5.24110970e-6 * ta * vel * d_tr2 +
                -8.75874982e-8 * ta2 * vel * d_tr2 +
                -1.50743064e-9 * ta3 * vel * d_tr2 +
                -1.56236307e-5 * vel2 * d_tr2 +
                -1.33895614e-7 * ta * vel2 * d_tr2 +
                2.49709824e-9 * ta2 * vel2 * d_tr2 +
                6.51711721e-7 * vel3 * d_tr2 +
                1.94960053e-9 * ta * vel3 * d_tr2 +
                -1.00361113e-8 * vel4 * d_tr2 +
                -1.21206673e-5 * d_tr3 +
                -2.18203660e-7 * ta * d_tr3 +
                7.51269482e-9 * ta2 * d_tr3 +
                9.79063848e-11 * ta3 * d_tr3 +
                1.25006734e-6 * vel * d_tr3 +
                -1.81584736e-9 * ta * vel * d_tr3 +
                -3.52197671e-10 * ta2 * vel * d_tr3 +
                -3.36514630e-8 * vel2 * d_tr3 +
                1.35908359e-10 * ta * vel2 * d_tr3 +
                4.17032620e-10 * vel3 * d_tr3 +
                -1.30369025e-9 * d_tr4 +
                4.13908461e-10 * ta * d_tr4 +
                9.22652254e-12 * ta2 * d_tr4 +
                -5.08220384e-9 * vel * d_tr4 +
                -2.24730961e-11 * ta * vel * d_tr4 +
                1.17139133e-10 * vel2 * d_tr4 +
                6.62154879e-10 * d_tr5 +
                4.03863260e-13 * ta * d_tr5 +
                1.95087203e-12 * vel * d_tr5 +
                -4.73602469e-12 * d_tr6 +
                5.12733497 * pa_pr +
                -0.312788561 * ta * pa_pr +
                -0.0196701861 * ta2 * pa_pr +
                9.99690870e-4 * ta3 * pa_pr +
                9.51738512e-6 * ta4 * pa_pr +
                -4.66426341e-7 * ta5 * pa_pr +
                0.548050612 * vel * pa_pr +
                -0.00330552823 * ta * vel * pa_pr +
                -0.00164119440 * ta2 * vel * pa_pr +
                -5.16670694e-6 * ta3 * vel * pa_pr +
                9.52692432e-7 * ta4 * vel * pa_pr +
                -0.0429223622 * vel2 * pa_pr +
                0.00500845667 * ta * vel2 * pa_pr +
                1.00601257e-6 * ta2 * vel2 * pa_pr ;


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
