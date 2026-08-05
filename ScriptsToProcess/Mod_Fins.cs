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
		double Spacing,
		double Depth,
		double HeaderDrop,
		List<string> HorizPattern,
		string VertPattern,
		bool FlipDir,
		ref object Glass,
		ref object Spandrels,
		ref object Fins)
    {
   

/*
FACADE MODULE 01: VERTICAL FINS (V3.1 - C# OPTIMIZED)
================================================================================
Generates vertical fins with a patterned rhythm, vision glass, and spandrels.
* UPDATED: C# Implementation for extreme performance.
* UPDATED: DataTree support added for Bounds and Heights.
* ADDED: Fast Math area approximation instead of Brep physics.

INPUTS:
    Bounds       (Curve)  [Tree Access]
    Heights      (double) [Tree Access]
    Spacing      (double) [Item Access]
    Depth        (double) [Item Access]
    HeaderDrop   (double) [Item Access]
    HorizPattern (string) [List Access]
    VertPattern  (string) [Item Access]
    FlipDir      (bool)   [Item Access]

OUTPUTS:
    Glass        (Brep)   [Tree Access]
    Spandrels    (Brep)   [Tree Access]
    Fins         (Brep)   [Tree Access]
================================================================================
*/

var watch = System.Diagnostics.Stopwatch.StartNew();

// Set Component Metadata
Component.Name = "Facade Module: Vertical Fins";
Component.NickName = "Mod_Fins";
Component.Description = "Generates patterned vertical fins, spandrels, and vision glass.";

// 1. Safe Defaults
if (Spacing <= 0.0) Spacing = 1.5;
if (Depth <= 0.0) Depth = 0.3;
if (HeaderDrop < 0.0) HeaderDrop = 0.0;

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

// 3. Initialize Output Trees
DataTree<Brep> outGlass = new DataTree<Brep>();
DataTree<Brep> outSpandrel = new DataTree<Brep>();
DataTree<Brep> outFins = new DataTree<Brep>();

double glassArea = 0, spandrelArea = 0;
int finCount = 0;

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
        double crvLen = crv.GetLength();

        // --- INNER WALL: GLASS & SPANDREL ---
        double visionH = h - HeaderDrop;
        if (visionH > 0)
        {
            Extrusion glassExt = Extrusion.Create(crv, visionH, false);
            if (glassExt != null)
            {
                outGlass.Add(glassExt.ToBrep(), path);
                glassArea += crvLen * visionH;
            }
        }

        if (HeaderDrop > 0)
        {
            Curve crvTop = crv.DuplicateCurve();
            crvTop.Transform(Transform.Translation(new Vector3d(0, 0, visionH)));
            Extrusion spanExt = Extrusion.Create(crvTop, HeaderDrop, false);
            if (spanExt != null)
            {
                outSpandrel.Add(spanExt.ToBrep(), path);
                spandrelArea += crvLen * HeaderDrop;
            }
        }

        // --- PATTERNED FINS ---
        int vIdx = vIndices[i % vIndices.Count];
        string activeHStr = cleanHPats[vIdx % cleanHPats.Count];
        List<char> pattern = activeHStr.ToList();

        Curve[] segments = crv.DuplicateSegments();
        if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

        // HashSet to deduplicate corner points
        HashSet<string> placedPts = new HashSet<string>();
        int finIndex = 0;

        foreach (Curve seg in segments)
        {
            double segLen = seg.GetLength();
            int divCount = Math.Max(1, (int)Math.Round(segLen / Spacing));

            double[] tParams = seg.DivideByCount(divCount, true);
            if (tParams != null)
            {
                foreach (double t in tParams)
                {
                    Point3d pt = seg.PointAt(t);
                    string ptKey = $"{Math.Round(pt.X, 3)},{Math.Round(pt.Y, 3)},{Math.Round(pt.Z, 3)}";

                    // Prevent double-placing at segment corners
                    if (!placedPts.Add(ptKey)) continue;

                    // Check if the matrix allows a fin here
                    if (pattern[finIndex % pattern.Count] == '1')
                    {
                        Vector3d tan = seg.TangentAt(t);
                        Vector3d normal = new Vector3d(-tan.Y, tan.X, 0);
                        normal.Unitize();

                        if (FlipDir) normal.Reverse();

                        // Extrude full height over the spandrel
                        Line finLine = new Line(pt, pt + (normal * Depth));
                        Extrusion finExt = Extrusion.Create(finLine.ToNurbsCurve(), h, false);
                        
                        if (finExt != null)
                        {
                            outFins.Add(finExt.ToBrep(), path);
                            finCount++;
                        }
                    }
                    finIndex++;
                }
            }
        }
    }
}

watch.Stop();

// 5. Outputs
Glass = outGlass;
Spandrels = outSpandrel;
Fins = outFins;

// 6. Update UI
string vStr = string.Join("", vIndices);

if (glassArea > 0 || spandrelArea > 0)
{
    Component.Message = string.Format(
        "MOD: VERTICAL FINS\nTime: {0} ms\nDrop: {1:0.0#}m\n---\nFins:    {2}\nGlass:   {3:N0} SQM\nSpandrel:{4:N0} SQM",
        watch.ElapsedMilliseconds, HeaderDrop, finCount, glassArea, spandrelArea);
}
else
{
    Component.Message = "Awaiting Data";
}




    }
}
