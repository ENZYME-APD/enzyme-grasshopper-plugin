using System;
using System.Linq;
using System.Collections.Generic;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Masterplan
{
    public class ModRhythmBalcComponent : GH_Component
    {
        public ModRhythmBalcComponent()
          : base("Facade Module: Rhythmic Balconies", "Mod_Rhythm_Balc",
              "2D patterned balconies with merge logic, corner isolation, and fast math.",
              "Enzyme", "Masterplan (Beta)")
        {
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 3.5, 330, -220);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 3.0, 2.9, 330, -180);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 3.0, 1.5, 330, -140);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 2.0, 1.0, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 6, "0110101\n1010110", 250, -60, 100, 40);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 7, "10", 250, 0, 100, 25);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 8, false, 210, 40);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 9, false, 210, 80);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10, true, 210, 120);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 160);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 12, 0.0, 5.0, 3.0, 330, 200);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 240);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(180, 180, 180), 220, -240);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(150, 150, 150), 220, -165);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(230, 230, 230), 220, -90);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 3, System.Drawing.Color.FromArgb(150, 200, 255), 220, -15);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 4, System.Drawing.Color.FromArgb(250, 250, 250), 220, 60);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 5, System.Drawing.Color.FromArgb(250, 250, 250), 220, 135);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 6, System.Drawing.Color.FromArgb(250, 250, 250), 220, 210);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Bounds", "B", "Curve boundaries", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "H", "Heights", GH_ParamAccess.tree);
            pManager.AddNumberParameter("BayWidth", "BW", "Bay width", GH_ParamAccess.item, 3.0);
            pManager.AddNumberParameter("Depth", "D", "Depth", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("RailHeight", "RH", "Rail height", GH_ParamAccess.item, 1.1);
            pManager.AddNumberParameter("HeaderDrop", "HD", "Header drop", GH_ParamAccess.item, 0.0);
            pManager.AddTextParameter("HorizPattern", "HP", "Horizontal pattern", GH_ParamAccess.list);
            pManager[6].Optional = true;
            pManager.AddTextParameter("VertPattern", "VP", "Vertical pattern", GH_ParamAccess.item, "0");
            pManager.AddBooleanParameter("MergeAdjacent", "MA", "Merge adjacent", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("FlipDir", "FD", "Flip direction", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("FirstBalcony", "FB", "First balcony", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("CornerWindow", "CW", "Corner window", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("CornerOffset", "CO", "Corner offset", GH_ParamAccess.item, 0.0);
            pManager.AddBooleanParameter("SegLength", "SL", "Seg length", GH_ParamAccess.item, false);
            
            pManager[1].Optional = true; // Heights is optional since it has a fallback logic
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Slabs", "S", "Slabs", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Railings", "R", "Railings", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Partitions", "P", "Partitions", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Glass", "G", "Glass", GH_ParamAccess.tree);
            pManager.AddBrepParameter("SolidPanels", "SP", "Solid panels", GH_ParamAccess.tree);
            pManager.AddBrepParameter("HeaderPanels", "HP", "Header panels", GH_ParamAccess.tree);
            pManager.AddBrepParameter("CornerPanels", "CP", "Corner panels", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            GH_Structure<GH_Curve> ghBounds = null;
            if (!DA.GetDataTree(0, out ghBounds)) return;
            
            GH_Structure<GH_Number> ghHeights = null;
            DA.GetDataTree(1, out ghHeights); // Optional
            
            double BayWidth = 3.0;
            DA.GetData(2, ref BayWidth);
            
            double Depth = 1.5;
            DA.GetData(3, ref Depth);
            
            double RailHeight = 1.1;
            DA.GetData(4, ref RailHeight);
            
            double HeaderDrop = 0.0;
            DA.GetData(5, ref HeaderDrop);
            
            List<string> HorizPattern = new List<string>();
            if (!DA.GetDataList(6, HorizPattern) || HorizPattern.Count == 0) HorizPattern.Add("10");
            
            string VertPattern = "0";
            DA.GetData(7, ref VertPattern);
            
            bool MergeAdjacent = false;
            DA.GetData(8, ref MergeAdjacent);
            
            bool FlipDir = false;
            DA.GetData(9, ref FlipDir);
            
            bool FirstBalcony = false;
            DA.GetData(10, ref FirstBalcony);
            
            bool CornerWindow = false;
            DA.GetData(11, ref CornerWindow);
            
            double CornerOffset = 0.0;
            DA.GetData(12, ref CornerOffset);
            
            bool SegLength = false;
            DA.GetData(13, ref SegLength);

            // Convert GH_Structure to DataTree for ease of porting
            DataTree<Curve> Bounds = new DataTree<Curve>();
            if (ghBounds != null)
            {
                for (int p = 0; p < ghBounds.PathCount; p++)
                {
                    var path = ghBounds.get_Path(p);
                    var list = ghBounds.get_Branch(path);
                    foreach (GH_Curve ghCrv in list)
                    {
                        if (ghCrv != null && ghCrv.Value != null)
                            Bounds.Add(ghCrv.Value, path);
                        else
                            Bounds.Add(null, path);
                    }
                }
            }
            
            DataTree<double> Heights = new DataTree<double>();
            if (ghHeights != null)
            {
                for (int p = 0; p < ghHeights.PathCount; p++)
                {
                    var path = ghHeights.get_Path(p);
                    var list = ghHeights.get_Branch(path);
                    foreach (GH_Number ghNum in list)
                    {
                        if (ghNum != null)
                            Heights.Add(ghNum.Value, path);
                        else
                            Heights.Add(4.0, path); // Default fallback
                    }
                }
            }

            // 1. Safe Defaults
            if (BayWidth <= 0.0) BayWidth = 3.0;
            if (Depth <= 0.0) Depth = 1.5;
            if (RailHeight <= 0.0) RailHeight = 1.1;
            if (HeaderDrop < 0.0) HeaderDrop = 0.0;
            if (CornerOffset < 0.0) CornerOffset = 0.0;

            // 2. Parse Patterns
            List<string> cleanHPats = new List<string>();
            foreach (var pat in HorizPattern)
            {
                string clean = new string((pat ?? "10").Where(c => c == '0' || c == '1').ToArray());
                cleanHPats.Add(string.IsNullOrEmpty(clean) ? "10" : clean);
            }

            string rawVPat = (VertPattern ?? "0").Trim();
            List<int> vIndices = rawVPat.Where(char.IsDigit).Select(c => (int)char.GetNumericValue(c)).ToList();
            if (vIndices.Count == 0) vIndices.Add(0);

            // 3. Initialize Output Trees
            DataTree<Brep> outSlabs = new DataTree<Brep>();
            DataTree<Brep> outRails = new DataTree<Brep>();
            DataTree<Brep> outParts = new DataTree<Brep>();
            DataTree<Brep> outGlass = new DataTree<Brep>();
            DataTree<Brep> outSolid = new DataTree<Brep>();
            DataTree<Brep> outHeaders = new DataTree<Brep>();
            DataTree<Brep> outCorners = new DataTree<Brep>();

            double glassArea = 0, solidArea = 0, headerArea = 0, slabArea = 0, cornerArea = 0;
            int balconyCount = 0;

            // 4. Main Processing Loop (Tree Support)
            for (int p = 0; p < Bounds.BranchCount; p++)
            {
                GH_Path path = Bounds.Path(p);
                List<Curve> crvs = Bounds.Branch(path);
                List<double> hts = Heights.PathExists(path) ? Heights.Branch(path) : new List<double>();

                for (int i = 0; i < crvs.Count; i++)
                {
                    Curve crv = crvs[i];
                    if (crv == null) continue;

                    double h = (hts.Count > i) ? hts[i] : 4.0;
                    double dist = FlipDir ? -Depth : Depth;

                    // --- INNER WALL LOGIC & CORNER ISOLATION ---
                    int vIdx = vIndices[i % vIndices.Count];
                    string activeHStr = cleanHPats[vIdx % cleanHPats.Count];
                    List<char> pattern = activeHStr.ToList();

                    Curve[] segments = crv.DuplicateSegments();
                    if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

                    List<Curve> cornerCrvs = new List<Curve>();
                    List<Curve> middleCrvs = new List<Curve>();

                    foreach (Curve seg in segments)
                    {
                        double length = seg.GetLength();

                        // FEATURE: Fillet / Short Segment Bypass
                        if (SegLength && length < (BayWidth * 2.0))
                        {
                            cornerCrvs.Add(seg);
                        }
                        // FEATURE: Standard Corner Isolation (Auto-triggers if offset > 0)
                        else if (CornerOffset > 0 && length > (CornerOffset * 2.01))
                        {
                            seg.LengthParameter(CornerOffset, out double t1);
                            seg.LengthParameter(length - CornerOffset, out double t2);

                            Curve corner1 = seg.Trim(seg.Domain.Min, t1);
                            Curve corner2 = seg.Trim(t2, seg.Domain.Max);
                            Curve middle = seg.Trim(t1, t2);

                            if (corner1 != null) cornerCrvs.Add(corner1);
                            if (corner2 != null) cornerCrvs.Add(corner2);
                            if (middle != null) middleCrvs.Add(middle);
                        }
                        else
                        {
                            // No corners requested, or segment too short
                            middleCrvs.Add(seg);
                        }
                    }

                    // --- GENERATE CORNER GEOMETRY ---
                    foreach (Curve cCrv in cornerCrvs)
                    {
                        double cLen = cCrv.GetLength();
                        if (CornerWindow)
                        {
                            // Corner Window Logic (Matches Mod 03)
                            double visionH = h - HeaderDrop;
                            if (visionH > 0)
                            {
                                Extrusion extGlass = Extrusion.Create(cCrv, visionH, false);
                                if (extGlass != null)
                                {
                                    outCorners.Add(extGlass.ToBrep(), path);
                                    cornerArea += cLen * visionH;
                                }
                            }
                            if (HeaderDrop > 0)
                            {
                                Curve cTop = cCrv.DuplicateCurve();
                                cTop.Transform(Transform.Translation(new Vector3d(0, 0, visionH)));
                                Extrusion extTop = Extrusion.Create(cTop, HeaderDrop, false);
                                if (extTop != null)
                                {
                                    outHeaders.Add(extTop.ToBrep(), path);
                                    headerArea += cLen * HeaderDrop;
                                }
                            }
                        }
                        else
                        {
                            // Solid Column Corner
                            Extrusion extCorner = Extrusion.Create(cCrv, h, false);
                            if (extCorner != null)
                            {
                                outCorners.Add(extCorner.ToBrep(), path);
                                cornerArea += cLen * h;
                            }
                        }
                    }

                    // --- SUBDIVIDE MIDDLE SEGMENTS INTO BAYS ---
                    List<List<Curve>> allMiddleBays = new List<List<Curve>>();
                    int totalGlobalBays = 0;

                    foreach (Curve middle in middleCrvs)
                    {
                        List<Curve> bayCrvs = new List<Curve>();
                        double midLen = middle.GetLength();
                        int divCount = Math.Max(1, (int)Math.Round(midLen / BayWidth));
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

                        allMiddleBays.Add(bayCrvs);
                        totalGlobalBays += bayCrvs.Count;
                    }

                    // --- GENERATE RHYTHMIC BAYS ---
                    int globalBayIndex = 0;
                    
                    for (int m = 0; m < allMiddleBays.Count; m++)
                    {
                        List<Curve> bayCrvs = allMiddleBays[m];
                        int baysInSeg = bayCrvs.Count;

                        for (int b = 0; b < baysInSeg; b++)
                        {
                            Curve bayCrv = bayCrvs[b];
                            bool isActive = pattern[globalBayIndex % pattern.Count] == '1';
                            double bayLen = bayCrv.GetLength();

                            // FEATURE: Ground floor balcony override
                            if (!FirstBalcony && i == 0) isActive = false;

                            if (isActive)
                            {
                                // 1. Inner Wall Glass
                                double glassH = h - HeaderDrop;
                                if (glassH > 0)
                                {
                                    Extrusion glassWall = Extrusion.Create(bayCrv, glassH, false);
                                    if (glassWall != null)
                                    {
                                        outGlass.Add(glassWall.ToBrep(), path);
                                        glassArea += bayLen * glassH;
                                    }
                                }

                                // 2. Inner Wall Header
                                if (HeaderDrop > 0)
                                {
                                    Curve topCrv = bayCrv.DuplicateCurve();
                                    topCrv.Transform(Transform.Translation(new Vector3d(0, 0, glassH)));
                                    Extrusion topWall = Extrusion.Create(topCrv, HeaderDrop, false);
                                    if (topWall != null)
                                    {
                                        outHeaders.Add(topWall.ToBrep(), path);
                                        headerArea += bayLen * HeaderDrop;
                                    }
                                }

                                // 3. Rhythmic Balcony & Railing
                                Curve[] offsets = bayCrv.Offset(Plane.WorldXY, dist, 0.1, CurveOffsetCornerStyle.Sharp);
                                if (offsets != null && offsets.Length > 0)
                                {
                                    Curve offCrv = offsets[0];
                                    Brep[] lofts = Brep.CreateFromLoft(new Curve[] { bayCrv, offCrv }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                                    
                                    if (lofts != null && lofts.Length > 0)
                                    {
                                        outSlabs.Add(lofts[0], path);
                                        slabArea += bayLen * Math.Abs(Depth); // High-performance area
                                    }

                                    Extrusion extRail = Extrusion.Create(offCrv, RailHeight, false);
                                    if (extRail != null) outRails.Add(extRail.ToBrep(), path);

                                    // 4. Adjacency Logic (Partition Walls)
                                    bool prevActive = false;
                                    if (b > 0) 
                                        prevActive = pattern[(globalBayIndex - 1) % pattern.Count] == '1';
                                    else if (crv.IsClosed && CornerOffset == 0 && middleCrvs.Count == 1) 
                                        prevActive = pattern[(totalGlobalBays - 1) % pattern.Count] == '1';

                                    bool nextActive = false;
                                    if (b < baysInSeg - 1) 
                                        nextActive = pattern[(globalBayIndex + 1) % pattern.Count] == '1';
                                    else if (crv.IsClosed && CornerOffset == 0 && middleCrvs.Count == 1) 
                                        nextActive = pattern[0] == '1';

                                    Point3d p1 = bayCrv.PointAtStart;
                                    Point3d p2 = offCrv.PointAtStart;
                                    Point3d p3 = bayCrv.PointAtEnd;
                                    Point3d p4 = offCrv.PointAtEnd;

                                    if (!(MergeAdjacent && prevActive))
                                    {
                                        Line partLine1 = new Line(p1, p2);
                                        Extrusion part1 = Extrusion.Create(partLine1.ToNurbsCurve(), RailHeight, false);
                                        if (part1 != null) outParts.Add(part1.ToBrep(), path);
                                    }

                                    if (!(MergeAdjacent && nextActive))
                                    {
                                        Line partLine2 = new Line(p3, p4);
                                        Extrusion part2 = Extrusion.Create(partLine2.ToNurbsCurve(), RailHeight, false);
                                        if (part2 != null) outParts.Add(part2.ToBrep(), path);
                                    }
                                }
                                balconyCount++;
                            }
                            else
                            {
                                // Solid Wall (No Balcony)
                                Extrusion solidWall = Extrusion.Create(bayCrv, h, false);
                                if (solidWall != null)
                                {
                                    outSolid.Add(solidWall.ToBrep(), path);
                                    solidArea += bayLen * h;
                                }
                            }
                            globalBayIndex++;
                        }
                    }
                }
            }

            watch.Stop();

            // 5. Outputs
            DA.SetDataTree(0, outSlabs);
            DA.SetDataTree(1, outRails);
            DA.SetDataTree(2, outParts);
            DA.SetDataTree(3, outGlass);
            DA.SetDataTree(4, outSolid);
            DA.SetDataTree(5, outHeaders);
            DA.SetDataTree(6, outCorners);

            // 6. Update UI
            string vStr = string.Join("", vIndices);
            string fbStatus = FirstBalcony ? "On" : "Off";
            string coStatus = CornerOffset > 0 ? CornerOffset.ToString("0.0#") + "m" : "Off";

            if (glassArea > 0 || solidArea > 0 || cornerArea > 0)
            {
                this.Message = string.Format(
                    "{8}\nTime: {0} ms\nGrnd Balc: {1}\nCorner Off: {2}\n---\nGlass:  {3:N0} SQM\nSolid:  {4:N0} SQM\nHead:   {5:N0} SQM\nCorner: {6:N0} SQM\nBalc:   {7}",
                    watch.ElapsedMilliseconds, fbStatus, coStatus, glassArea, solidArea, headerArea, cornerArea, balconyCount, this.NickName);
            }
            else
            {
                this.Message = this.NickName + "\nAwaiting Data";
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Mod_Rhythm_Balc.png");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        public override Guid ComponentGuid
        {
            get { return new Guid("0a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"); }
        }
    }
}
