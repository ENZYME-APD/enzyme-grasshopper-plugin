// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Notes
    /* 
      Members:
        RhinoDoc RhinoDocument
        GH_Document GrasshopperDocument
        IGH_Component Component
        int Iteration

      Methods (Virtual & overridable):
        Print(string text)
        Print(string format, params object[] args)
        Reflect(object obj)
        Reflect(object obj, string method_name)
    */
    #endregion

    private void RunScript(
		DataTree<Curve> Bounds,
		DataTree<double> Heights,
		double BayWidth,
		double BeamWidth,
		double BeamDepth,
		double ColWidth,
		double ColDepth,
		bool HasPanel,
		double SillHeight,
		double HeaderDrop,
		double SubdivWidth,
		double FinDepth,
		bool FlipDir,
		bool RemoveBaseBeam,
		ref object Beams,
		ref object Columns,
		ref object Glass,
		ref object Fins,
		ref object HeaderPanels,
		ref object SillPanels)
    {



/*
FACADE MODULE 06: EXO-FRAMES (STRUCTURAL GRID)
================================================================================
Generates a deep external structural grid (Columns & Beams) with an optional 
recessed inner facade (Glass, Sill, Header, and subdivisions).
* FIXED: Uses the "Area Method" for closed curves. Offsets both ways, compares 
  areas, and definitively picks the Outward/Inward curve. 
* FIXED: Columns and Fins calculate their vectors by pointing directly at the 
  chosen Beam offset curve, guaranteeing 100% synchronization.
* FIXED: Column profiles use a dynamic Cross-Product check to ensure they are 
  ALWAYS drawn CCW so they extrude UPWARDS (+Z).

INPUTS:
    Bounds         (Curve)  [Tree Access]
    Heights        (double) [Tree Access]
    BayWidth       (double) [Item Access]
    BeamWidth      (double) [Item Access]
    BeamDepth      (double) [Item Access]
    ColWidth       (double) [Item Access]
    ColDepth       (double) [Item Access]
    HasPanel       (bool)   [Item Access]
    SillHeight     (double) [Item Access]
    HeaderDrop     (double) [Item Access]
    SubdivWidth    (double) [Item Access]
    FinDepth       (double) [Item Access]
    FlipDir        (bool)   [Item Access]
    RemoveBaseBeam (bool)   [Item Access]

OUTPUTS:
    Beams          (Brep)   [Tree Access]
    Columns        (Brep)   [Tree Access]
    Glass          (Brep)   [Tree Access]
    Fins           (Brep)   [Tree Access]
    HeaderPanels   (Brep)   [Tree Access]
    SillPanels     (Brep)   [Tree Access]
================================================================================
*/

var watch = System.Diagnostics.Stopwatch.StartNew();

// Set Component Metadata
Component.Name = "Facade Module: Frames";
Component.NickName = "Mod_Frames";
Component.Description = "Deep structural exoskeleton grid utilizing the robust Area-Offset Method.";

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

// 2. Initialize Output Trees
DataTree<Brep> outBeams = new DataTree<Brep>();
DataTree<Brep> outCols = new DataTree<Brep>();
DataTree<Brep> outGlass = new DataTree<Brep>();
DataTree<Brep> outFins = new DataTree<Brep>();
DataTree<Brep> outHeaders = new DataTree<Brep>();
DataTree<Brep> outSills = new DataTree<Brep>();

double glassArea = 0, solidArea = 0;
int colCount = 0, finCount = 0;
double displayHeight = 4.0;

// 3. Main Processing Loop
for (int p = 0; p < Bounds.BranchCount; p++)
{
    GH_Path path = Bounds.Path(p);
    List<Curve> crvs = Bounds.Branch(path);
    List<double> hts = Heights.PathExists(path) ? Heights.Branch(path) : new List<double>();

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
            foreach (Brep b in joined) outBeams.Add(b, path);
        else
            foreach (Brep b in faces) outBeams.Add(b, path); 
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

            // Pick the larger area for Outward. FlipDir reverses it.
            if (aPos > aNeg) usePos = !FlipDir;
            else usePos = FlipDir;
        }
        else
        {
            // Open curve fallback
            bool isCW = crv.ClosedCurveOrientation(Plane.WorldXY) == CurveOrientation.Clockwise;
            usePos = isCW ? FlipDir : !FlipDir;
        }

        Curve[] selectedOffsets = usePos ? offPos : offNeg;
        if (selectedOffsets != null && selectedOffsets.Length > 0)
        {
            outCrv = selectedOffsets[0];
            
            // Generate Beams
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
                        // ULTIMATE SYNC: The column outward vector simply points to the closest point on the Beam curve.
                        Vector3d outward;
                        if (outCrv != null && outCrv.ClosestPoint(pt, out double tOut))
                        {
                            outward = outCrv.PointAt(tOut) - pt;
                            outward.Unitize();
                        }
                        else 
                        {
                            // Safety Fallback
                            outward = Vector3d.CrossProduct(tan, Vector3d.ZAxis);
                            if (FlipDir) outward.Reverse();
                        }

                        Point3d p1 = pt - tan * (ColWidth / 2.0);
                        Point3d p2 = pt + tan * (ColWidth / 2.0);
                        Point3d p3 = p2 + outward * ColDepth;
                        Point3d p4 = p1 + outward * ColDepth;

                        // MATHEMATICAL CCW CHECK: Forces extrusion to always go UP (+Z)
                        Polyline rect;
                        if (Vector3d.CrossProduct(tan, outward).Z > 0) {
                            rect = new Polyline(new Point3d[] { p1, p2, p3, p4, p1 }); 
                        } else {
                            rect = new Polyline(new Point3d[] { p1, p4, p3, p2, p1 }); 
                        }

                        Extrusion colExt = Extrusion.Create(rect.ToNurbsCurve(), h, true); 
                        if (colExt != null) { outCols.Add(colExt.ToBrep(), path); colCount++; }
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
                            if (eSill != null) { outSills.Add(eSill.ToBrep(), path); solidArea += bayLen * actualSill; }
                        }
                        
                        if (visionH > 0)
                        {
                            Curve vCrv = bayCrv.DuplicateCurve(); vCrv.Translate(new Vector3d(0, 0, actualSill));
                            Extrusion eVis = Extrusion.Create(vCrv, visionH, false);
                            if (eVis != null) { outGlass.Add(eVis.ToBrep(), path); glassArea += bayLen * visionH; }
                        }
                        
                        if (actualDrop > 0)
                        {
                            Curve hCrv = bayCrv.DuplicateCurve(); hCrv.Translate(new Vector3d(0, 0, hIn - actualDrop));
                            Extrusion eHead = Extrusion.Create(hCrv, actualDrop, false);
                            if (eHead != null) { outHeaders.Add(eHead.ToBrep(), path); solidArea += bayLen * actualDrop; }
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
                                        
                                        // SYNCHRONIZED FIN VECTOR
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
                                        if (finExt != null) { outFins.Add(finExt.ToBrep(), path); finCount++; }
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

// 4. Outputs
Beams = outBeams;
Columns = outCols;
Glass = outGlass;
Fins = outFins;
HeaderPanels = outHeaders;
SillPanels = outSills;

// 5. Update UI
if (glassArea > 0 || solidArea > 0 || colCount > 0)
{
    Component.Message = string.Format(
        "MOD: FRAMES\nTime: {0} ms\nGrid: {1:0.0#}m x {2:0.0#}m\n---\nCols:    {3}\nFins:    {4}\nGlass:   {5:N0} SQM\nSolid:   {6:N0} SQM",
        watch.ElapsedMilliseconds, BayWidth, displayHeight, colCount, finCount, glassArea, solidArea);
}
else
{
    Component.Message = "Awaiting Data";
}



    }
}
