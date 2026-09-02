using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class ModFramesComponent : GH_Component
    {
        public ModFramesComponent()
          : base("Facade Module: Frames", "Mod_Frames",
              "Deep structural exoskeleton grid utilizing the robust Area-Offset Method.",
              Enzyme.Utils.TabInfo.TabName, "Masterplan (Beta)")
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 3.0, 330, -220);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 2.0, 0.5, 330, -180);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 2.0, 0.4, 330, -140);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 2.0, 0.5, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 2.0, 0.4, 330, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 7, false, 210, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 2.0, 0.0, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 2.0, 0.0, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 10, 0.0, 3.0, 1.5, 330, 100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 11, 0.0, 2.0, 0.15, 330, 140);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 12, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 13, false, 210, 220);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -203);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(230, 230, 230), 220, -128);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(150, 200, 255), 220, -53);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 3, System.Drawing.Color.FromArgb(230, 230, 230), 220, 22);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 4, System.Drawing.Color.FromArgb(250, 250, 250), 220, 97);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 5, System.Drawing.Color.FromArgb(250, 250, 250), 220, 172);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Bounds", "Bounds", "Input bounds curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "Heights", "Input heights", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("BayWidth", "BayWidth", "Width of each bay", GH_ParamAccess.item, 3.0);
            pManager.AddNumberParameter("BeamWidth", "BeamWidth", "Width of the beam", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("BeamDepth", "BeamDepth", "Depth of the beam", GH_ParamAccess.item, 0.4);
            pManager.AddNumberParameter("ColWidth", "ColWidth", "Width of the column", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("ColDepth", "ColDepth", "Depth of the column", GH_ParamAccess.item, 0.4);
            pManager.AddBooleanParameter("HasPanel", "HasPanel", "Enable inner facade panels", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("SillHeight", "SillHeight", "Height of the sill", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("HeaderDrop", "HeaderDrop", "Header drop distance", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("SubdivWidth", "SubdivWidth", "Subdivision width", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("FinDepth", "FinDepth", "Depth of the fins", GH_ParamAccess.item, 0.15);
            pManager.AddBooleanParameter("FlipDir", "FlipDir", "Flip direction", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("RemoveBaseBeam", "RemoveBaseBeam", "Remove base beam", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Beams", "Beams", "Output beams", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Columns", "Columns", "Output columns", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Glass", "Glass", "Output glass panels", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Fins", "Fins", "Output fins", GH_ParamAccess.tree);
            pManager.AddBrepParameter("HeaderPanels", "HeaderPanels", "Output header panels", GH_ParamAccess.tree);
            pManager.AddBrepParameter("SillPanels", "SillPanels", "Output sill panels", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Curve> boundsTree;
            GH_Structure<GH_Number> heightsTree;
            double BayWidth = 3.0;
            double BeamWidth = 0.5;
            double BeamDepth = 0.4;
            double ColWidth = 0.5;
            double ColDepth = 0.4;
            bool HasPanel = false;
            double SillHeight = 0.0;
            double HeaderDrop = 0.0;
            double SubdivWidth = 1.5;
            double FinDepth = 0.15;
            bool FlipDir = false;
            bool RemoveBaseBeam = false;

            if (!DA.GetDataTree(0, out boundsTree)) return;
            DA.GetDataTree(1, out heightsTree);
            DA.GetData(2, ref BayWidth);
            DA.GetData(3, ref BeamWidth);
            DA.GetData(4, ref BeamDepth);
            DA.GetData(5, ref ColWidth);
            DA.GetData(6, ref ColDepth);
            DA.GetData(7, ref HasPanel);
            DA.GetData(8, ref SillHeight);
            DA.GetData(9, ref HeaderDrop);
            DA.GetData(10, ref SubdivWidth);
            DA.GetData(11, ref FinDepth);
            DA.GetData(12, ref FlipDir);
            DA.GetData(13, ref RemoveBaseBeam);

            if (boundsTree == null || boundsTree.IsEmpty) return;

            var watch = System.Diagnostics.Stopwatch.StartNew();

            // 1. Safe Defaults
            if (BayWidth <= 0.0) BayWidth = 3.0;
            if (BeamWidth <= 0.0) BeamWidth = 0.5;
            if (BeamDepth <= 0.0) BeamDepth = 0.4;
            if (ColWidth <= 0.0) ColWidth = 0.5;
            if (ColDepth <= 0.0) ColDepth = 0.4;
            if (SubdivWidth <= 0.0) SubdivWidth = 1.5;
            if (FinDepth <= 0.0) FinDepth = 0.15;
            if (SillHeight < 0.0) SillHeight = 0.0;
            if (HeaderDrop < 0.0) HeaderDrop = 0.0;

            GH_Structure<GH_Brep> outBeams = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outCols = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outGlass = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outFins = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outHeaders = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outSills = new GH_Structure<GH_Brep>();

            double glassArea = 0, solidArea = 0;
            int colCount = 0, finCount = 0;
            double displayHeight = 4.0;

            for (int p = 0; p < boundsTree.PathCount; p++)
            {
                GH_Path path = boundsTree.get_Path(p);
                var crvs = boundsTree.get_Branch(path).Cast<GH_Curve>().Select(g => g?.Value).ToList();
                var hts = heightsTree != null && heightsTree.PathExists(path) ? heightsTree.get_Branch(path).Cast<GH_Number>().Select(g => g?.Value ?? 0.0).ToList() : new List<double>();

                void GenerateSolidBeam(Curve cIn, Curve cOut, double startZ, double bW)
                {
                    Curve bIn = cIn.DuplicateCurve(); bIn.Translate(new Vector3d(0, 0, startZ));
                    Curve bOut = cOut.DuplicateCurve(); bOut.Translate(new Vector3d(0, 0, startZ));
                    Curve tIn = cIn.DuplicateCurve(); tIn.Translate(new Vector3d(0, 0, startZ + bW));
                    Curve tOut = cOut.DuplicateCurve(); tOut.Translate(new Vector3d(0, 0, startZ + bW));
                    
                    List<Brep> faces = new List<Brep>();
                    
                    Brep[] bLoft = Brep.CreateFromLoft(new Curve[] { bIn, bOut }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                    if (bLoft != null && bLoft.Length > 0) faces.AddRange(bLoft);
                    
                    Brep[] tLoft = Brep.CreateFromLoft(new Curve[] { tIn, tOut }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                    if (tLoft != null && tLoft.Length > 0) faces.AddRange(tLoft);
                    
                    Extrusion iExt = Extrusion.Create(bIn, bW, false);
                    if (iExt != null) faces.Add(iExt.ToBrep());
                    
                    Extrusion oExt = Extrusion.Create(bOut, bW, false);
                    if (oExt != null) faces.Add(oExt.ToBrep());
                    
                    if (!cIn.IsClosed)
                    {
                        Line e1 = new Line(bIn.PointAtStart, bOut.PointAtStart);
                        Extrusion cap1 = Extrusion.Create(e1.ToNurbsCurve(), bW, false);
                        if (cap1 != null) faces.Add(cap1.ToBrep());
                        
                        Line e2 = new Line(bIn.PointAtEnd, bOut.PointAtEnd);
                        Extrusion cap2 = Extrusion.Create(e2.ToNurbsCurve(), bW, false);
                        if (cap2 != null) faces.Add(cap2.ToBrep());
                    }
                    
                    Brep[] joined = Brep.JoinBreps(faces, 0.01);
                    if (joined != null && joined.Length > 0)
                        foreach (Brep b in joined) outBeams.Append(new GH_Brep(b), path);
                    else
                        foreach (Brep b in faces) outBeams.Append(new GH_Brep(b), path); 
                }

                for (int i = 0; i < crvs.Count; i++)
                {
                    Curve crv = crvs[i];
                    if (crv == null) continue;

                    double h = (hts.Count > i) ? hts[i] : 4.0;
                    if (h <= 0.1) h = 4.0;
                    displayHeight = h; 
                    
                    double actualBeamW = Math.Min(BeamWidth, h);
                    double hIn = h - actualBeamW; 
                    double actualSill = Math.Min(SillHeight, hIn);
                    double actualDrop = Math.Min(HeaderDrop, hIn - actualSill);
                    double visionH = hIn - actualSill - actualDrop;

                    // --- THE AREA METHOD: FOOLPROOF OFFSET ---
                    Curve outCrv = null;
                    double depthToUse = Math.Abs(BeamDepth);

                    Curve[] offPos = crv.Offset(Plane.WorldXY, depthToUse, 0.1, CurveOffsetCornerStyle.Sharp);
                    Curve[] offNeg = crv.Offset(Plane.WorldXY, -depthToUse, 0.1, CurveOffsetCornerStyle.Sharp);

                    bool usePos = true;

                    if (crv.IsClosed)
                    {
                        double aPos = 0, aNeg = 0;
                        if (offPos != null && offPos.Length > 0 && offPos[0].IsClosed) 
                            aPos = AreaMassProperties.Compute(offPos[0])?.Area ?? 0;
                        if (offNeg != null && offNeg.Length > 0 && offNeg[0].IsClosed) 
                            aNeg = AreaMassProperties.Compute(offNeg[0])?.Area ?? 0;

                        if (aPos > aNeg) usePos = !FlipDir;
                        else usePos = FlipDir;
                    }
                    else
                    {
                        bool isCW = crv.ClosedCurveOrientation(Plane.WorldXY) == CurveOrientation.Clockwise;
                        usePos = isCW ? FlipDir : !FlipDir;
                    }

                    Curve[] selectedOffsets = usePos ? offPos : offNeg;
                    if (selectedOffsets != null && selectedOffsets.Length > 0)
                    {
                        outCrv = selectedOffsets[0];
                        
                        if (actualBeamW > 0 && depthToUse > 0)
                        {
                            GenerateSolidBeam(crv, outCrv, hIn, actualBeamW);
                            if (i == 0 && !RemoveBaseBeam) GenerateSolidBeam(crv, outCrv, 0.0, actualBeamW);
                        }
                    }

                    // --- COLUMNS & FINS ---
                    Curve[] segments = crv.DuplicateSegments();
                    if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

                    HashSet<string> placedCols = new HashSet<string>();

                    foreach (Curve seg in segments)
                    {
                        double length = seg.GetLength();
                        int divCount = Math.Max(1, (int)Math.Round(length / BayWidth));
                        double[] tParams = seg.DivideByCount(divCount, true);

                        if (tParams != null && tParams.Length >= 2)
                        {
                            // A. COLUMNS
                            foreach (double t in tParams)
                            {
                                Point3d pt = seg.PointAt(t);
                                Vector3d tan = seg.TangentAt(t);
                                tan.Unitize();
                                
                                string ptKey = $"{Math.Round(pt.X, 2)},{Math.Round(pt.Y, 2)},{Math.Round(tan.X, 1)},{Math.Round(tan.Y, 1)}";

                                if (placedCols.Add(ptKey))
                                {
                                    Vector3d outward;
                                    if (outCrv != null && outCrv.ClosestPoint(pt, out double tOut))
                                    {
                                        outward = outCrv.PointAt(tOut) - pt;
                                        outward.Unitize();
                                    }
                                    else 
                                    {
                                        outward = Vector3d.CrossProduct(tan, Vector3d.ZAxis);
                                        if (FlipDir) outward.Reverse();
                                    }

                                    Point3d p1 = pt - tan * (ColWidth / 2.0);
                                    Point3d p2 = pt + tan * (ColWidth / 2.0);
                                    Point3d p3 = p2 + outward * ColDepth;
                                    Point3d p4 = p1 + outward * ColDepth;

                                    Polyline rect;
                                    if (Vector3d.CrossProduct(tan, outward).Z > 0) {
                                        rect = new Polyline(new Point3d[] { p1, p2, p3, p4, p1 }); 
                                    } else {
                                        rect = new Polyline(new Point3d[] { p1, p4, p3, p2, p1 }); 
                                    }

                                    Extrusion colExt = Extrusion.Create(rect.ToNurbsCurve(), h, true); 
                                    if (colExt != null) { outCols.Append(new GH_Brep(colExt.ToBrep()), path); colCount++; }
                                }
                            }

                            // B. INNER FACADE & PLANAR FINS
                            for (int pIdx = 0; pIdx < tParams.Length - 1; pIdx++)
                            {
                                Curve bayCrv = seg.Trim(new Interval(tParams[pIdx], tParams[pIdx + 1]));
                                if (bayCrv == null) continue;

                                double bayLen = bayCrv.GetLength();

                                if (HasPanel)
                                {
                                    if (actualSill > 0)
                                    {
                                        Extrusion eSill = Extrusion.Create(bayCrv, actualSill, false);
                                        if (eSill != null) { outSills.Append(new GH_Brep(eSill.ToBrep()), path); solidArea += bayLen * actualSill; }
                                    }
                                    
                                    if (visionH > 0)
                                    {
                                        Curve vCrv = bayCrv.DuplicateCurve(); vCrv.Translate(new Vector3d(0, 0, actualSill));
                                        Extrusion eVis = Extrusion.Create(vCrv, visionH, false);
                                        if (eVis != null) { outGlass.Append(new GH_Brep(eVis.ToBrep()), path); glassArea += bayLen * visionH; }
                                    }
                                    
                                    if (actualDrop > 0)
                                    {
                                        Curve hCrv = bayCrv.DuplicateCurve(); hCrv.Translate(new Vector3d(0, 0, hIn - actualDrop));
                                        Extrusion eHead = Extrusion.Create(hCrv, actualDrop, false);
                                        if (eHead != null) { outHeaders.Append(new GH_Brep(eHead.ToBrep()), path); solidArea += bayLen * actualDrop; }
                                    }

                                    if (SubdivWidth > 0 && visionH > 0 && FinDepth > 0)
                                    {
                                        int subCount = Math.Max(1, (int)Math.Round(bayLen / SubdivWidth));
                                        if (subCount > 1)
                                        {
                                            double[] subParams = bayCrv.DivideByCount(subCount, true);
                                            if (subParams != null)
                                            {
                                                for (int s = 1; s < subParams.Length - 1; s++)
                                                {
                                                    Point3d pt = bayCrv.PointAt(subParams[s]);
                                                    Vector3d tan = bayCrv.TangentAt(subParams[s]);
                                                    
                                                    Vector3d outward;
                                                    if (outCrv != null && outCrv.ClosestPoint(pt, out double tOut)) {
                                                        outward = outCrv.PointAt(tOut) - pt;
                                                        outward.Unitize();
                                                    } else {
                                                        outward = Vector3d.CrossProduct(tan, Vector3d.ZAxis);
                                                        if (FlipDir) outward.Reverse();
                                                    }

                                                    Line finLine = new Line(pt, pt + outward * FinDepth);
                                                    Curve finProfile = finLine.ToNurbsCurve();
                                                    finProfile.Translate(new Vector3d(0, 0, actualSill)); 
                                                    
                                                    Extrusion finExt = Extrusion.Create(finProfile, visionH, false); 
                                                    if (finExt != null) { outFins.Append(new GH_Brep(finExt.ToBrep()), path); finCount++; }
                                                }
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

            DA.SetDataTree(0, outBeams);
            DA.SetDataTree(1, outCols);
            DA.SetDataTree(2, outGlass);
            DA.SetDataTree(3, outFins);
            DA.SetDataTree(4, outHeaders);
            DA.SetDataTree(5, outSills);

            if (glassArea > 0 || solidArea > 0 || colCount > 0)
            {
                this.Message = this.NickName + "\n" + string.Format(
                    "Time: {0} ms\nGrid: {1:0.0#}m x {2:0.0#}m\n---\nCols:    {3}\nFins:    {4}\nGlass:   {5:N0} SQM\nSolid:   {6:N0} SQM",
                    watch.ElapsedMilliseconds, BayWidth, displayHeight, colCount, finCount, glassArea, solidArea);
            }
            else
            {
                this.Message = this.NickName + "\n" + "Awaiting Data";
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Mod_Frames.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("14B022CD-93D3-4886-A480-167DFD9CDD33"); }
        }
    }
}
