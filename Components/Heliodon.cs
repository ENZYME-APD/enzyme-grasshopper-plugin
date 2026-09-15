using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class Heliodon : GH_Component
    {
        public Heliodon()
          : base("Enzyme Heliodon", "Heliodon",
              "Generates a 3D solar dome and calculates annual sun vectors for environmental analysis.",
              "Enzyme", "Site Analysis")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Latitude", "Lat", "Location latitude (-90 to 90)", GH_ParamAccess.item, 51.5);
            pManager.AddNumberParameter("Longitude", "Lon", "Location longitude (-180 to 180)", GH_ParamAccess.item, -0.1);
            pManager.AddNumberParameter("Time Zone", "TZ", "Time zone offset from GMT", GH_ParamAccess.item, 0.0);
                        pManager.AddNumberParameter("Radius", "Radius", "Visual radius of the heliodon dome", GH_ParamAccess.item, 100.0);
            pManager.AddPointParameter("Center", "Center", "Center point of the heliodon", GH_ParamAccess.item, Point3d.Origin);
            pManager.AddIntervalParameter("Months", "Months", "Domain of months to evaluate (e.g., 1 to 12).", GH_ParamAccess.item, new Interval(1, 12));
            pManager.AddIntervalParameter("Hours", "Hours", "Domain of hours to evaluate (e.g., 8 to 17).", GH_ParamAccess.item, new Interval(0, 24));
            
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddVectorParameter("Sun Vectors", "Vectors", "All solar vectors above the horizon (pointing TO the sun).", GH_ParamAccess.list);
            pManager.AddCurveParameter("Daily Arcs", "Arcs", "Visual sun path curves for the 21st of each month.", GH_ParamAccess.list);
            pManager.AddPointParameter("Sun Points", "Points", "Visual hourly sun positions.", GH_ParamAccess.list);
        }

                protected override void SolveInstance(IGH_DataAccess DA)
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            double lat = 51.5;
            double lon = -0.1;
            double tz = 0.0;
            double radius = 100.0;
            Rhino.Geometry.Point3d center = Rhino.Geometry.Point3d.Origin;
            Rhino.Geometry.Interval mth = new Rhino.Geometry.Interval(1, 12);
            Rhino.Geometry.Interval hrs = new Rhino.Geometry.Interval(0, 24);

            DA.GetData(0, ref lat);
            DA.GetData(1, ref lon);
            DA.GetData(2, ref tz);
            DA.GetData(3, ref radius);
            DA.GetData(4, ref center);
            DA.GetData(5, ref mth);
            DA.GetData(6, ref hrs);

            System.Collections.Generic.List<Rhino.Geometry.Vector3d> vectors = new System.Collections.Generic.List<Rhino.Geometry.Vector3d>();
            System.Collections.Generic.List<Rhino.Geometry.Curve> arcs = new System.Collections.Generic.List<Rhino.Geometry.Curve>();
            System.Collections.Generic.List<Rhino.Geometry.Point3d> points = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

            int startMonth = (int)System.Math.Max(1, System.Math.Min(12, mth.Min));
            int endMonth = (int)System.Math.Max(1, System.Math.Min(12, mth.Max));
            
            double startHour = System.Math.Max(0, System.Math.Min(24, hrs.Min));
            double endHour = System.Math.Max(0, System.Math.Min(24, hrs.Max));

            int[] allDays = new int[] { 21, 52, 80, 111, 141, 172, 202, 233, 264, 294, 325, 355 };
            
            System.Collections.Generic.List<int> validDays = new System.Collections.Generic.List<int>();
            for (int i = startMonth - 1; i <= endMonth - 1; i++)
            {
                if (i >= 0 && i < 12) validDays.Add(allDays[i]);
            }

            foreach (int day in validDays)
            {
                System.Collections.Generic.List<Rhino.Geometry.Point3d> dailyPts = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

                for (double hour = startHour; hour <= endHour; hour += 0.5)
                {
                    Rhino.Geometry.Vector3d sunVec = GetSunVector(lat, lon, day, hour, tz);
                    
                    if (sunVec.Z > 0.01) 
                    {
                        if (System.Math.Abs(hour % 1.0) < 0.01)
                        {
                            vectors.Add(sunVec);
                            Rhino.Geometry.Point3d pt = center + (sunVec * radius);
                            points.Add(pt);
                        }
                        
                        Rhino.Geometry.Point3d curvePt = center + (sunVec * radius);
                        dailyPts.Add(curvePt);
                    }
                }

                if (dailyPts.Count > 1)
                {
                    Rhino.Geometry.Curve arc = Rhino.Geometry.Curve.CreateInterpolatedCurve(dailyPts, 3);
                    if (arc != null) arcs.Add(arc);
                }
            }

            sw.Stop();

            DA.SetDataList(0, vectors);
            DA.SetDataList(1, arcs);
            DA.SetDataList(2, points);
            
            Message = $"Heliodon\n{sw.ElapsedMilliseconds} ms\n---\nLat: {lat:F1}\nLon: {lon:F1}\nVectors: {vectors.Count}";
        }

        private Vector3d GetSunVector(double lat, double lon, int dayOfYear, double hour, double tz)
        {
            // Standard Solar Position Algorithm (NOAA approximation)
            double latRad = lat * Math.PI / 180.0;
            
            // Fractional year (gamma)
            double gamma = 2.0 * Math.PI / 365.0 * (dayOfYear - 1.0 + (hour - 12.0) / 24.0);
            
            // Equation of time (in minutes)
            double eqTime = 229.18 * (0.000075 + 0.001868 * Math.Cos(gamma) - 0.032077 * Math.Sin(gamma) 
                            - 0.014615 * Math.Cos(2.0 * gamma) - 0.040849 * Math.Sin(2.0 * gamma));
            
            // Declination angle (in radians)
            double decl = 0.006918 - 0.399912 * Math.Cos(gamma) + 0.070257 * Math.Sin(gamma) 
                          - 0.006758 * Math.Cos(2.0 * gamma) + 0.000907 * Math.Sin(2.0 * gamma) 
                          - 0.002697 * Math.Cos(3.0 * gamma) + 0.00148 * Math.Sin(3.0 * gamma);
            
            // True solar time (in minutes)
            double timeOffset = eqTime + 4.0 * lon - 60.0 * tz;
            double tst = hour * 60.0 + timeOffset;
            
            // Solar hour angle (in degrees, then radians)
            double ha = (tst / 4.0) - 180.0;
            double haRad = ha * Math.PI / 180.0;
            
            // Solar zenith angle
            double cosZenith = Math.Sin(latRad) * Math.Sin(decl) + Math.Cos(latRad) * Math.Cos(decl) * Math.Cos(haRad);
            cosZenith = Math.Max(-1.0, Math.Min(1.0, cosZenith)); // Clamp
            double zenith = Math.Acos(cosZenith);
            double altitude = (Math.PI / 2.0) - zenith;
            
            // Solar azimuth
            double cosAzimuth = -(Math.Sin(latRad) * Math.Cos(zenith) - Math.Sin(decl)) / (Math.Cos(latRad) * Math.Sin(zenith));
            cosAzimuth = Math.Max(-1.0, Math.Min(1.0, cosAzimuth)); // Clamp
            double azimuth = Math.Acos(cosAzimuth);
            
            // Adjust azimuth based on hour angle
            if (ha > 0)
            {
                azimuth = 2.0 * Math.PI - azimuth;
            }
            
            // Convert Azimuth from South (0) to North (0)
            // Actually, in the NOAA formula, Azimuth is measured from North=0, East=90, South=180, West=270.
            // Wait, the standard output of the formula above (with the negative sign in cosAzimuth) gives North=0.
            // Let's verify by checking typical X/Y mapping. 
            // In Rhino: Y+ is North. X+ is East.
            // If Azimuth = 0 (North), X = sin(0) = 0, Y = cos(0) = 1. Points North.
            // If Azimuth = 90 (East), X = sin(90) = 1, Y = cos(90) = 0. Points East.
            // If Azimuth = 180 (South), X = 0, Y = -1. Points South.
            
            double x = Math.Sin(azimuth) * Math.Cos(altitude);
            double y = Math.Cos(azimuth) * Math.Cos(altitude);
            double z = Math.Sin(altitude);
            
            // Inverse the vector so it points FROM the ground TO the sun
            // (Wait, the math above gives the coordinate of the sun. So it already points to the sun).
            
            return new Vector3d(x, y, z);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[0].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 0, -90.0, 90.0, 51.5, 200, -60);
            if (this.Params.Input[1].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 1, -180.0, 180.0, -0.1, 200, -30);
            if (this.Params.Input[2].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 2, -12, 14, 0, 200, 0);
            if (this.Params.Input[3].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 3, 10.0, 1000.0, 100.0, 200, 30);
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("Heliodon.png"); 

        
        public override GH_Exposure Exposure => GH_Exposure.secondary;
public override Guid ComponentGuid => new Guid("B5D8F2B1-4A2E-4D7F-8C9B-9A3E4F5D6C7B");
    }
}
