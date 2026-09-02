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
            pManager.AddCurveParameter("Boundary", "Boundary", "Closed boundary curve", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Origin Plane", "Plane", "Grid base plane", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddTextParameter("Grid Type", "Grid Type", "Grid type: rectangular, offset_rectangular, hexagonal, triangular", GH_ParamAccess.item, "rectangular");
            pManager.AddNumberParameter("Cell Width", "X Dim", "Cell width", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Cell Height", "Y Dim", "Cell height", GH_ParamAccess.item, 1.0);
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

            Curve boundary = null;
            double x_dim = 1.0, y_dim = 1.0;
            string gridType = "rectangular";
            Plane originPlane = Plane.WorldXY;

            if (!DA.GetData(0, ref boundary)) return;
            DA.GetData(1, ref originPlane);
            DA.GetData(2, ref gridType);
            DA.GetData(3, ref x_dim);
            DA.GetData(4, ref y_dim);

            if (boundary == null || !boundary.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Boundary must be a closed curve.");
                return;
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

            List<GridCell> cells = GenerateCells(bbox, x_dim, y_dim, gridType.ToLower());

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

        private List<GridCell> GenerateCells(BoundingBox bbox, double cellWidth, double cellHeight, string gridType)
        {
            List<GridCell> cells = new List<GridCell>();

            if (gridType == "rectangular" || gridType == "offset_rectangular")
            {
                double x = Math.Floor(bbox.Min.X / cellWidth) * cellWidth;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / cellHeight) * cellHeight;
                    int rowIndex = (int)Math.Round((y - bbox.Min.Y) / cellHeight);

                    while (y < bbox.Max.Y)
                    {
                        double offset = (gridType == "offset_rectangular" && rowIndex % 2 == 1) ? 0.5 * cellWidth : 0;
                        cells.Add(CreateRectangularCell(x, y, offset, cellWidth, cellHeight));
                        y += cellHeight;
                        rowIndex++;
                    }
                    x += cellWidth;
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
                        cells.Add(CreateHexagonalCell(x, y + offset, cellWidth));
                        y += vSpacing;
                    }
                    rowCount++;
                    x += hSpacing;
                }
            }
            else if (gridType == "triangular")
            {
                double height = cellWidth * Math.Sqrt(3) / 2.0;
                double startY = Math.Floor(bbox.Min.Y / height) * height;
                double startX = Math.Floor(bbox.Min.X / cellWidth) * cellWidth - cellWidth;
                
                double y = startY;
                int row = 0;
                while (y < bbox.Max.Y)
                {
                    double x = startX + (row % 2 == 0 ? 0 : cellWidth / 2.0);
                    while (x < bbox.Max.X)
                    {
                        cells.Add(CreateTriangularCell(x, y, false, cellWidth, height));
                        cells.Add(CreateTriangularCell(x, y, true, cellWidth, height));
                        x += cellWidth;
                    }
                    y += height;
                    row++;
                }
            }

            return cells;
        }

        private GridCell CreateRectangularCell(double x, double y, double offset, double cellWidth, double cellHeight)
        {
            GridCell cell = new GridCell();
            double adjustedX = x + offset;
            cell.Curve = new Rectangle3d(Plane.WorldXY, new Point3d(adjustedX, y, 0), new Point3d(adjustedX + cellWidth, y + cellHeight, 0)).ToNurbsCurve();
            cell.Center = new Point3d(adjustedX + cellWidth / 2, y + cellHeight / 2, 0);
            cell.Corners = new List<Point3d> {
                new Point3d(adjustedX, y, 0),
                new Point3d(adjustedX + cellWidth, y, 0),
                new Point3d(adjustedX + cellWidth, y + cellHeight, 0),
                new Point3d(adjustedX, y + cellHeight, 0)
            };
            return cell;
        }

        private GridCell CreateHexagonalCell(double centerX, double centerY, double cellWidth)
        {
            GridCell cell = new GridCell();
            double radius = cellWidth / 2.0;
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

        private GridCell CreateTriangularCell(double baseX, double baseY, bool inverted, double cellWidth, double height)
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
            cell.Corners = corners;
            cell.Center = new Point3d(corners.Sum(p => p.X) / 3.0, corners.Sum(p => p.Y) / 3.0, 0);

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
