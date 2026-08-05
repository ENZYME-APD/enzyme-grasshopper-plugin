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
		double BaseHeight,
		double TransomHeight,
		double MullionDepth,
		bool FlipMullions,
		List<string> HorizPattern,
		string VertPattern,
		double CanopyDepth,
		string CanopyPattern,
		bool FlipCanopy,
		bool ShowColumns,
		double ColumnOffset,
		ref object Glass,
		ref object SolidPanels,
		ref object HeaderPanels,
		ref object Mullions,
		ref object Canopy,
		ref object Columns)
    {


/*
FACADE MODULE 04: RETAIL STOREFRONT (V7.1 - C# OPTIMIZED)
================================================================================
Generates articulated storefronts with patterned solid/glass bays.
* UPDATED: C# Implementation for extreme performance.
* UPDATED: Full DataTree support for Bounds and Heights.
* FIXED: Inputs like BaseHeight or CanopyDepth safely accept 0.0 values.

INPUTS:
    Bounds        (Curve)  [Tree Access]
    Heights       (double) [Tree Access]
    BayWidth      (double) [Item Access]
    BaseHeight    (double) [Item Access]
    TransomHeight (double) [Item Access]
    CanopyDepth   (double) [Item Access]
    MullionDepth  (double) [Item Access]
    HorizPattern  (string) [List Access]
    VertPattern   (string) [Item Access]
    CanopyPattern (string) [Item Access]
    ShowColumns   (bool)   [Item Access]
    ColumnOffset  (double) [Item Access]
    FlipCanopy    (bool)   [Item Access]
    FlipMullions  (bool)   [Item Access]

OUTPUTS:
    Glass         (Brep)   [Tree Access]
    SolidPanels   (Brep)   [Tree Access]
    HeaderPanels  (Brep)   [Tree Access]
    Mullions      (Brep)   [Tree Access]
    Canopy        (Brep)   [Tree Access]
    Columns       (Curve)  [Tree Access]
================================================================================
*/

var watch = System.Diagnostics.Stopwatch.StartNew();

// Set Component Metadata
Component.Name = "Facade Module: Storefront";
Component.NickName = "Mod_Retail";
Component.Description = "Generates patterned storefronts with intelligent canopies and structure.";

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
                Extrusion bWall = Extrusion.Create(bayCrv, actualBaseH, false);
                if (bWall != null)
                {
                    outSolid.Add(bWall.ToBrep(), path);
                    solidArea += bayLen * actualBaseH;
                }
            }

            // 2. Middle Vision/Solid Band
            if (midH > 0)
            {
                Curve midCrv = bayCrv.DuplicateCurve();
                midCrv.Transform(Transform.Translation(new Vector3d(0, 0, actualBaseH)));
                Extrusion midWall = Extrusion.Create(midCrv, midH, false);
                if (midWall != null)
                {
                    if (isGlass)
                    {
                        outGlass.Add(midWall.ToBrep(), path);
                        glassArea += bayLen * midH;
                    }
                    else
                    {
                        outSolid.Add(midWall.ToBrep(), path);
                        solidArea += bayLen * midH;
                    }
                }
            }

            // 3. Header Band (Signage)
            if (headH > 0)
            {
                Curve headCrv = bayCrv.DuplicateCurve();
                headCrv.Transform(Transform.Translation(new Vector3d(0, 0, actualTransomH)));
                Extrusion headWall = Extrusion.Create(headCrv, headH, false);
                if (headWall != null)
                {
                    outHeaders.Add(headWall.ToBrep(), path);
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
                            Extrusion finExt = Extrusion.Create(finLine.ToNurbsCurve(), midH, false);
                            if (finExt != null) outMullions.Add(finExt.ToBrep(), path);

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
Glass = outGlass;
SolidPanels = outSolid;
HeaderPanels = outHeaders;
Mullions = outMullions;
Canopy = outCanopy;
Columns = outColumns;

// 6. Update UI
string vStr = string.Join("", vIndices);
string cStr = string.Join("", cPattern);

if (glassArea > 0 || solidArea > 0)
{
    string msg = string.Format(
        "MOD: STOREFRONT\nTime: {0} ms\nWall Matrix: [{1}]\nCanopy Rhythm: [{2}]\n---\nGlass: {3:N0} SQM\nSolid: {4:N0} SQM\nHead:  {5:N0} SQM",
        watch.ElapsedMilliseconds, vStr, cStr, glassArea, solidArea, headerArea);
        
    if (colCount > 0) msg += string.Format("\nCols:  {0}", colCount);
    
    Component.Message = msg;
}
else
{
    Component.Message = "Awaiting Data";
}


    }
}
