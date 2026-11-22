using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Enzyme.Utils;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class ModRetailComponent : GH_Component
    {
        public ModRetailComponent()
          : base("Facade Module: Storefront", "Mod_Retail",
              "Generates patterned storefronts from planar outline curves: bays with a solid kickplate, a glass or solid middle band and a header band, mullion fins, optional flat canopies and column lines at the mullions.",
              "Enzyme", "JSON Masterplan (Beta)")
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
                AutoWireDefaultInputs(document);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Bounds", "Bounds", "Planar storefront outline curves in world XY (one per facade or floor), as a tree.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "Heights", "Storefront height per curve in m, matched by branch path and index (default 4.5).", GH_ParamAccess.tree);
            pManager.AddNumberParameter("BayWidth", "BayWidth", "Target bay width in m; each segment gets round(length / BayWidth) equal bays (0 or less = 2.5).", GH_ParamAccess.item);
            pManager.AddNumberParameter("BaseHeight", "BaseHeight", "Height of the solid kickplate band in m (may be 0).", GH_ParamAccess.item);
            pManager.AddNumberParameter("TransomHeight", "TransomHeight", "Height in m of the top of the middle (glass) band; above it is the header band. If 0 or unset the whole height is header, with no glass or mullions.", GH_ParamAccess.item);
            pManager.AddNumberParameter("MullionDepth", "MullionDepth", "Depth of the mullion fins in m (0 or less = 0.2).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("FlipMullions", "FlipMullions", "True puts the mullion fins on the other side of the curve.", GH_ParamAccess.item);
            pManager.AddTextParameter("HorizPattern", "HorizPattern", "List of 0/1 strings, one per pattern line: 1 = glass bay, 0 = solid bay, repeated along the curve.", GH_ParamAccess.list);
            pManager.AddTextParameter("VertPattern", "VertPattern", "String of digits: digit i (repeated) picks which HorizPattern line curve i uses.", GH_ParamAccess.item);
            pManager.AddNumberParameter("CanopyDepth", "CanopyDepth", "Depth of the flat canopy at transom height in m (0 = no canopy).", GH_ParamAccess.item);
            pManager.AddTextParameter("CanopyPattern", "CanopyPattern", "0/1 string repeated over the curves: 1 = curve gets a canopy.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("FlipCanopy", "FlipCanopy", "True offsets the canopy to the other side of the curve.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("ShowColumns", "ShowColumns", "True draws a column line at each mullion under a canopy.", GH_ParamAccess.item);
            pManager.AddNumberParameter("ColumnOffset", "ColumnOffset", "Distance in m of the column lines in from the canopy edge (0 or less = 0.2).", GH_ParamAccess.item);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
            pManager[11].Optional = true;
            pManager[12].Optional = true;
            pManager[13].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Glass", "Glass", "Glass panels of the middle band (surfaces).", GH_ParamAccess.tree);
            pManager.AddBrepParameter("SolidPanels", "SolidPanels", "Solid kickplate panels and solid middle-band panels (surfaces).", GH_ParamAccess.tree);
            pManager.AddBrepParameter("HeaderPanels", "HeaderPanels", "Header band panels above the transom (surfaces).", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Mullions", "Mullions", "Mullion fins at the bay ends, spanning the middle band (surfaces).", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Canopy", "Canopy", "Flat canopy surfaces at transom height.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Columns", "Columns", "Column centre lines from the ground to transom height.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Info", "Info", "Method, assumptions, simplifications and references.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (DA.Iteration == 0 && Params.Output.Count > 6) DA.SetData(6, Enzyme.Utils.MethodNotes.Info("ModRetail"));
            var watch = System.Diagnostics.Stopwatch.StartNew();

            GH_Structure<GH_Curve> ghBounds;
            if (!DA.GetDataTree(0, out ghBounds)) return;
            DataTree<Curve> Bounds = new DataTree<Curve>();
            foreach (GH_Path path in ghBounds.Paths)
            {
                var branch = ghBounds.get_Branch(path);
                foreach (GH_Curve item in branch)
                {
                    if (item != null && item.Value != null)
                        Bounds.Add(item.Value, path);
                }
            }

            GH_Structure<GH_Number> ghHeights;
            DA.GetDataTree(1, out ghHeights);
            DataTree<double> Heights = new DataTree<double>();
            if (ghHeights != null)
            {
                foreach (GH_Path path in ghHeights.Paths)
                {
                    var branch = ghHeights.get_Branch(path);
                    foreach (GH_Number item in branch)
                    {
                        if (item != null)
                            Heights.Add(item.Value, path);
                    }
                }
            }

            double BayWidth = 0;
            DA.GetData(2, ref BayWidth);
            double BaseHeight = 0;
            DA.GetData(3, ref BaseHeight);
            double TransomHeight = 0;
            if (!DA.GetData(4, ref TransomHeight) || TransomHeight <= 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "TransomHeight is 0 or unset: the whole height is header band, with no glass or mullions.");
            double MullionDepth = 0;
            DA.GetData(5, ref MullionDepth);
            bool FlipMullions = false;
            DA.GetData(6, ref FlipMullions);
            List<string> HorizPattern = new List<string>();
            DA.GetDataList(7, HorizPattern);
            string VertPattern = null;
            DA.GetData(8, ref VertPattern);
            double CanopyDepth = 0;
            DA.GetData(9, ref CanopyDepth);
            string CanopyPattern = null;
            DA.GetData(10, ref CanopyPattern);
            bool FlipCanopy = false;
            DA.GetData(11, ref FlipCanopy);
            bool ShowColumns = false;
            DA.GetData(12, ref ShowColumns);
            double ColumnOffset = 0;
            DA.GetData(13, ref ColumnOffset);

            // 1. Safe Defaults (Ensuring 0 is respected where intentional)
            if (BayWidth <= 0.0) BayWidth = 2.5;
            if (MullionDepth <= 0.0) MullionDepth = 0.2;
            if (ColumnOffset <= 0.0) ColumnOffset = 0.2;
            // BaseHeight, TransomHeight, and CanopyDepth are allowed to be 0.0

            // 2. Parse Patterns
            if (HorizPattern == null || HorizPattern.Count == 0) HorizPattern = new List<string> { "1" };
            List<string> cleanHPats = new List<string>();
            foreach (var pat in HorizPattern)
            {
                string clean = new string((pat ?? "1").Where(c => c == '0' || c == '1').ToArray());
                cleanHPats.Add(string.IsNullOrEmpty(clean) ? "1" : clean);
            }

            string rawVPat = (VertPattern ?? "0").Trim();
            List<int> vIndices = rawVPat.Where(char.IsDigit).Select(c => (int)char.GetNumericValue(c)).ToList();
            if (vIndices.Count == 0) vIndices.Add(0);

            string rawCPat = (CanopyPattern ?? "1").Trim();
            List<char> cPattern = rawCPat.Where(c => c == '0' || c == '1').ToList();
            if (cPattern.Count == 0) cPattern.Add('1');

            // 3. Initialize Output Trees
            DataTree<Brep> outGlass = new DataTree<Brep>();
            DataTree<Brep> outSolid = new DataTree<Brep>();
            DataTree<Brep> outHeaders = new DataTree<Brep>();
            DataTree<Brep> outMullions = new DataTree<Brep>();
            DataTree<Brep> outCanopy = new DataTree<Brep>();
            DataTree<Curve> outColumns = new DataTree<Curve>();

            double glassArea = 0, solidArea = 0, headerArea = 0;
            int colCount = 0;

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

                    double h = (hts.Count > i) ? hts[i] : 4.5;

                    // Clamp heights safely to prevent negative extrusions
                    double actualTransomH = Math.Min(TransomHeight, h);
                    double actualBaseH = Math.Min(BaseHeight, actualTransomH);
                    double midH = actualTransomH - actualBaseH;
                    double headH = h - actualTransomH;

                    // --- CANOPY LOGIC ---
                    bool hasCanopy = cPattern[i % cPattern.Count] == '1';
                    Curve baseOffCrv = null; // Store for column targeting

                    if (hasCanopy && CanopyDepth > 0 && actualTransomH > 0)
                    {
                        double dist = FlipCanopy ? -CanopyDepth : CanopyDepth;

                        // Generate 3D Canopy
                        Curve cTransom = crv.DuplicateCurve();
                        cTransom.Transform(Transform.Translation(new Vector3d(0, 0, actualTransomH)));
                        
                        // Loosened tolerance (0.1) for faster offset solving
                        Curve[] offsets = cTransom.Offset(Plane.WorldXY, dist, 0.1, CurveOffsetCornerStyle.Sharp);
                        if (offsets != null && offsets.Length > 0)
                        {
                            Brep[] lofts = Brep.CreateFromLoft(new Curve[] { cTransom, offsets[0] }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                            if (lofts != null && lofts.Length > 0) outCanopy.Add(lofts[0], path);
                        }

                        // Generate 2D Base Curve for Column Reference
                        Curve[] baseOffsets = crv.Offset(Plane.WorldXY, dist, 0.1, CurveOffsetCornerStyle.Sharp);
                        if (baseOffsets != null && baseOffsets.Length > 0) baseOffCrv = baseOffsets[0];
                    }

                    // --- INNER WALL LOGIC ---
                    int vIdx = vIndices[i % vIndices.Count];
                    string activeHStr = cleanHPats[vIdx % cleanHPats.Count];
                    List<char> pattern = activeHStr.ToList();

                    Curve[] segments = crv.DuplicateSegments();
                    if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

                    List<Curve> bayCrvs = new List<Curve>();
                    HashSet<string> placedPts = new HashSet<string>();

                    foreach (Curve seg in segments)
                    {
                        double length = seg.GetLength();
                        int divCount = Math.Max(1, (int)Math.Round(length / BayWidth));

                        double[] tParams = seg.DivideByCount(divCount, true);
                        if (tParams != null && tParams.Length >= 2)
                        {
                            for (int pIdx = 0; pIdx < tParams.Length - 1; pIdx++)
                            {
                                Curve bayCurve = seg.Trim(new Interval(tParams[pIdx], tParams[pIdx + 1]));
                                if (bayCurve != null) bayCrvs.Add(bayCurve);
                            }
                        }
                    }

                    for (int bayIndex = 0; bayIndex < bayCrvs.Count; bayIndex++)
                    {
                        Curve bayCrv = bayCrvs[bayIndex];
                        bool isGlass = pattern[bayIndex % pattern.Count] == '1';
                        double bayLen = bayCrv.GetLength();

                        // 1. Solid Base (Kickplate)
                        if (actualBaseH > 0)
                        {
                            Brep bWall = ExtrudeUp(bayCrv, actualBaseH);
                            if (bWall != null)
                            {
                                outSolid.Add(bWall, path);
                                solidArea += bayLen * actualBaseH;
                            }
                        }

                        // 2. Middle Vision/Solid Band
                        if (midH > 0)
                        {
                            Curve midCrv = bayCrv.DuplicateCurve();
                            midCrv.Transform(Transform.Translation(new Vector3d(0, 0, actualBaseH)));
                            Brep midWall = ExtrudeUp(midCrv, midH);
                            if (midWall != null)
                            {
                                if (isGlass)
                                {
                                    outGlass.Add(midWall, path);
                                    glassArea += bayLen * midH;
                                }
                                else
                                {
                                    outSolid.Add(midWall, path);
                                    solidArea += bayLen * midH;
                                }
                            }
                        }

                        // 3. Header Band (Signage)
                        if (headH > 0)
                        {
                            Curve headCrv = bayCrv.DuplicateCurve();
                            headCrv.Transform(Transform.Translation(new Vector3d(0, 0, actualTransomH)));
                            Brep headWall = ExtrudeUp(headCrv, headH);
                            if (headWall != null)
                            {
                                outHeaders.Add(headWall, path);
                                headerArea += bayLen * headH;
                            }
                        }

                        // 4. Mullions & Structural Columns
                        if (midH > 0)
                        {
                            double[] tVals = { 0.0, 1.0 };
                            foreach (double tVal in tVals)
                            {
                                Point3d pt = bayCrv.PointAtNormalizedLength(tVal);
                                string ptKey = $"{Math.Round(pt.X, 3)},{Math.Round(pt.Y, 3)},{Math.Round(pt.Z, 3)}";

                                // Deduplicate points using C# HashSet
                                if (placedPts.Add(ptKey))
                                {
                                    if (bayCrv.ClosestPoint(pt, out double tParam))
                                    {
                                        Vector3d tan = bayCrv.TangentAt(tParam);
                                        Vector3d normal = new Vector3d(-tan.Y, tan.X, 0);
                                        normal.Unitize();

                                        // Independent Mullion Flip
                                        if (FlipMullions) normal.Reverse();

                                        // A. Place the Mullion (At the inner facade line with parametric depth)
                                        Line finLine = new Line(pt, pt + (normal * MullionDepth));
                                        finLine.Transform(Transform.Translation(new Vector3d(0, 0, actualBaseH)));
                                        Brep finExt = ExtrudeUp(finLine.ToNurbsCurve(), midH);
                                        if (finExt != null) outMullions.Add(finExt, path);

                                        // B. Place the Canopy Column (Targeting the actual outer offset curve)
                                        if (ShowColumns && baseOffCrv != null)
                                        {
                                            if (baseOffCrv.ClosestPoint(pt, out double tOff))
                                            {
                                                Point3d outerPt = baseOffCrv.PointAt(tOff);
                                                Vector3d inwardVec = pt - outerPt;

                                                Point3d colBasePt = pt;
                                                if (inwardVec.Length > 0.01)
                                                {
                                                    inwardVec.Unitize();
                                                    colBasePt = outerPt + (inwardVec * ColumnOffset);
                                                }

                                                // Draw the column from the base up to the transom height
                                                Line colLine = new Line(colBasePt, colBasePt + new Vector3d(0, 0, actualTransomH));
                                                outColumns.Add(colLine.ToNurbsCurve(), path);
                                                colCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            watch.Stop();

            // 5. Outputs
            DA.SetDataTree(0, outGlass);
            DA.SetDataTree(1, outSolid);
            DA.SetDataTree(2, outHeaders);
            DA.SetDataTree(3, outMullions);
            DA.SetDataTree(4, outCanopy);
            DA.SetDataTree(5, outColumns);

            // 6. Update UI
            string vStr = string.Join("", vIndices);
            string cStr = string.Join("", cPattern);

            if (glassArea > 0 || solidArea > 0)
            {
                Hud.Done(this, watch,
                    $"Wall matrix: [{vStr}]", $"Canopy rhythm: [{cStr}]",
                    Hud.Metric("Glass", glassArea, "m²", 0), Hud.Metric("Solid", solidArea, "m²", 0),
                    Hud.Metric("Head", headerArea, "m²", 0), colCount > 0 ? Hud.Metric("Cols", colCount) : null);
            }
            else
            {
                Hud.Waiting(this);
            }
        }

        // Extrudes a curve straight up world +Z. (Extrusion.Create used the curve's own plane normal, which for lines is
        // not unique and for arcs follows the arc orientation, so bands and fins could go sideways or downwards.)
        private static Brep ExtrudeUp(Curve crv, double height)
        {
            if (crv == null || height <= 0) return null;
            Surface srf = Surface.CreateExtrusion(crv, new Vector3d(0, 0, height));
            return srf != null ? srf.ToBrep() : null;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => IconLoader.Load("Mod_Retail.png");

        
        private void AutoWireDefaultInputs(GH_Document document)
        {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 6.0, 330, -220);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 5.0, 0.0, 330, -180);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 10.0, 6.0, 330, -140);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 2.0, 0.4, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, false, 210, -60);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 7, "1111111111\n1000100010\n0000000000", 250, -20, 120, 60);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 8, "0111", 250, 40, 100, 25);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 5.0, 2.5, 330, 80);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 10, "100", 250, 120, 100, 25);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 11, false, 210, 160);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 200);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 13, 0.0, 5.0, 1.5, 330, 240);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(150, 200, 255), 220, -188);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(250, 250, 250), 220, -113);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(250, 250, 250), 220, -38);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 3, System.Drawing.Color.FromArgb(50, 50, 50), 220, 37);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 4, System.Drawing.Color.FromArgb(200, 200, 200), 220, 112);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "curve", 220, 187);
            }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc != null) AutoWireDefaultInputs(doc);
            });
        }

        public override Guid ComponentGuid => new Guid("0FEE5394-D9AC-4BB5-A7B8-9333917830A3");
    }
}
