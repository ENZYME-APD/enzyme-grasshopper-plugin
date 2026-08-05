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
		double SillHeight,
		double HeaderDrop,
		double MullionDepth,
		bool FullHeightMullions,
		bool FlipDir,
		ref object Glass,
		ref object Spandrels,
		ref object Mullions)
    {


/*
FACADE MODULE 02: HORIZONTAL SPANDRELS (V5 - C# OPTIMIZED)
================================================================================
Generates horizontal solid bands (Sill and Header) and Vision Glass.
* UPDATED: C# Implementation for extreme performance.
* UPDATED: Full DataTree support for Bounds and Heights.
* FIXED: Null-safe handling of Extrusion generations and HashSet deduplication.

INPUTS:
    Bounds             (Curve)  [Tree Access]
    Heights            (double) [Tree Access]
    SillHeight         (double) [Item Access]
    HeaderDrop         (double) [Item Access]
    BayWidth           (double) [Item Access]
    MullionDepth       (double) [Item Access]
    FlipDir            (bool)   [Item Access]
    FullHeightMullions (bool)   [Item Access]

OUTPUTS:
    Glass              (Brep)   [Tree Access]
    Spandrels          (Brep)   [Tree Access]
    Mullions           (Brep)   [Tree Access]
================================================================================
*/

var watch = System.Diagnostics.Stopwatch.StartNew();

// Set Component Metadata
Component.Name = "Facade Module: Spandrels";
Component.NickName = "Mod_Spandrel";
Component.Description = "Generates solid horizontal spandrel bands, vision glass, and variable-depth mullions.";

// 1. Safe Defaults
if (BayWidth <= 0.0) BayWidth = 1.5;
if (MullionDepth <= 0.0) MullionDepth = 0.15;
if (SillHeight < 0.0) SillHeight = 0.0; // Allowed to be 0
if (HeaderDrop < 0.0) HeaderDrop = 0.0; // Allowed to be 0

// 2. Initialize Output Trees
DataTree<Brep> outGlass = new DataTree<Brep>();
DataTree<Brep> outSpandrel = new DataTree<Brep>();
DataTree<Brep> outMullions = new DataTree<Brep>();

double glassArea = 0, spandrelArea = 0;
int mullionCount = 0;

// 3. Main Processing Loop (Tree Support)
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
        double crvLen = crv.GetLength();

        // --- 1. BOTTOM SPANDREL (SILL) ---
        if (SillHeight > 0)
        {
            Extrusion extSill = Extrusion.Create(crv, SillHeight, false);
            if (extSill != null)
            {
                outSpandrel.Add(extSill.ToBrep(), path);
                spandrelArea += crvLen * SillHeight;
            }
        }

        // --- 2. VISION GLASS ---
        double visionH = h - SillHeight - HeaderDrop;
        if (visionH > 0)
        {
            Curve crvVision = crv.DuplicateCurve();
            crvVision.Transform(Transform.Translation(new Vector3d(0, 0, SillHeight)));
            Extrusion extVision = Extrusion.Create(crvVision, visionH, false);
            if (extVision != null)
            {
                outGlass.Add(extVision.ToBrep(), path);
                glassArea += crvLen * visionH;
            }
        }

        // --- 3. TOP SPANDREL (HEADER) ---
        if (HeaderDrop > 0)
        {
            Curve crvTop = crv.DuplicateCurve();
            crvTop.Transform(Transform.Translation(new Vector3d(0, 0, h - HeaderDrop)));
            Extrusion extTop = Extrusion.Create(crvTop, HeaderDrop, false);
            if (extTop != null)
            {
                outSpandrel.Add(extTop.ToBrep(), path);
                spandrelArea += crvLen * HeaderDrop;
            }
        }

        // --- 4. MULLION LOGIC ---
        double mStartZ = FullHeightMullions ? 0.0 : SillHeight;
        double mHeight = FullHeightMullions ? h : visionH;

        if (mHeight > 0 && MullionDepth > 0)
        {
            Curve[] segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0) segments = new Curve[] { crv };
            
            HashSet<string> placedPts = new HashSet<string>();

            foreach (Curve seg in segments)
            {
                double segLen = seg.GetLength();
                int divCount = Math.Max(1, (int)Math.Round(segLen / BayWidth));

                double[] tParams = seg.DivideByCount(divCount, true);
                if (tParams != null)
                {
                    foreach (double t in tParams)
                    {
                        Point3d pt = seg.PointAt(t);
                        string ptKey = $"{Math.Round(pt.X, 3)},{Math.Round(pt.Y, 3)},{Math.Round(pt.Z, 3)}";

                        // HashSet automatically prevents double-placing at segment corners
                        if (!placedPts.Add(ptKey)) continue;

                        Vector3d tan = seg.TangentAt(t);
                        Vector3d normal = new Vector3d(-tan.Y, tan.X, 0);
                        normal.Unitize();
                        
                        if (FlipDir) normal.Reverse();

                        // Generate mullion line shifted to the correct starting height with variable depth
                        Line finLine = new Line(pt, pt + (normal * MullionDepth));
                        finLine.Transform(Transform.Translation(new Vector3d(0, 0, mStartZ)));
                        Extrusion finExt = Extrusion.Create(finLine.ToNurbsCurve(), mHeight, false);

                        if (finExt != null)
                        {
                            outMullions.Add(finExt.ToBrep(), path);
                            mullionCount++;
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
Spandrels = outSpandrel;
Mullions = outMullions;

// 6. Update UI
if (glassArea > 0 || spandrelArea > 0)
{
    string msg = string.Format(
        "MOD: HORIZONTAL BANDS\nTime: {0} ms\nSill: {1:0.0#}m | Drop: {2:0.0#}m\n---\nGlass: {3:N0} SQM\nSolid: {4:N0} SQM",
        watch.ElapsedMilliseconds, SillHeight, HeaderDrop, glassArea, spandrelArea);
        
    if (mullionCount > 0) msg += string.Format("\nMullions: {0}", mullionCount);
    
    Component.Message = msg;
}
else
{
    Component.Message = "Awaiting Data";
}



    }
}
