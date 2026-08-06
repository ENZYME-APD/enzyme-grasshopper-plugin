/*
================================================================================
CURTAIN WALL SYSTEM (CWS) - v1.1
================================================================================
DESCRIPTION:
Generates a detailed LOD 300 curtain wall system from base curves. Computes 
vertical and horizontal mullions, corner mullions (handling C2 discontinuity), 
and outputs glass panels. Includes performance timing and UI metrics.

INPUTS:
    BaseCurves         (List<Curve>)  : Planar boundary curves to extrude.
    Heights            (List<double>) : Floor-to-floor heights corresponding to curves.
    GridVertical       (List<double>) : Spacing pattern for vertical mullions (e.g., [1.5, 2.0]).
    GridHorizontal     (List<double>) : Spacing pattern for horizontal transoms (e.g., [1.0, 2.0]).
    Align              (string)       : Grid justification ("Start", "Middle", "End").
    VertExt            (double)       : Fallback extrusion depth for vertical mullions.
    HorExt             (double)       : Fallback extrusion depth for horizontal transoms.
    DetailVertical     (Curve)        : Optional 2D profile to sweep for vertical mullions.
    DetailHorizontal   (Curve)        : Optional 2D profile to sweep for horizontal transoms.
    DetailCorner       (Curve)        : Optional 2D profile to sweep for corner mullions.
    CurvedGridVertical (List<double>) : Optional alternative vertical spacing for curved segments.

OUTPUTS:
    Mullions       (List<Brep>) : Generated vertical mullions.
    Transoms       (List<Brep>) : Generated horizontal transoms.
    CornerMullions (List<Brep>) : Vertical mullions generated specifically at sharp vertices.
    GlassWall      (List<Brep>) : Uncapped extruded glass surfaces.

MAINTENANCE NOTES:
    - Profile Sweeps: If custom profiles (Detail...) are provided, the script attempts 
      a SweepOneRail. If the sweep fails or no profile is provided, it safely falls 
      back to a normal extrusion (using VertExt/HorExt) to guarantee geometry generation.
    - Curvature: Uses Continuity.C2_locus_continuous to mathematically isolate sharp 
      corners on polycurves to ensure corners always receive a dedicated mullion.
================================================================================
*/

