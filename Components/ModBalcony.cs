using System;
using System.Linq;
using System.Collections.Generic;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Grasshopper.Components
{
    public class ModBalconyComponent : GH_Component
    {
        public ModBalconyComponent()
          : base("Facade Module: Balcony", "Mod_Balcony",
              "Continuous balconies with 2D wall patterns and intelligent corners.",
              "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Bounds", "Bounds", "Boundary curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "Heights", "Floor heights", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("BayWidth", "BayWidth", "Width of each bay", GH_ParamAccess.item, 3.0);
            pManager.AddNumberParameter("Depth", "Depth", "Depth of the balcony", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("RailHeight", "RailHeight", "Height of the railing", GH_ParamAccess.item, 1.1);
            pManager.AddNumberParameter("SillHeight", "SillHeight", "Height of the sill", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("HeaderDrop", "HeaderDrop", "Header drop distance", GH_ParamAccess.item, 0.0);
            pManager.AddTextParameter("HorizPattern", "HorizPattern", "Horizontal pattern array", GH_ParamAccess.list);
            pManager[7].Optional = true;
            pManager.AddTextParameter("VertPattern", "VertPattern", "Vertical pattern", GH_ParamAccess.item, "0");
            pManager.AddTextParameter("BalconyPattern", "BalconyPattern", "Balcony pattern", GH_ParamAccess.item, "1");
            pManager.AddBooleanParameter("FlipDir", "FlipDir", "Flip direction", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("FirstBalcony", "FirstBalcony", "Toggle first balcony", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("CornerWindow", "CornerWindow", "Toggle corner window", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("CornerOffset", "CornerOffset", "Corner offset distance", GH_ParamAccess.item, 0.0);
            pManager.AddBooleanParameter("SegLength", "SegLength", "Segment length bypass", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Slabs", "Slabs", "Slab Breps", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Railings", "Railings", "Railing Breps", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Glass", "Glass", "Vision glass Breps", GH_ParamAccess.tree);
            pManager.AddBrepParameter("SolidPanels", "SolidPanels", "Solid wall Breps", GH_ParamAccess.tree);
            pManager.AddBrepParameter("HeaderPanels", "HeaderPanels", "Header drop Breps", GH_ParamAccess.tree);
            pManager.AddBrepParameter("CornerPanels", "CornerPanels", "Corner window Breps", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> boundsTree)) return;
            DA.GetDataTree(1, out GH_Structure<GH_Number> heightsTree);
            
            double bayWidth = 3.0; DA.GetData(2, ref bayWidth);
            double depth = 1.5; DA.GetData(3, ref depth);
            double railHeight = 1.1; DA.GetData(4, ref railHeight);
            double sillHeight = 0.0; DA.GetData(5, ref sillHeight);
            double headerDrop = 0.0; DA.GetData(6, ref headerDrop);
            
            List<string> horizPattern = new List<string>(); DA.GetDataList(7, horizPattern);
            string vertPattern = "0"; DA.GetData(8, ref vertPattern);
            string balconyPattern = "1"; DA.GetData(9, ref balconyPattern);
            bool flipDir = false; DA.GetData(10, ref flipDir);
            bool firstBalcony = false; DA.GetData(11, ref firstBalcony);
            bool cornerWindow = false; DA.GetData(12, ref cornerWindow);
            double cornerOffset = 0.0; DA.GetData(13, ref cornerOffset);
            bool segLength = false; DA.GetData(14, ref segLength);

            // 1. Safe Defaults
            if (bayWidth <= 0.0) bayWidth = 3.0;
            if (depth <= 0.0) depth = 1.5;
            if (railHeight <= 0.0) railHeight = 1.1;

            // 2. Parse Patterns
            if (horizPattern == null || horizPattern.Count == 0) horizPattern = new List<string> { "1" };
            List<string> cleanHPats = new List<string>();
            foreach (var pat in horizPattern)
            {
                string clean = new string((pat ?? "1").Where(c => c == '0' || c == '1').ToArray());
                cleanHPats.Add(string.IsNullOrEmpty(clean) ? "1" : clean);
            }

            string rawVPat = (vertPattern ?? "0").Trim();
            List<int> vIndices = rawVPat.Where(char.IsDigit).Select(c => (int)char.GetNumericValue(c)).ToList();
            if (vIndices.Count == 0) vIndices.Add(0);

            string rawBPat = (balconyPattern ?? "1").Trim();
            List<char> balcPattern = rawBPat.Where(c => c == '0' || c == '1').ToList();
            if (balcPattern.Count == 0) balcPattern.Add('1');

            // 3. Initialize Output Trees
            var outSlabs = new GH_Structure<GH_Brep>();
            var outRails = new GH_Structure<GH_Brep>();
            var outGlass = new GH_Structure<GH_Brep>();
            var outSolid = new GH_Structure<GH_Brep>();
            var outHeaders = new GH_Structure<GH_Brep>();
            var outCorners = new GH_Structure<GH_Brep>();

            double slabArea = 0, glassArea = 0, solidArea = 0, headerArea = 0, cornerArea = 0;

            // 4. Main Processing Loop
            for (int p = 0; p < boundsTree.PathCount; p++)
            {
                GH_Path path = boundsTree.Paths[p];
                var ghCrvs = boundsTree.get_Branch(path);
                List<Curve> crvs = new List<Curve>();
                foreach (var ghCrv in ghCrvs)
                {
                    crvs.Add(ghCrv != null ? ((GH_Curve)ghCrv).Value : null);
                }

                List<double> hts = new List<double>();
                if (heightsTree != null && heightsTree.PathExists(path))
                {
                    foreach (var ghHt in heightsTree.get_Branch(path))
                    {
                        if (ghHt != null) hts.Add(((GH_Number)ghHt).Value);
                    }
                }

                for (int i = 0; i < crvs.Count; i++)
                {
                    Curve crv = crvs[i];
                    if (crv == null) continue;

                    double h = (hts.Count > i) ? hts[i] : 4.0;
                    double dist = flipDir ? -depth : depth;

                    // --- BALCONY LOGIC ---
                    bool hasBalcony = balcPattern[i % balcPattern.Count] == '1';
                    
                    if (!firstBalcony && i == 0) hasBalcony = false;

                    if (hasBalcony)
                    {
                        Curve[] offsets = crv.Offset(Plane.WorldXY, dist, 0.1, CurveOffsetCornerStyle.Sharp);
                        if (offsets != null && offsets.Length > 0)
                        {
                            Curve offCrv = offsets[0];
                            Brep[] lofts = Brep.CreateFromLoft(new Curve[] { crv, offCrv }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                            
                            if (lofts != null && lofts.Length > 0)
                            {
                                outSlabs.Append(new GH_Brep(lofts[0]), path);
                                slabArea += crv.GetLength() * Math.Abs(depth);
                            }

                            Extrusion extRail = Extrusion.Create(offCrv, railHeight, false);
                            if (extRail != null) outRails.Append(new GH_Brep(extRail.ToBrep()), path);
                        }
                    }

                    // --- INNER WALL LOGIC ---
                    int vIdx = vIndices[i % vIndices.Count];
                    string activeHStr = cleanHPats[vIdx % cleanHPats.Count];
                    List<char> pattern = activeHStr.ToList();

                    Curve[] segments = crv.DuplicateSegments();
                    if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

                    List<Curve> bayCrvs = new List<Curve>();
                    List<Curve> cornerCrvs = new List<Curve>();
                    List<Curve> middleCrvs = new List<Curve>();

                    foreach (Curve seg in segments)
                    {
                        double length = seg.GetLength();

                        if (segLength && length < (bayWidth * 2.0))
                        {
                            cornerCrvs.Add(seg);
                        }
                        else if (cornerOffset > 0 && length > (cornerOffset * 2.01))
                        {
                            seg.LengthParameter(cornerOffset, out double t1);
                            seg.LengthParameter(length - cornerOffset, out double t2);

                            Curve corner1 = seg.Trim(seg.Domain.Min, t1);
                            Curve corner2 = seg.Trim(t2, seg.Domain.Max);
                            Curve middle = seg.Trim(t1, t2);

                            if (corner1 != null) cornerCrvs.Add(corner1);
                            if (corner2 != null) cornerCrvs.Add(corner2);
                            if (middle != null) middleCrvs.Add(middle);
                        }
                        else
                        {
                            middleCrvs.Add(seg);
                        }
                    }

                    // Subdivide Middle Segments into Standard Bays
                    foreach (Curve middle in middleCrvs)
                    {
                        double midLen = middle.GetLength();
                        int divCount = Math.Max(1, (int)Math.Round(midLen / bayWidth));
                        double[] tParams = middle.DivideByCount(divCount, true);
                        
                        if (tParams != null && tParams.Length >= 2)
                        {
                            for (int pIdx = 0; pIdx < tParams.Length - 1; pIdx++)
                            {
                                Curve bayCurve = middle.Trim(new Interval(tParams[pIdx], tParams[pIdx + 1]));
                                if (bayCurve != null) bayCrvs.Add(bayCurve);
                            }
                        }
                        else
                        {
                            bayCrvs.Add(middle);
                        }
                    }

                    // --- GENERATE CORNER GEOMETRY ---
                    foreach (Curve cCrv in cornerCrvs)
                    {
                        double cLen = cCrv.GetLength();

                        if (cornerWindow)
                        {
                            if (sillHeight > 0)
                            {
                                Extrusion extSill = Extrusion.Create(cCrv, sillHeight, false);
                                if (extSill != null)
                                {
                                    outSolid.Append(new GH_Brep(extSill.ToBrep()), path);
                                    solidArea += cLen * sillHeight;
                                }
                            }

                            double visionH = h - sillHeight - headerDrop;
                            if (visionH > 0)
                            {
                                Curve cGlass = cCrv.DuplicateCurve();
                                cGlass.Transform(Transform.Translation(new Vector3d(0, 0, sillHeight)));
                                Extrusion extGlass = Extrusion.Create(cGlass, visionH, false);
                                if (extGlass != null)
                                {
                                    outCorners.Append(new GH_Brep(extGlass.ToBrep()), path);
                                    cornerArea += cLen * visionH;
                                }
                            }

                            if (headerDrop > 0)
                            {
                                Curve cTop = cCrv.DuplicateCurve();
                                cTop.Transform(Transform.Translation(new Vector3d(0, 0, h - headerDrop)));
                                Extrusion extTop = Extrusion.Create(cTop, headerDrop, false);
                                if (extTop != null)
                                {
                                    outHeaders.Append(new GH_Brep(extTop.ToBrep()), path);
                                    headerArea += cLen * headerDrop;
                                }
                            }
                        }
                        else
                        {
                            Extrusion extCorner = Extrusion.Create(cCrv, h, false);
                            if (extCorner != null)
                            {
                                outCorners.Append(new GH_Brep(extCorner.ToBrep()), path);
                                cornerArea += cLen * h;
                            }
                        }
                    }

                    // --- GENERATE STANDARD BAY GEOMETRY ---
                    for (int bayIndex = 0; bayIndex < bayCrvs.Count; bayIndex++)
                    {
                        Curve bayCrv = bayCrvs[bayIndex];
                        bool isGlass = pattern[bayIndex % pattern.Count] == '1';
                        double bayLen = bayCrv.GetLength();

                        if (isGlass)
                        {
                            if (sillHeight > 0)
                            {
                                Extrusion extSill = Extrusion.Create(bayCrv, sillHeight, false);
                                if (extSill != null)
                                {
                                    outSolid.Append(new GH_Brep(extSill.ToBrep()), path);
                                    solidArea += bayLen * sillHeight;
                                }
                            }

                            double visionH = h - sillHeight - headerDrop;
                            if (visionH > 0)
                            {
                                Curve cGlass = bayCrv.DuplicateCurve();
                                cGlass.Transform(Transform.Translation(new Vector3d(0, 0, sillHeight)));
                                Extrusion extGlass = Extrusion.Create(cGlass, visionH, false);
                                if (extGlass != null)
                                {
                                    outGlass.Append(new GH_Brep(extGlass.ToBrep()), path);
                                    glassArea += bayLen * visionH;
                                }
                            }

                            if (headerDrop > 0)
                            {
                                Curve cTop = bayCrv.DuplicateCurve();
                                cTop.Transform(Transform.Translation(new Vector3d(0, 0, h - headerDrop)));
                                Extrusion extTop = Extrusion.Create(cTop, headerDrop, false);
                                if (extTop != null)
                                {
                                    outHeaders.Append(new GH_Brep(extTop.ToBrep()), path);
                                    headerArea += bayLen * headerDrop;
                                }
                            }
                        }
                        else
                        {
                            Extrusion extSolid = Extrusion.Create(bayCrv, h, false);
                            if (extSolid != null)
                            {
                                outSolid.Append(new GH_Brep(extSolid.ToBrep()), path);
                                solidArea += bayLen * h;
                            }
                        }
                    }
                }
            }

            watch.Stop();

            DA.SetDataTree(0, outSlabs);
            DA.SetDataTree(1, outRails);
            DA.SetDataTree(2, outGlass);
            DA.SetDataTree(3, outSolid);
            DA.SetDataTree(4, outHeaders);
            DA.SetDataTree(5, outCorners);

            string fbStatus = firstBalcony ? "On" : "Off";
            string coStatus = cornerOffset > 0 ? cornerOffset.ToString("0.0#") + "m" : "Off";

            if (glassArea > 0 || solidArea > 0 || cornerArea > 0)
            {
                this.Message = string.Format(
                    "{8}\nTime: {0} ms\nFirst Balcony: {1}\nCorner Offset: {2}\n---\nGlass:   {3:N0} SQM\nSolid:   {4:N0} SQM\nHead:    {5:N0} SQM\nCorner:  {6:N0} SQM\nTerrace: {7:N0} SQM",
                    watch.ElapsedMilliseconds, fbStatus, coStatus, glassArea, solidArea, headerArea, cornerArea, slabArea, this.NickName);
            }
            else
            {
                this.Message = this.NickName + "\nAwaiting Data";
            }
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Mod_Balcony.png");

        public override Guid ComponentGuid => new Guid("14d64098-b8cf-4a11-bf32-e4b2d3525cb7");

        public override GH_Exposure Exposure => GH_Exposure.secondary;
    }
}
