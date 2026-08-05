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
		double HeaderDrop,
		List<string> HorizPattern,
		string VertPattern,
		bool MergeAdjacent,
		bool FlipDir,
		bool FirstBalcony,
		bool CornerWindow,
		double CornerOffset,
		bool SegLength,
		ref object Slabs,
		ref object Railings,
		ref object Partitions,
		ref object Glass,
		ref object SolidPanels,
		ref object HeaderPanels,
		ref object CornerPanels)
    {


/*
FACADE MODULE 05: RHYTHMIC BALCONIES (V6 - C# OPTIMIZED + CORNERS)
================================================================================
Subdivides the facade into bays and generates staggered balconies. 
* UPDATED: Integrated Corner isolation (Auto-triggers if CornerOffset > 0).
* ADDED: FirstBalcony toggles the rhythmic terrace on the ground level.
* ADDED: SegLength bypass (short segments turn into corners).
* ADDED: CornerWindow logic applies sill/header to corner panels.

INPUTS:
    Bounds        (Curve)  [Tree Access]
    Heights       (double) [Tree Access]
    BayWidth      (double) [Item Access]
    Depth         (double) [Item Access]
    RailHeight    (double) [Item Access]
    HeaderDrop    (double) [Item Access]
    HorizPattern  (string) [List Access]
    VertPattern   (string) [Item Access]
    MergeAdjacent (bool)   [Item Access]
    FlipDir       (bool)   [Item Access]
    FirstBalcony  (bool)   [Item Access]
    CornerOffset  (double) [Item Access]
    CornerWindow  (bool)   [Item Access]
    SegLength     (bool)   [Item Access]

OUTPUTS:
    Slabs         (Brep)   [Tree Access]
    Railings      (Brep)   [Tree Access]
    Partitions    (Brep)   [Tree Access]
    Glass         (Brep)   [Tree Access]
    SolidPanels   (Brep)   [Tree Access]
    HeaderPanels  (Brep)   [Tree Access]
    CornerPanels  (Brep)   [Tree Access]
================================================================================
*/

var watch = System.Diagnostics.Stopwatch.StartNew();

// Set Component Metadata
Component.Name = "Facade Module: Rhythmic Balconies";
Component.NickName = "Mod_Rhythm_Balc";
Component.Description = "2D patterned balconies with merge logic, corner isolation, and fast math.";

// 1. Safe Defaults
if (BayWidth <= 0.0) BayWidth = 3.0;
if (Depth <= 0.0) Depth = 1.5;
if (RailHeight <= 0.0) RailHeight = 1.1;
if (HeaderDrop < 0.0) HeaderDrop = 0.0;
if (CornerOffset < 0.0) CornerOffset = 0.0;

// 2. Parse Patterns
if (HorizPattern == null || HorizPattern.Count == 0) HorizPattern = new List<string> { "10" };
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
Slabs = outSlabs;
Railings = outRails;
Partitions = outParts;
Glass = outGlass;
SolidPanels = outSolid;
HeaderPanels = outHeaders;
CornerPanels = outCorners;

// 6. Update UI
string vStr = string.Join("", vIndices);
string fbStatus = FirstBalcony ? "On" : "Off";
string coStatus = CornerOffset > 0 ? CornerOffset.ToString("0.0#") + "m" : "Off";

if (glassArea > 0 || solidArea > 0 || cornerArea > 0)
{
    Component.Message = string.Format(
        "MOD: RHYTHMIC BALCONIES\nTime: {0} ms\nGrnd Balc: {1}\nCorner Off: {2}\n---\nGlass:  {3:N0} SQM\nSolid:  {4:N0} SQM\nHead:   {5:N0} SQM\nCorner: {6:N0} SQM\nBalc:   {7}",
        watch.ElapsedMilliseconds, fbStatus, coStatus, glassArea, solidArea, headerArea, cornerArea, balconyCount);
}
else
{
    Component.Message = "Awaiting Data";
}



    }
}
