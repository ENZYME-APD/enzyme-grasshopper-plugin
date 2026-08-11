using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics; // stopwatch
using System.Linq; // for Enumerable
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Enzyme; // for IconLoader

namespace Enzyme.Components
{
    public class TileGridGenerator : GH_Component
    {
        public TileGridGenerator()
            : base("Grid Pattern Generator and Trimmer", "GridPattern",
                "Generates a grid pattern (rectangular, hexagonal, triangular) within a boundary and trims cells to fit.",
                "Enzyme", "Pattern")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                Bitmap icon = IconLoader.Load("tile_gen_icon.png");
                if (icon == null)
                    this.Message = "Icon missing";
                return icon;
            }
        }

        public override Guid ComponentGuid => new Guid("3E7B9F2A-C4D8-4A1E-B5F3-8D2C6E0A9B4F");

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

            // Create transforms between world and local coordinates
            Transform toLocal = Transform.PlaneToPlane(originPlane, Plane.WorldXY);
            Transform toWorld = Transform.PlaneToPlane(Plane.WorldXY, originPlane);

            // Transform boundary to local coordinates for calculations
            Curve localBoundary = boundary.DuplicateCurve();
            localBoundary.Transform(toLocal);

            // Get bounding box in local coordinates
            BoundingBox bbox = localBoundary.GetBoundingBox(true);

            // --- Grid generation and trimming logic ---
            // 1. Generate grid cells (rectangular, hex, triangle) in local plane coordinates
            // 2. Transform cells to world coordinates
            // 3. Test containment/intersection with boundary
            // 4. Output all tiles, panel status, counts, and full tiles

            // Generate grid cells based on grid type
            List<GridCell> cells = GenerateCells(bbox, x_dim, y_dim, gridType);

            // Process cells and determine which are inside, intersecting, or outside the boundary
            var allTiles = new List<Curve>();
            var panelStatus = new List<int>();
            int fullCount = 0;
            int trimCount = 0;
            var fullTiles = new List<Curve>();
            var trimmedTiles = new List<Curve>();

            foreach (GridCell cell in cells)
            {
                // Transform cell to world coordinates for intersection testing
                cell.Transform(toWorld);

                // Test if cell is fully inside, intersecting, or outside the boundary
                List<Point3d> testPoints = cell.GetTestPoints();
                bool allPointsInside = true;

                foreach (Point3d pt in testPoints)
                {
                    if (PointContainmentTest(pt, localBoundary, originPlane) != PointContainment.Inside)
                    {
                        allPointsInside = false;
                        break;
                    }
                }

                // Check for intersection
                CurveIntersections intersections = Intersection.CurveCurve(cell.Curve, boundary, 0.001, 0.001);
                bool hasIntersection = intersections != null && intersections.Count > 0;

                if (allPointsInside)
                {
                    // Cell is fully inside
                    allTiles.Add(cell.Curve);
                    fullTiles.Add(cell.Curve);
                    panelStatus.Add(0);
                    fullCount++;
                }
                else if (hasIntersection || testPoints.Exists(pt => PointContainmentTest(pt, localBoundary, originPlane) == PointContainment.Inside))
                {
                    // Cell intersects with boundary - trim it
                    Curve[] trimmedCurves = Curve.CreateBooleanIntersection(cell.Curve, boundary, 0.001);
                    if (trimmedCurves != null && trimmedCurves.Length > 0)
                    {
                        allTiles.AddRange(trimmedCurves);
                        trimmedTiles.AddRange(trimmedCurves);
                        panelStatus.AddRange(Enumerable.Repeat(1, trimmedCurves.Length));
                        trimCount += trimmedCurves.Length;
                    }
                }
            }

            stopwatch.Stop();
            double executionTime = stopwatch.Elapsed.TotalSeconds;

            // Example output (empty)
            DA.SetDataList(0, panelStatus);
            DA.SetData(1, fullCount);
            DA.SetData(2, trimCount);
            DA.SetDataList(3, allTiles);
            DA.SetDataList(4, fullTiles);
            DA.SetDataList(5, trimmedTiles);

            Message = $"{gridType} Grid";
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
                var points = new List<Point3d>(Corners);
                if (Center != null)
                    points.Add(Center);
                return points;
            }

            public void Transform(Transform xform)
            {
                if (Curve != null)
                    Curve.Transform(xform);
                if (Center != null)
                    Center.Transform(xform);
                for (int i = 0; i < Corners.Count; i++)
                {
                    Corners[i].Transform(xform);
                }
            }
        }

        private List<GridCell> GenerateCells(
            BoundingBox bbox,
            double cellWidth,
            double cellHeight,
            string gridType
        )
        {
            var cells = new List<GridCell>();

            switch (gridType.ToLower())
            {
                case "rectangular":
                case "offset_rectangular":
                {
                    double x = Math.Floor(bbox.Min.X / cellWidth) * cellWidth;
                    int rowIndex = 0;
                    double yStart = Math.Floor(bbox.Min.Y / cellHeight) * cellHeight;
                    while (x < bbox.Max.X)
                    {
                        rowIndex = 0;
                        double y = yStart;
                        while (y < bbox.Max.Y)
                        {
                            // Offset alternate rows for offset_rectangular
                            double offset = (gridType.ToLower() == "offset_rectangular" && rowIndex % 2 == 1) ? cellWidth / 2 : 0;
                            var cell = CreateRectangularCell(x + offset, y, cellWidth, cellHeight);
                            cells.Add(cell);
                            y += cellHeight;
                            rowIndex++;
                        }
                        x += cellWidth;
                    }
                    break;
                }

                case "hexagonal":
                {
                    double hSpacing = cellWidth * 3 / 4;
                    double vSpacing = cellWidth * Math.Sqrt(3) / 2;
                    double x = Math.Floor(bbox.Min.X / hSpacing) * hSpacing;
                    int rowIndex = 0;
                    while (x < bbox.Max.X)
                    {
                        double y = Math.Floor(bbox.Min.Y / vSpacing) * vSpacing;
                        double offset = (rowIndex % 2 == 1) ? vSpacing / 2 : 0;
                        while (y < bbox.Max.Y)
                        {
                            var cell = CreateHexagonalCell(x, y + offset, cellWidth);
                            cells.Add(cell);
                            y += vSpacing;
                        }
                        rowIndex++;
                        x += hSpacing;
                    }
                    break;
                }

                case "triangular":
                {
                    double height = cellWidth * Math.Sqrt(3) / 2;
                    double x = Math.Floor(bbox.Min.X / cellWidth) * cellWidth;
                    double yStart = Math.Floor(bbox.Min.Y / height) * height;
                    while (x < bbox.Max.X)
                    {
                        double y = yStart;
                        int rowIndex = 0;
                        while (y < bbox.Max.Y)
                        {
                            var cell = CreateTriangularCell(x, y, (rowIndex % 2 == 1), cellWidth, height);
                            cells.Add(cell);
                            cell = CreateTriangularCell(x + cellWidth / 2, y, (rowIndex % 2 == 0), cellWidth, height);
                            cells.Add(cell);
                            y += height;
                            rowIndex++;
                        }
                        x += cellWidth;
                    }
                    break;
                }
            }

            return cells;
        }

        private GridCell CreateRectangularCell(
            double x,
            double y,
            double cellWidth,
            double cellHeight
        )
        {
            var cell = new GridCell();
            cell.Curve = new Rectangle3d(Plane.WorldXY, new Point3d(x, y, 0), new Point3d(x + cellWidth, y + cellHeight, 0)).ToNurbsCurve();
            cell.Center = new Point3d(x + cellWidth / 2, y + cellHeight / 2, 0);
            cell.Corners.Add(new Point3d(x, y, 0));
            cell.Corners.Add(new Point3d(x + cellWidth, y, 0));
            cell.Corners.Add(new Point3d(x + cellWidth, y + cellHeight, 0));
            cell.Corners.Add(new Point3d(x, y + cellHeight, 0));
            return cell;
        }

        private GridCell CreateHexagonalCell(
            double x, // center
            double y, // center
            double cellWidth
        )
        {
            var cell = new GridCell();
            double radius = cellWidth / 2;
            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3;
                double cornerX = x + radius * Math.Cos(angle);
                double cornerY = y + radius * Math.Sin(angle);
                cell.Corners.Add(new Point3d(cornerX, cornerY, 0));
            }
            cell.Center = new Point3d(x, y, 0);
            cell.Corners.Add(cell.Corners[0]); // close the curve
            cell.Curve = Curve.CreateInterpolatedCurve(cell.Corners, 1);
            return cell;
        }

        private GridCell CreateTriangularCell(
            double x,
            double y,
            bool inverted,
            double cellWidth,
            double height
        )
        {
            var cell = new GridCell();
            if (inverted)
            {
                cell.Corners.Add(new Point3d(x, y, 0));
                cell.Corners.Add(new Point3d(x + cellWidth, y, 0));
                cell.Corners.Add(new Point3d(x + cellWidth / 2, y + height, 0));
            }
            else
            {
                cell.Corners.Add(new Point3d(x + cellWidth / 2, y, 0));
                cell.Corners.Add(new Point3d(x + cellWidth, y + height, 0));
                cell.Corners.Add(new Point3d(x, y + height, 0));
            }
            // Calc center as average of corner points
            double centerX = (cell.Corners[0].X + cell.Corners[1].X + cell.Corners[2].X) / 3;
            double centerY = (cell.Corners[0].Y + cell.Corners[1].Y + cell.Corners[2].Y) / 3;
            cell.Center = new Point3d(centerX, centerY, 0);
            cell.Corners.Add(cell.Corners[0]);
            cell.Curve = Curve.CreateInterpolatedCurve(cell.Corners, 1);
            return cell;
        }

        private PointContainment PointContainmentTest(
            Point3d point,
            Curve curve,
            Plane plane,
            double tolerance = 0.001
        )
        {
            return curve.Contains(point, plane, tolerance);
        }
    }
}