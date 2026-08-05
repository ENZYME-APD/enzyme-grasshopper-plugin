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
		double Depth,
		double RailHeight,
		double SillHeight,
		double HeaderDrop,
		List<string> HorizPattern,
		string VertPattern,
		string BalconyPattern,
		bool FlipDir,
		bool FirstBalcony,
		bool CornerWindow,
		double CornerOffset,
		bool SegLength,
		ref object Slabs,
		ref object Railings,
		ref object Glass,
		ref object SolidPanels,
		ref object HeaderPanels,
		ref object CornerPanels)
    {
    

/*
FACADE MODULE 03: CONTINUOUS BALCONY (V6.1 - UI UPDATED)
================================================================================
Generates continuous balconies while subdividing the inner wall into bays.
* UPDATED: UI Message stripped of matrices, now tracks FirstBalcony and CornerOffset.
* UPDATED: Corner logic auto-triggers if CornerOffset > 0.
* ADDED: SegLength bypass. If true, segments smaller than 2 bays become corners.
* ADDED: CornerWindow logic applies sill/header generation to corners.

INPUTS:
    Bounds         (Curve)  [Tree Access]
    Heights        (double) [Tree Access]
    BayWidth       (double) [Item Access]
    Depth          (double) [Item Access]
    RailHeight     (double) [Item Access]
    SillHeight     (double) [Item Access]
    HeaderDrop     (double) [Item Access]
    HorizPattern   (string) [List Access]
    VertPattern    (string) [Item Access]
    BalconyPattern (string) [Item Access]
    FlipDir        (bool)   [Item Access]
    FirstBalcony   (bool)   [Item Access]
    CornerOffset   (double) [Item Access]
    CornerWindow   (bool)   [Item Access]
    SegLength      (bool)   [Item Access]

OUTPUTS:
    Slabs          (Brep)   [Tree Access]
    Railings       (Brep)   [Tree Access]
    Glass          (Brep)   [Tree Access]
    SolidPanels    (Brep)   [Tree Access]
    HeaderPanels   (Brep)   [Tree Access]
    CornerPanels   (Brep)   [Tree Access]
================================================================================
*/

var watch = System.Diagnostics.Stopwatch.StartNew();

// Set Component Metadata
Component.Name = "Facade Module: Balcony";
Component.NickName = "Mod_Balcony";
Component.Description = "Continuous balconies with 2D wall patterns and intelligent corners.";

// 1. Safe Defaults
if (BayWidth <= 0.0) BayWidth = 3.0;
if (Depth <= 0.0) Depth = 1.5;
if (RailHeight <= 0.0) RailHeight = 1.1;

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

string rawBPat = (BalconyPattern ?? "1").Trim();
List<char> balcPattern = rawBPat.Where(c => c == '0' || c == '1').ToList();
if (balcPattern.Count == 0) balcPattern.Add('1');

// 3. Initialize Output Trees
DataTree<Brep> outSlabs = new DataTree<Brep>();
DataTree<Brep> outRails = new DataTree<Brep>();
DataTree<Brep> outGlass = new DataTree<Brep>();
DataTree<Brep> outSolid = new DataTree<Brep>();
DataTree<Brep> outHeaders = new DataTree<Brep>();
DataTree<Brep> outCorners = new DataTree<Brep>();

double slabArea = 0, glassArea = 0, solidArea = 0, headerArea = 0, cornerArea = 0;
int floorCount = 0;

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

        // --- BALCONY LOGIC ---
        bool hasBalcony = balcPattern[i % balcPattern.Count] == '1';
        
        // Ground floor balcony toggle (False = Remove)
        if (!FirstBalcony && i == 0) hasBalcony = false;

        if (hasBalcony)
        {
            Curve[] offsets = crv.Offset(Plane.WorldXY, dist, 0.1, CurveOffsetCornerStyle.Sharp);
            if (offsets != null && offsets.Length > 0)
            {
                Curve offCrv = offsets[0];
                Brep[] lofts = Brep.CreateFromLoft(new Curve[] { crv, offCrv }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                
                if (lofts != null && lofts.Length > 0)
                {
                    outSlabs.Add(lofts[0], path);
                    slabArea += crv.GetLength() * Math.Abs(Depth); // Fast O(1) area math
                }

                Extrusion extRail = Extrusion.Create(offCrv, RailHeight, false);
                if (extRail != null) outRails.Add(extRail.ToBrep(), path);
            }
            floorCount++;
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

            // FEATURE 3: Fillet / Short Segment Bypass
            if (SegLength && length < (BayWidth * 2.0))
            {
                // Segment is too short to subdivide normally; treat entire segment as a corner panel
                cornerCrvs.Add(seg);
            }
            // FEATURE 2: Standard Corner Isolation (Auto-triggers if offset > 0)
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
                // No corners requested, or segment too short for requested offset
                middleCrvs.Add(seg);
            }
        }

        // Subdivide Middle Segments into Standard Bays
        foreach (Curve middle in middleCrvs)
        {
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
                bayCrvs.Add(middle); // Fallback
            }
        }

        // --- GENERATE CORNER GEOMETRY ---
        foreach (Curve cCrv in cornerCrvs)
        {
            double cLen = cCrv.GetLength();

            // FEATURE 1: Corner Window Logic
            if (CornerWindow)
            {
                // 1. Sill (Kickplate) routed to continuous Solid stream
                if (SillHeight > 0)
                {
                    Extrusion extSill = Extrusion.Create(cCrv, SillHeight, false);
                    if (extSill != null)
                    {
                        outSolid.Add(extSill.ToBrep(), path);
                        solidArea += cLen * SillHeight;
                    }
                }

                // 2. Vision Glass routed to isolated Corner stream
                double visionH = h - SillHeight - HeaderDrop;
                if (visionH > 0)
                {
                    Curve cGlass = cCrv.DuplicateCurve();
                    cGlass.Transform(Transform.Translation(new Vector3d(0, 0, SillHeight)));
                    Extrusion extGlass = Extrusion.Create(cGlass, visionH, false);
                    if (extGlass != null)
                    {
                        outCorners.Add(extGlass.ToBrep(), path);
                        cornerArea += cLen * visionH;
                    }
                }

                // 3. Header Drop routed to continuous Header stream
                if (HeaderDrop > 0)
                {
                    Curve cTop = cCrv.DuplicateCurve();
                    cTop.Transform(Transform.Translation(new Vector3d(0, 0, h - HeaderDrop)));
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
                // Solid Column Corner routed to isolated Corner stream
                Extrusion extCorner = Extrusion.Create(cCrv, h, false);
                if (extCorner != null)
                {
                    outCorners.Add(extCorner.ToBrep(), path);
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
                // 1. Sill (Kickplate)
                if (SillHeight > 0)
                {
                    Extrusion extSill = Extrusion.Create(bayCrv, SillHeight, false);
                    if (extSill != null)
                    {
                        outSolid.Add(extSill.ToBrep(), path);
                        solidArea += bayLen * SillHeight;
                    }
                }

                // 2. Vision Glass
                double visionH = h - SillHeight - HeaderDrop;
                if (visionH > 0)
                {
                    Curve cGlass = bayCrv.DuplicateCurve();
                    cGlass.Transform(Transform.Translation(new Vector3d(0, 0, SillHeight)));
                    Extrusion extGlass = Extrusion.Create(cGlass, visionH, false);
                    if (extGlass != null)
                    {
                        outGlass.Add(extGlass.ToBrep(), path);
                        glassArea += bayLen * visionH;
                    }
                }

                // 3. Header Drop (Top Spandrel)
                if (HeaderDrop > 0)
                {
                    Curve cTop = bayCrv.DuplicateCurve();
                    cTop.Transform(Transform.Translation(new Vector3d(0, 0, h - HeaderDrop)));
                    Extrusion extTop = Extrusion.Create(cTop, HeaderDrop, false);
                    if (extTop != null)
                    {
                        outHeaders.Add(extTop.ToBrep(), path);
                        headerArea += bayLen * HeaderDrop;
                    }
                }
            }
            else
            {
                // '0' - Full Solid Wall
                Extrusion extSolid = Extrusion.Create(bayCrv, h, false);
                if (extSolid != null)
                {
                    outSolid.Add(extSolid.ToBrep(), path);
                    solidArea += bayLen * h;
                }
            }
        }
    }

}

watch.Stop();

// 5. Outputs
Slabs = outSlabs;
Railings = outRails;
Glass = outGlass;
SolidPanels = outSolid;
HeaderPanels = outHeaders;
CornerPanels = outCorners;

// 6. Update UI
string fbStatus = FirstBalcony ? "On" : "Off";
string coStatus = CornerOffset > 0 ? CornerOffset.ToString("0.0#") + "m" : "Off";

if (glassArea > 0 || solidArea > 0 || cornerArea > 0)
{
    Component.Message = string.Format(
        "MOD: CONTINUOUS BALCONY\nTime: {0} ms\nFirst Balcony: {1}\nCorner Offset: {2}\n---\nGlass:   {3:N0} SQM\nSolid:   {4:N0} SQM\nHead:    {5:N0} SQM\nCorner:  {6:N0} SQM\nTerrace: {7:N0} SQM",
        watch.ElapsedMilliseconds, fbStatus, coStatus, glassArea, solidArea, headerArea, cornerArea, slabArea);
}
else
{
    Component.Message = "Awaiting Data";
}

    
    }
}
