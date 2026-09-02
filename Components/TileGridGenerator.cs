using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Enzyme;

namespace Enzyme.Components
{
    public class TileGridGenerator : GH_Component
    {
        public TileGridGenerator()
            : base("Grid Pattern Generator and Trimmer", "GridPattern",
                "Generates a grid pattern (rectangular, hexagonal, triangular) within a boundary and trims cells to fit.",
                "Enzyme", "Facade")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("GridPattern.png");
            }
        }

        public override Guid ComponentGuid => new Guid("3E7B9F2A-C4D8-4A1E-B5F3-8D2C6E0A9B4F");
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 2, 
                    new string[] { "Rectangular", "Offset Rectangular", "Hexagonal", "Triangular" }, 
                    new string[] { "\"rectangular\"", "\"offset_rectangular\"", "\"hexagonal\"", "\"triangular\"" }, 
                    330, -60);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 2.0, 1.0, 330, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 2.0, 1.0, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", 220, -45);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", 220, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "curve", 220, 45);
            }
        }

                protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Base Geometry", "Base", "Planar Surface, Brep, or closed Curve to fill.", GH_ParamAccess.item);
            pManager.AddPointParameter("Setout Point", "Setout", "Optional origin point for the grid alignment. If not supplied, the centroid is used.", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Grid Type", "Grid Type", "Grid type: rectangular, offset_rectangular, hexagonal, triangular", GH_ParamAccess.item, "rectangular");
            pManager.AddNumberParameter("Cell Width", "X Dim", "Cell width", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Cell Height", "Y Dim", "Cell height", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Grout Width", "Grout", "Width of the grout joint between tiles. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddVectorParameter("Direction", "Dir", "Optional vector to align the grid's X-axis. Projects to the base plane.", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("Rotation", "Rot", "Optional rotation angle (in degrees) applied after alignment.", GH_ParamAccess.item, 0.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Tile Status", "Tile Status", "0 = full, 1 = trimmed", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Full Count", "Full Count", "Number of full tiles", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Trimmed Count", "Trim Count", "Number of trimmed tiles", GH_ParamAccess.item);
            pManager.AddCurveParameter("All Tiles", "All Tiles", "All grid tiles (trimmed and full)", GH_ParamAccess.list);
            pManager.AddCurveParameter("Full Tiles", "Full Tiles", "Tiles fully inside the boundary", GH_ParamAccess.list);
            pManager.AddCurveParameter("Trimmed Tiles", "Trimmed Tiles", "Tiles partially inside the boundary", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            GeometryBase baseGeom = null;
            Point3d setoutPt = Point3d.Unset;
            double x_dim = 1.0, y_dim = 1.0, grout = 0.0, rot = 0.0;
            string gridType = "rectangular";
            Rhino.Geometry.Vector3d dir = Rhino.Geometry.Vector3d.Unset;

            if (!DA.GetData(0, ref baseGeom)) return;
            DA.GetData(1, ref setoutPt);
            DA.GetData(2, ref gridType);
            DA.GetData(3, ref x_dim);
            DA.GetData(4, ref y_dim);
            DA.GetData(5, ref grout);
            DA.GetData(6, ref dir);
            DA.GetData(7, ref rot);
            if (grout < 0) grout = 0;

            Curve boundary = null;
            Plane originPlane = Plane.WorldXY;

            if (baseGeom is Curve crv)
            {
                boundary = crv;
                if (!boundary.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base curve must be closed.");
                    return;
                }
                if (!boundary.TryGetPlane(out originPlane))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base curve must be planar.");
                    return;
                }
                
                var amp = Rhino.Geometry.AreaMassProperties.Compute(boundary);
                Point3d centroid = amp != null ? amp.Centroid : boundary.PointAtStart;
                originPlane.Origin = centroid;
                if (setoutPt.IsValid) originPlane.Origin = originPlane.ClosestPoint(setoutPt);
            }
            else if (baseGeom is Surface srf)
            {
                if (!srf.IsPlanar())
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base surface must be planar.");
                    return;
                }
                var brep = Brep.CreateFromSurface(srf);
                Curve[] naked = brep.DuplicateNakedEdgeCurves(true, false);
                if (naked != null && naked.Length > 0)
                {
                    Curve[] joined = Curve.JoinCurves(naked);
                    if (joined != null && joined.Length > 0) boundary = joined[0];
                }
                
                double u = srf.Domain(0).Mid;
                double v = srf.Domain(1).Mid;
                Vector3d normal = srf.NormalAt(u, v);
                
                var amp = Rhino.Geometry.AreaMassProperties.Compute(brep);
                Point3d centroid = amp != null ? amp.Centroid : srf.PointAt(u, v);
                originPlane = new Plane(centroid, normal);
                if (setoutPt.IsValid) originPlane.Origin = originPlane.ClosestPoint(setoutPt);
            }
            else if (baseGeom is Brep b)
            {
                if (b.Faces.Count != 1 || !b.Faces[0].IsPlanar())
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base Brep must be a single planar face.");
                    return;
                }
                Curve[] naked = b.DuplicateNakedEdgeCurves(true, false);
                if (naked != null && naked.Length > 0)
                {
                    Curve[] joined = Curve.JoinCurves(naked);
                    if (joined != null && joined.Length > 0) boundary = joined[0];
                }
                
                Surface bsrf = b.Faces[0].UnderlyingSurface();
                double u = bsrf.Domain(0).Mid;
                double v = bsrf.Domain(1).Mid;
                Vector3d normal = bsrf.NormalAt(u, v);
                
                var amp = Rhino.Geometry.AreaMassProperties.Compute(b);
                Point3d centroid = amp != null ? amp.Centroid : bsrf.PointAt(u, v);
                originPlane = new Plane(centroid, normal);
                if (setoutPt.IsValid) originPlane.Origin = originPlane.ClosestPoint(setoutPt);
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base Geometry must be a Curve, Surface, or Brep.");
                return;
            }

            if (boundary == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not extract a valid boundary.");
                return;
            }

            if (dir.IsValid && !dir.IsZero)
            {
                Rhino.Geometry.Vector3d projDir = originPlane.ClosestPoint(originPlane.Origin + dir) - originPlane.Origin;
                if (projDir.Length > 1e-6)
                {
                    projDir.Unitize();
                    Rhino.Geometry.Vector3d yAxis = Rhino.Geometry.Vector3d.CrossProduct(originPlane.ZAxis, projDir);
                    originPlane = new Plane(originPlane.Origin, projDir, yAxis);
                }
            }

            if (rot != 0.0)
            {
                originPlane.Rotate(rot * Math.PI / 180.0, originPlane.ZAxis);
            }

            List<Curve> allTiles = new List<Curve>();
            List<int> panelStatus = new List<int>();
            int fullCount = 0;
            int trimCount = 0;
            List<Curve> fullTiles = new List<Curve>();
            List<Curve> trimmedTiles = new List<Curve>();

            Transform toLocal = Transform.PlaneToPlane(originPlane, Plane.WorldXY);
            Transform toWorld = Transform.PlaneToPlane(Plane.WorldXY, originPlane);

            Curve localBoundary = boundary.DuplicateCurve();
            localBoundary.Transform(toLocal);

            BoundingBox bbox = localBoundary.GetBoundingBox(true);

            List<GridCell> cells = GenerateCells(bbox, x_dim, y_dim, gridType.ToLower(), grout);

            cells.RemoveAll(c => c == null || c.Curve == null);
            foreach (GridCell cell in cells)
            {
                cell.Transform(toWorld);
                List<Point3d> testPoints = cell.GetTestPoints();

                bool[] pointsInside = testPoints.Select(pt => PointContainmentTest(pt, boundary, originPlane)).ToArray();
                bool allInside = pointsInside.All(p => p);
                bool anyInside = pointsInside.Any(p => p);

                CurveIntersections intersections = Intersection.CurveCurve(cell.Curve, boundary, 0.001, 0.001);
                bool hasIntersection = intersections != null && intersections.Count > 0;

                if (allInside)
                {
                    allTiles.Add(cell.Curve);
                    fullTiles.Add(cell.Curve);
                    panelStatus.Add(0);
                    fullCount++;
                }
                else if (anyInside || hasIntersection)
                {
                    Curve[] intersectCurves = Curve.CreateBooleanIntersection(cell.Curve, boundary, 0.001);
                    if (intersectCurves != null && intersectCurves.Length > 0)
                    {
                        foreach (Curve trimmedCurve in intersectCurves)
                        {
                            allTiles.Add(trimmedCurve);
                            trimmedTiles.Add(trimmedCurve);
                            panelStatus.Add(1);
                            trimCount++;
                        }
                    }
                }
            }

            stopwatch.Stop();
            double executionTime = stopwatch.Elapsed.TotalSeconds;

            DA.SetDataList(0, panelStatus);
            DA.SetData(1, fullCount);
            DA.SetData(2, trimCount);
            DA.SetDataList(3, allTiles);
            DA.SetDataList(4, fullTiles);
            DA.SetDataList(5, trimmedTiles);

            string capGridType = gridType.Length > 0 ? char.ToUpper(gridType[0]) + gridType.Substring(1).ToLower() : gridType;
            Message = $"{capGridType} Grid";
            Message += $"\n{fullCount} complete | {trimCount} trimmed";
            Message += $"\nTime: {executionTime:F3}s";
        }

        private class GridCell
        {
            public Curve Curve { get; set; }
            public Point3d Center { get; set; }
            public List<Point3d> Corners { get; set; } = new List<Point3d>();

            public List<Point3d> GetTestPoints()
            {
                List<Point3d> pts = new List<Point3d>(Corners);
                if (Center.IsValid) pts.Add(Center);
                return pts;
            }

            public void Transform(Transform xform)
            {
                if (Curve != null) Curve.Transform(xform);
                Center = new Point3d(Center);
                Point3d c = Center;
                c.Transform(xform);
                Center = c;
                for (int i = 0; i < Corners.Count; i++)
                {
                    Point3d p = Corners[i];
                    p.Transform(xform);
                    Corners[i] = p;
                }
            }
        }

        private List<GridCell> GenerateCells(BoundingBox bbox, double cellWidth, double cellHeight, string gridType, double grout)
        {
            List<GridCell> cells = new List<GridCell>();

            if (gridType == "rectangular" || gridType == "offset_rectangular")
            {
                double pitchX = cellWidth + grout;
                double pitchY = cellHeight + grout;
                double startX = Math.Floor((bbox.Min.X - pitchX) / pitchX) * pitchX;
                double endX = bbox.Max.X + pitchX;
                double startY = Math.Floor((bbox.Min.Y - pitchY) / pitchY) * pitchY;
                double endY = bbox.Max.Y + pitchY;
                
                double x = startX;
                while (x <= endX)
                {
                    double y = startY;
                    int rowIndex = (int)Math.Round((y - startY) / pitchY);

                    while (y <= endY)
                    {
                        double offset = (gridType == "offset_rectangular" && rowIndex % 2 == 1) ? 0.5 * pitchX : 0;
                        cells.Add(CreateRectangularCell(x, y, offset, pitchX, pitchY, grout));
                        y += pitchY;
                        rowIndex++;
                    }
                    x += pitchX;
                }
            }
            else if (gridType == "hexagonal")
            {
                double hSpacing = cellWidth * 3.0 / 4.0;
                double vSpacing = cellWidth * Math.Sqrt(3) / 2.0;
                double x = Math.Floor(bbox.Min.X / hSpacing) * hSpacing;
                int rowCount = 0;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / vSpacing) * vSpacing;
                    double offset = (rowCount % 2 != 0) ? vSpacing / 2.0 : 0;
                    while (y < bbox.Max.Y)
                    {
                        cells.Add(CreateHexagonalCell(x, y + offset, cellWidth, grout));
                        y += vSpacing;
                    }
                    rowCount++;
                    x += hSpacing;
                }
            }
            else if (gridType == "triangular")
            {
                double pitchW = cellWidth + Math.Sqrt(3.0) * grout;
                double pitchH = pitchW * Math.Sqrt(3) / 2.0;
                double startY = Math.Floor((bbox.Min.Y - pitchH) / pitchH) * pitchH;
                double endY = bbox.Max.Y + pitchH;
                double startX = Math.Floor((bbox.Min.X - pitchW) / pitchW) * pitchW;
                double endX = bbox.Max.X + pitchW;
                
                double y = startY;
                int row = 0;
                while (y <= endY)
                {
                    double x = startX + (row % 2 == 0 ? 0 : pitchW / 2.0);
                    while (x <= endX)
                    {
                        cells.Add(CreateTriangularCell(x, y, false, pitchW, pitchH, grout));
                        cells.Add(CreateTriangularCell(x, y, true, pitchW, pitchH, grout));
                        x += pitchW;
                    }
                    y += pitchH;
                    row++;
                }
            }

            return cells;
        }

        private GridCell CreateRectangularCell(double x, double y, double offset, double cellWidth, double cellHeight, double grout)
        {
            GridCell cell = new GridCell();
            double halfGrout = grout / 2.0;
            double adjustedX = x + offset + halfGrout;
            double adjustedY = y + halfGrout;
            double cw = cellWidth - grout;
            double ch = cellHeight - grout;
            
            if (cw <= 0 || ch <= 0) return null; // cell fully eaten by grout
            
            cell.Curve = new Rectangle3d(Plane.WorldXY, new Point3d(adjustedX, adjustedY, 0), new Point3d(adjustedX + cw, adjustedY + ch, 0)).ToNurbsCurve();
            cell.Center = new Point3d(adjustedX + cw / 2, adjustedY + ch / 2, 0);
            cell.Corners = new List<Point3d> {
                new Point3d(adjustedX, adjustedY, 0),
                new Point3d(adjustedX + cw, adjustedY, 0),
                new Point3d(adjustedX + cw, adjustedY + ch, 0),
                new Point3d(adjustedX, adjustedY + ch, 0)
            };
            return cell;
        }

        private GridCell CreateHexagonalCell(double centerX, double centerY, double cellWidth, double grout)
        {
            GridCell cell = new GridCell();
            double radius = cellWidth / 2.0;
            if (grout > 0) radius -= grout / Math.Sqrt(3.0);
            if (radius <= 0) return null;
            
            List<Point3d> corners = new List<Point3d>();
            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3.0;
                corners.Add(new Point3d(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle), 0));
            }
            cell.Corners = corners;
            cell.Center = new Point3d(centerX, centerY, 0);
            
            List<Point3d> crvPts = new List<Point3d>(corners);
            crvPts.Add(corners[0]);
            cell.Curve = new PolylineCurve(crvPts);
            return cell;
        }

        private GridCell CreateTriangularCell(double baseX, double baseY, bool inverted, double cellWidth, double height, double grout)
        {
            GridCell cell = new GridCell();
            List<Point3d> corners = new List<Point3d>();
            if (!inverted)
            {
                corners.Add(new Point3d(baseX, baseY, 0));
                corners.Add(new Point3d(baseX + cellWidth, baseY, 0));
                corners.Add(new Point3d(baseX + cellWidth / 2.0, baseY + height, 0));
            }
            else
            {
                corners.Add(new Point3d(baseX + cellWidth / 2.0, baseY + height, 0));
                corners.Add(new Point3d(baseX + cellWidth * 1.5, baseY + height, 0));
                corners.Add(new Point3d(baseX + cellWidth, baseY, 0));
            }
            Point3d centroid = new Point3d(corners.Sum(p => p.X) / 3.0, corners.Sum(p => p.Y) / 3.0, 0);
            
            if (grout > 0)
            {
                double r = height / 3.0;
                double scale = 1.0 - (grout / 2.0) / r;
                if (scale <= 0) return null;
                for (int i = 0; i < corners.Count; i++)
                {
                    corners[i] = centroid + (corners[i] - centroid) * scale;
                }
            }
            
            cell.Corners = corners;
            cell.Center = centroid;

            List<Point3d> crvPts = new List<Point3d>(corners);
            crvPts.Add(corners[0]);
            cell.Curve = new PolylineCurve(crvPts);
            return cell;
        }

        private bool PointContainmentTest(Point3d point, Curve curve, Plane plane, double tolerance = 0.001)
        {
            return curve.Contains(point, plane, tolerance) == PointContainment.Inside;
        }
        public override GH_Exposure Exposure => GH_Exposure.secondary;

    }
}