using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		List<Curve> BaseCurves,
		List<double> Heights,
		List<double> GridVertical,
		List<double> GridHorizontal,
		string Align,
		double VertExt,
		double HorExt,
		Curve DetailVertical,
		Curve DetailHorizontal,
		Curve DetailCorner,
		List<double> CurvedGridVertical,
		ref object Mullions,
		ref object Transoms,
		ref object CornerMullions,
		ref object GlassWall)
    {
        // 1. START TIMER
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        string version = "v1.1";
        Component.Name = "CurtainWallSystem";
        Component.NickName = "CWS";

        // Set default values if inputs are not provided
        if (Component.Params.Input[1].VolatileData.DataCount == 0)
        {
            Heights = new List<double> { 3.0 }; 
        }
        if (Component.Params.Input[2].VolatileData.DataCount == 0)
        {
            GridVertical = new List<double> { 1.5 }; 
        }
        if (Component.Params.Input[3].VolatileData.DataCount == 0)
        {
            GridHorizontal = new List<double> { 1.5 }; 
        }
        if (Component.Params.Input[4].VolatileData.DataCount == 0)
        {
            Align = "Start"; 
        }
        if (Component.Params.Input[5].VolatileData.DataCount == 0)
        {
            VertExt = 0.5; 
        }
        if (Component.Params.Input[6].VolatileData.DataCount == 0)
        {
            HorExt = 0.3; 
        }

        // Validate inputs
        if (BaseCurves == null || BaseCurves.Count == 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "BaseCurves input is empty or invalid.");
            return;
        }
        if (Heights == null || Heights.Count == 0 || Heights.Any(h => h <= 0))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Heights must contain positive values.");
            return;
        }
        if (GridVertical == null || GridVertical.Count == 0 || GridVertical.Any(v => v <= 0))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "GridVertical must contain positive values.");
            return;
        }
        if (GridHorizontal == null || GridHorizontal.Count == 0 || GridHorizontal.Any(h => h <= 0))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "GridHorizontal must contain positive values.");
            return;
        }
        if (!new[] { "Start", "Middle", "End" }.Contains(Align, StringComparer.OrdinalIgnoreCase))
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Align must be 'Start', 'Middle', or 'End'.");
            return;
        }
        if (VertExt < 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "VertExt cannot be negative.");
            return;
        }
        if (HorExt < 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "HorExt cannot be negative.");
            return;
        }

        // Normalize Align input
        Align = Align.ToLower();

        // Initialize output lists & metrics
        List<Brep> mullionBreps = new List<Brep>();
        List<Brep> transomBreps = new List<Brep>();
        List<Brep> cornerMullionBreps = new List<Brep>();
        List<Brep> glassWallBreps = new List<Brep>();
        
        double totalGlassArea = 0.0;

        // Process each base curve
        for (int curveIndex = 0; curveIndex < BaseCurves.Count; curveIndex++)
        {
            var baseCurve = BaseCurves[curveIndex];
            if (baseCurve == null || !baseCurve.IsValid || !baseCurve.IsPlanar())
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Skipping invalid or non-planar base curve.");
                continue;
            }

            // Get the height for this curve
            double height = Heights[Math.Min(curveIndex, Heights.Count - 1)]; 

            // Extrude base curve to create glass wall
            Vector3d extrudeDir = new Vector3d(0, 0, height);
            Surface glassSurface = Surface.CreateExtrusion(baseCurve, extrudeDir);
            if (glassSurface != null)
            {
                Brep glassWall = glassSurface.ToBrep();
                if (glassWall != null)
                {
                    glassWallBreps.Add(glassWall);
                    
                    // ACCUMULATE GLASS AREA
                    var amp = AreaMassProperties.Compute(glassWall);
                    if (amp != null)
                    {
                        totalGlassArea += amp.Area;
                    }
                }
            }

            // Explode the curve into segments
            Curve[] segments = baseCurve.DuplicateSegments();
            if (segments == null || segments.Length == 0)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Base curve has no segments.");
                continue;
            }

            // Get discontinuity points (corners) for the entire base curve using C2 continuity
            List<Point3d> baseCornerPoints = new List<Point3d>();
            double discontinuityT = baseCurve.Domain.T0;
            baseCornerPoints.Add(baseCurve.PointAt(discontinuityT));
            while (baseCurve.GetNextDiscontinuity(Continuity.C2_locus_continuous, discontinuityT, baseCurve.Domain.T1, out discontinuityT))
            {
                baseCornerPoints.Add(baseCurve.PointAt(discontinuityT));
            }
            if (!baseCornerPoints.Last().Equals(baseCurve.PointAtEnd))
            {
                baseCornerPoints.Add(baseCurve.PointAtEnd);
            }

            // Process each segment
            for (int i = 0; i < segments.Length; i++)
            {
                Curve segment = segments[i];
                if (!segment.IsValid)
                {
                    continue;
                }

                // Determine if the segment is curved
                bool isCurved = !segment.IsLinear(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                List<double> currentGridVertical = isCurved && CurvedGridVertical != null && CurvedGridVertical.Count > 0 && CurvedGridVertical.All(v => v > 0)
                    ? CurvedGridVertical
                    : GridVertical;

                // Compute vertical subdivisions along the segment, applying Align
                double segmentLength = segment.GetLength();
                List<double> verticalDivisions = ComputeDivisions(segmentLength, currentGridVertical, Align);

                // Create vertical mullions
                for (int j = 0; j < verticalDivisions.Count; j++)
                {
                    double normalizedT = verticalDivisions[j] / segmentLength;
                    Point3d pt = segment.PointAtNormalizedLength(normalizedT);
                    double param = segment.Domain.T0 + normalizedT * (segment.Domain.T1 - segment.Domain.T0);
                    Vector3d tangent = segment.TangentAt(param);
                    Vector3d normal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                    normal.Unitize();

                    // Create vertical line for mullion
                    Point3d topPt = pt + new Vector3d(0, 0, height);
                    Line mullionLine = new Line(pt, topPt);
                    Curve mullionCurve = mullionLine.ToNurbsCurve();

                    // Create mullion geometry
                    Brep mullionBrep = CreateMullion(mullionCurve, normal, DetailVertical, VertExt);
                    if (mullionBrep != null)
                    {
                        mullionBreps.Add(mullionBrep);
                    }
                }

                // Compute horizontal subdivisions, always starting from the bottom (height 0)
                List<double> horizontalDivisions = ComputeDivisions(height, GridHorizontal, "start"); 

                // Create horizontal transoms for this segment
                foreach (double h in horizontalDivisions)
                {
                    if (h <= 0 || h >= height) continue;

                    // Offset the segment to the height
                    Curve transomCurve = segment.DuplicateCurve();
                    transomCurve.Translate(new Vector3d(0, 0, h));
                    Vector3d avgNormal = Vector3d.Zero;
                    for (double tNorm = 0; tNorm <= 1.0; tNorm += 0.1)
                    {
                        double param = transomCurve.Domain.T0 + tNorm * (transomCurve.Domain.T1 - transomCurve.Domain.T0);
                        Vector3d tangent = transomCurve.TangentAt(param);
                        Vector3d normal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                        normal.Unitize();
                        avgNormal += normal;
                    }
                    avgNormal.Unitize();

                    // Create transom geometry
                    Brep transomBrep = CreateMullion(transomCurve, avgNormal, DetailHorizontal, HorExt);
                    if (transomBrep != null)
                    {
                        transomBreps.Add(transomBrep);
                    }
                }

                // Find curvature transitions within this segment
                List<Point3d> curvatureTransitionPoints = FindCurvatureTransitions(segment);
                foreach (var pt in curvatureTransitionPoints)
                {
                    if (!baseCornerPoints.Any(cp => cp.DistanceTo(pt) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
                    {
                        baseCornerPoints.Add(pt);
                    }
                }
            }

            // Create corner mullions at all corner points (discontinuities + curvature transitions)
            foreach (var cornerPt in baseCornerPoints)
            {
                Point3d topPt = cornerPt + new Vector3d(0, 0, height);
                Line cornerLine = new Line(cornerPt, topPt);
                Curve cornerCurve = cornerLine.ToNurbsCurve();

                // Use average normal from adjacent segments
                Vector3d normal = Vector3d.Zero;
                int adjacentCount = 0;
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i].PointAtStart.DistanceTo(cornerPt) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance ||
                        segments[i].PointAtEnd.DistanceTo(cornerPt) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                    {
                        double tSegment = segments[i].PointAtStart.DistanceTo(cornerPt) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance ? 0 : 1;
                        double param = segments[i].Domain.T0 + tSegment * (segments[i].Domain.T1 - segments[i].Domain.T0);
                        Vector3d tangent = segments[i].TangentAt(param);
                        Vector3d segNormal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                        segNormal.Unitize();
                        normal += segNormal;
                        adjacentCount++;
                    }
                }
                if (adjacentCount > 0)
                {
                    normal /= adjacentCount;
                    normal.Unitize();
                }
                else
                {
                    normal = Vector3d.XAxis; // Fallback
                }

                // Create corner mullion geometry
                Brep cornerBrep = CreateMullion(cornerCurve, normal, DetailCorner, VertExt);
                if (cornerBrep != null)
                {
                    cornerMullionBreps.Add(cornerBrep);
                }
            }
        }

        // Set outputs
        Mullions = mullionBreps;
        Transoms = transomBreps;
        CornerMullions = cornerMullionBreps;
        GlassWall = glassWallBreps;
        
        // 2. STOP TIMER & FORMAT UI MESSAGE
        sw.Stop();
        int totalMullions = mullionBreps.Count + cornerMullionBreps.Count;
        int totalTransoms = transomBreps.Count;

        if (totalGlassArea > 0)
        {
            Component.Message = $"CWS {version}\nTime: {sw.ElapsedMilliseconds} ms\n---\nMullions: {totalMullions}\nTransoms: {totalTransoms}\nGlass: {totalGlassArea:F1} SQM";
        }
        else
        {
            Component.Message = $"CWS {version}\nAwaiting Data";
        }
    }

    private List<Point3d> FindCurvatureTransitions(Curve curve)
    {
        List<Point3d> transitionPoints = new List<Point3d>();
        if (curve == null || !curve.IsValid) return transitionPoints;

        // Check for kinks (sharp changes in direction)
        if (curve.SpanCount > 1)
        {
            for (int i = 0; i < curve.SpanCount; i++)
            {
                double t = curve.SpanDomain(i).T1;
                if (t < curve.Domain.T1)
                {
                    Point3d pt = curve.PointAt(t);
                    transitionPoints.Add(pt);
                }
            }
        }

        // Sample the curve to detect curvature changes
        int sampleCount = 100; // Adjust for precision
        double[] parameters = curve.DivideByCount(sampleCount, true);
        if (parameters == null || parameters.Length < 2) return transitionPoints;

        // Evaluate curvature at each sample point
        double previousCurvature = curve.CurvatureAt(parameters[0]).Length;
        bool previousIsStraight = Math.Abs(previousCurvature) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

        for (int i = 1; i < parameters.Length; i++)
        {
            double t = parameters[i];
            Vector3d curvatureVector = curve.CurvatureAt(t);
            double curvature = curvatureVector.Length;
            bool isStraight = Math.Abs(curvature) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // Detect transition: straight to curved, curved to straight, or significant curvature change
            if (previousIsStraight != isStraight || 
                (!previousIsStraight && !isStraight && Math.Abs(curvature - previousCurvature) > 0.1 * Math.Max(curvature, previousCurvature)))
            {
                Point3d pt = curve.PointAt(t);
                if (!transitionPoints.Any(p => p.DistanceTo(pt) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
                {
                    transitionPoints.Add(pt);
                }
            }

            previousCurvature = curvature;
            previousIsStraight = isStraight;
        }

        return transitionPoints;
    }

    private List<double> ComputeDivisions(double totalLength, List<double> pattern, string align)
    {
        List<double> divisions = new List<double>();
        double patternLength = pattern.Sum();
        int fullPatterns = (int)(totalLength / patternLength);
        double remainder = totalLength - (fullPatterns * patternLength);

        double offset = 0;
        if (align == "middle")
        {
            offset = remainder / 2.0;
        }
        else if (align == "end")
        {
            offset = remainder;
        }

        double currentPos = offset;
        for (int i = 0; i < fullPatterns; i++)
        {
            double posInPattern = 0;
            foreach (double segmentLength in pattern)
            {
                double divisionPos = currentPos + posInPattern;
                if (divisionPos > 0 && divisionPos < totalLength)
                {
                    divisions.Add(divisionPos);
                }
                posInPattern += segmentLength;
            }
            currentPos += patternLength;
        }

        // Add remaining divisions if needed
        if (remainder > 0)
        {
            double posInPattern = 0;
            foreach (double segmentLength in pattern)
            {
                double divisionPos = currentPos + posInPattern;
                if (divisionPos < totalLength)
                {
                    divisions.Add(divisionPos);
                }
                posInPattern += segmentLength;
                if (posInPattern >= remainder) break;
            }
        }

        divisions.Sort();
        return divisions;
    }

    private Brep CreateMullion(Curve rail, Vector3d normal, Curve profile, double extrusionDepth)
    {
        if (rail == null || !rail.IsValid) return null;

        if (profile != null && profile.IsValid && profile.IsClosed)
        {
            // Sweep the profile along the rail
            SweepOneRail sweep = new SweepOneRail();

            // Create a copy of the profile to avoid modifying the original
            Curve profileCopy = profile.DuplicateCurve();

            // Position the profile at the start of THIS rail
            Point3d railStart = rail.PointAtStart;
            Vector3d railTangent = rail.TangentAtStart;
            Vector3d profilePlaneNormal = Vector3d.CrossProduct(railTangent, normal);
            if (profilePlaneNormal.Length < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
            {
                profilePlaneNormal = Vector3d.ZAxis; // Fallback for vertical rails
            }
            Plane profilePlane = new Plane(railStart, profilePlaneNormal, normal);
            profileCopy.Transform(Transform.PlaneToPlane(new Plane(Point3d.Origin, Vector3d.ZAxis), profilePlane));

            Brep[] sweepBreps = sweep.PerformSweep(rail, profileCopy);
            if (sweepBreps != null && sweepBreps.Length > 0)
            {
                return sweepBreps[0];
            }
        }

        // Fallback to extrusion if no profile or sweep fails
        if (extrusionDepth <= 0) return null;

        Curve extrudedCurve = rail.DuplicateCurve();
        Vector3d extrudeDir = normal * extrusionDepth;
        Surface extrudeSurface = Surface.CreateExtrusion(extrudedCurve, extrudeDir);
        if (extrudeSurface != null)
        {
            return extrudeSurface.ToBrep();
        }
        return null;
    }
}