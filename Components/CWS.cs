using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Enzyme.Components
{
    public class CWSComponent : GH_Component
    {
        public CWSComponent()
          : base("CurtainWallSystem", "CWS",
              "Generates a detailed LOD 300 curtain wall system from base curves.",
              Enzyme.Utils.TabInfo.TabName, "Facade")
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
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 4, new string[]{"Start", "Middle", "End"}, new string[]{"\"Start\"", "\"Middle\"", "\"End\""}, 300, -40);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 2.0, 0.5, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 2.0, 0.3, 330, 40);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(50, 50, 50), 220, -128);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(230, 230, 230), 220, -53);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(50, 50, 50), 220, 22);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 3, System.Drawing.Color.FromArgb(150, 200, 255), 220, 97);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("BaseCurves", "BC", "Planar boundary curves to extrude.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "H", "Floor-to-floor heights corresponding to curves.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GridVertical", "GV", "Spacing pattern for vertical mullions (e.g., [1.5, 2.0]).", GH_ParamAccess.list, 1.5);
            pManager.AddNumberParameter("GridHorizontal", "GH", "Spacing pattern for horizontal transoms (e.g., [1.0, 2.0]).", GH_ParamAccess.list, 1.5);
            pManager.AddTextParameter("Align", "A", "Grid justification ('Start', 'Middle', 'End').", GH_ParamAccess.item, "Start");
            pManager.AddNumberParameter("VertExt", "VE", "Fallback extrusion depth for vertical mullions.", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("HorExt", "HE", "Fallback extrusion depth for horizontal transoms.", GH_ParamAccess.item, 0.3);
            pManager.AddCurveParameter("DetailVertical", "DV", "Optional 2D profile to sweep for vertical mullions.", GH_ParamAccess.item);
            pManager.AddCurveParameter("DetailHorizontal", "DH", "Optional 2D profile to sweep for horizontal transoms.", GH_ParamAccess.item);
            pManager.AddCurveParameter("DetailCorner", "DC", "Optional 2D profile to sweep for corner mullions.", GH_ParamAccess.item);
            pManager.AddNumberParameter("CurvedGridVertical", "CGV", "Optional alternative vertical spacing for curved segments.", GH_ParamAccess.list);

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
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Mullions", "M", "Generated vertical mullions.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Transoms", "T", "Generated horizontal transoms.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("CornerMullions", "CM", "Vertical mullions generated specifically at sharp vertices.", GH_ParamAccess.tree);
            pManager.AddBrepParameter("GlassWall", "GW", "Uncapped extruded glass surfaces.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // 1. START TIMER
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            string version = "v1.1";

            GH_Structure<GH_Curve> baseCurvesTree;
            if (!DA.GetDataTree(0, out baseCurvesTree)) return;

            GH_Structure<GH_Number> heightsTree;
            DA.GetDataTree(1, out heightsTree);

            List<double> gridVertical = new List<double>();
            if (!DA.GetDataList(2, gridVertical) || gridVertical.Count == 0) gridVertical = new List<double> { 1.5 };

            List<double> gridHorizontal = new List<double>();
            if (!DA.GetDataList(3, gridHorizontal) || gridHorizontal.Count == 0) gridHorizontal = new List<double> { 1.5 };

            string align = "Start";
            DA.GetData(4, ref align);

            double vertExt = 0.5;
            DA.GetData(5, ref vertExt);

            double horExt = 0.3;
            DA.GetData(6, ref horExt);

            Curve detailVertical = null;
            DA.GetData(7, ref detailVertical);

            Curve detailHorizontal = null;
            DA.GetData(8, ref detailHorizontal);

            Curve detailCorner = null;
            DA.GetData(9, ref detailCorner);

            List<double> curvedGridVertical = new List<double>();
            DA.GetDataList(10, curvedGridVertical);

            // Validate inputs
            if (baseCurvesTree.DataCount == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "BaseCurves input is empty or invalid.");
                return;
            }
            if (gridVertical.Any(v => v <= 0))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "GridVertical must contain positive values.");
                return;
            }
            if (gridHorizontal.Any(h => h <= 0))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "GridHorizontal must contain positive values.");
                return;
            }
            if (!new[] { "Start", "Middle", "End" }.Contains(align, StringComparer.OrdinalIgnoreCase))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Align must be 'Start', 'Middle', or 'End'.");
                return;
            }
            if (vertExt < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "VertExt cannot be negative.");
                return;
            }
            if (horExt < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "HorExt cannot be negative.");
                return;
            }

            align = align.ToLower();

            GH_Structure<GH_Brep> outMullions = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outTransoms = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outCornerMullions = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outGlassWall = new GH_Structure<GH_Brep>();

            double totalGlassArea = 0.0;
            int totalMullions = 0;
            int totalTransoms = 0;

            for (int p = 0; p < baseCurvesTree.PathCount; p++)
            {
                GH_Path path = baseCurvesTree.Paths[p];
                var branch = baseCurvesTree.get_Branch(path);
                
                var htsBranch = (heightsTree != null && heightsTree.PathExists(path)) ? heightsTree.get_Branch(path) : null;
                var hts = htsBranch != null ? htsBranch.Cast<GH_Number>().ToList() : new List<GH_Number>();

                int curveIndex = 0;
                foreach (GH_Curve ghCurve in branch.Cast<GH_Curve>())
                {
                    if (ghCurve == null || ghCurve.Value == null || !ghCurve.Value.IsValid || !ghCurve.Value.IsPlanar())
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Skipping invalid or non-planar base curve.");
                        curveIndex++;
                        continue;
                    }
                    Curve baseCurve = ghCurve.Value;

                    double height = 3.0;
                    if (hts.Count > 0)
                    {
                        height = hts[Math.Min(curveIndex, hts.Count - 1)].Value;
                    }
                    if (height <= 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Heights must contain positive values.");
                        curveIndex++;
                        continue;
                    }

                    // Extrude base curve to create glass wall
                    Vector3d extrudeDir = new Vector3d(0, 0, height);
                    Surface glassSurface = Surface.CreateExtrusion(baseCurve, extrudeDir);
                    if (glassSurface != null)
                    {
                        Brep glassWall = glassSurface.ToBrep();
                        if (glassWall != null)
                        {
                            outGlassWall.Append(new GH_Brep(glassWall), path);
                            var amp = AreaMassProperties.Compute(glassWall);
                            if (amp != null)
                            {
                                totalGlassArea += amp.Area;
                            }
                        }
                    }

                    Curve[] segments = baseCurve.DuplicateSegments();
                    if (segments == null || segments.Length == 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Base curve has no segments.");
                        curveIndex++;
                        continue;
                    }

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

                    for (int i = 0; i < segments.Length; i++)
                    {
                        Curve segment = segments[i];
                        if (!segment.IsValid) continue;

                        bool isCurved = !segment.IsLinear(RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        List<double> currentGridVertical = isCurved && curvedGridVertical != null && curvedGridVertical.Count > 0 && curvedGridVertical.All(v => v > 0)
                            ? curvedGridVertical
                            : gridVertical;

                        double segmentLength = segment.GetLength();
                        List<double> verticalDivisions = ComputeDivisions(segmentLength, currentGridVertical, align);

                        for (int j = 0; j < verticalDivisions.Count; j++)
                        {
                            double normalizedT = verticalDivisions[j] / segmentLength;
                            Point3d pt = segment.PointAtNormalizedLength(normalizedT);
                            double param = segment.Domain.T0 + normalizedT * (segment.Domain.T1 - segment.Domain.T0);
                            Vector3d tangent = segment.TangentAt(param);
                            Vector3d normal = Vector3d.CrossProduct(tangent, Vector3d.ZAxis);
                            normal.Unitize();

                            Point3d topPt = pt + new Vector3d(0, 0, height);
                            Line mullionLine = new Line(pt, topPt);
                            Curve mullionCurve = mullionLine.ToNurbsCurve();

                            Brep mullionBrep = CreateMullion(mullionCurve, normal, detailVertical, vertExt);
                            if (mullionBrep != null)
                            {
                                outMullions.Append(new GH_Brep(mullionBrep), path);
                                totalMullions++;
                            }
                        }

                        List<double> horizontalDivisions = ComputeDivisions(height, gridHorizontal, "start");

                        foreach (double hVal in horizontalDivisions)
                        {
                            if (hVal <= 0 || hVal >= height) continue;

                            Curve transomCurve = segment.DuplicateCurve();
                            transomCurve.Translate(new Vector3d(0, 0, hVal));
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

                            Brep transomBrep = CreateMullion(transomCurve, avgNormal, detailHorizontal, horExt);
                            if (transomBrep != null)
                            {
                                outTransoms.Append(new GH_Brep(transomBrep), path);
                                totalTransoms++;
                            }
                        }

                        List<Point3d> curvatureTransitionPoints = FindCurvatureTransitions(segment);
                        foreach (var pt in curvatureTransitionPoints)
                        {
                            if (!baseCornerPoints.Any(cp => cp.DistanceTo(pt) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance))
                            {
                                baseCornerPoints.Add(pt);
                            }
                        }
                    }

                    foreach (var cornerPt in baseCornerPoints)
                    {
                        Point3d topPt = cornerPt + new Vector3d(0, 0, height);
                        Line cornerLine = new Line(cornerPt, topPt);
                        Curve cornerCurve = cornerLine.ToNurbsCurve();

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
                            normal = Vector3d.XAxis;
                        }

                        Brep cornerBrep = CreateMullion(cornerCurve, normal, detailCorner, vertExt);
                        if (cornerBrep != null)
                        {
                            outCornerMullions.Append(new GH_Brep(cornerBrep), path);
                            totalMullions++;
                        }
                    }
                    curveIndex++;
                }
            }

            DA.SetDataTree(0, outMullions);
            DA.SetDataTree(1, outTransoms);
            DA.SetDataTree(2, outCornerMullions);
            DA.SetDataTree(3, outGlassWall);

            sw.Stop();

            if (totalGlassArea > 0)
            {
                this.Message = $"CWS {version}\nTime: {sw.ElapsedMilliseconds} ms\n---\nMullions: {totalMullions}\nTransoms: {totalTransoms}\nGlass: {totalGlassArea:F1} SQM";
            }
            else
            {
                this.Message = $"CWS {version}\nAwaiting Data";
            }
        }

        private List<Point3d> FindCurvatureTransitions(Curve curve)
        {
            List<Point3d> transitionPoints = new List<Point3d>();
            if (curve == null || !curve.IsValid) return transitionPoints;

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

            int sampleCount = 100;
            double[] parameters = curve.DivideByCount(sampleCount, true);
            if (parameters == null || parameters.Length < 2) return transitionPoints;

            double previousCurvature = curve.CurvatureAt(parameters[0]).Length;
            bool previousIsStraight = Math.Abs(previousCurvature) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            for (int i = 1; i < parameters.Length; i++)
            {
                double t = parameters[i];
                Vector3d curvatureVector = curve.CurvatureAt(t);
                double curvature = curvatureVector.Length;
                bool isStraight = Math.Abs(curvature) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

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
                SweepOneRail sweep = new SweepOneRail();
                Curve profileCopy = profile.DuplicateCurve();
                Point3d railStart = rail.PointAtStart;
                Vector3d railTangent = rail.TangentAtStart;
                Vector3d profilePlaneNormal = Vector3d.CrossProduct(railTangent, normal);
                if (profilePlaneNormal.Length < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                {
                    profilePlaneNormal = Vector3d.ZAxis;
                }
                Plane profilePlane = new Plane(railStart, profilePlaneNormal, normal);
                profileCopy.Transform(Transform.PlaneToPlane(new Plane(Point3d.Origin, Vector3d.ZAxis), profilePlane));

                Brep[] sweepBreps = sweep.PerformSweep(rail, profileCopy);
                if (sweepBreps != null && sweepBreps.Length > 0)
                {
                    return sweepBreps[0];
                }
            }

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

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("CWS.png");
            }
        }

        public override Guid ComponentGuid => new Guid("0F5F2B51-CD3B-4E38-B7D9-1B86A4F2A143");
    }
}
