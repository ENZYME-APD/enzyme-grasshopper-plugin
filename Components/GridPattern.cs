using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Enzyme.Components
{
    public class GridPattern : GH_Component
    {
        public GridPattern()
          : base("Grid Pattern Generator and Trimmer", "GridPattern",
              "",
              "Enzyme", "Pattern")
        {
        }

        public override Guid ComponentGuid => new Guid("D7A50BEF-4309-4C41-BCA6-455AB0B2C471");

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("GridPattern.png");
            }
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 2.0, 1.0, 330, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 2.0, 1.0, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "curve", 220, -23);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", 220, 22);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "Boundary", "Closed boundary curve(s)", GH_ParamAccess.tree);
            pManager.AddPlaneParameter("Origin Plane", "origin_plane", "Grid base plane", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddTextParameter("Grid Type", "grid_type", "Grid type: rectangular, offset_rectangular, hexagonal, triangular", GH_ParamAccess.item, "rectangular");
            pManager.AddNumberParameter("Cell Width", "x_dim", "Cell width", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Cell Height", "y_dim", "Cell height", GH_ParamAccess.item, 1.0);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("All Tiles", "a", "All grid tiles", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Tile Status", "b", "0 = full, 1 = trimmed", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Full Count", "c", "Number of complete cells", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Trim Count", "d", "Number of trimmed cells", GH_ParamAccess.item);
            pManager.AddCurveParameter("Full Tiles", "e", "Tiles fully inside the boundary", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> boundaryTree))
                return;

            Plane originPlane = Plane.WorldXY;
            DA.GetData(1, ref originPlane);

            string gridType = "rectangular";
            DA.GetData(2, ref gridType);

            double x_dim = 1.0;
            DA.GetData(3, ref x_dim);

            double y_dim = 1.0;
            DA.GetData(4, ref y_dim);

            List<Curve> allTiles = new List<Curve>();
            List<int> panelStatus = new List<int>();
            int fullCount = 0;
            int trimCount = 0;
            List<Curve> fullTiles = new List<Curve>();

            List<Curve> boundaries = new List<Curve>();
            foreach (GH_Path path in boundaryTree.Paths)
            {
                var branch = boundaryTree.get_Branch(path);
                foreach (GH_Curve ghCurve in branch)
                {
                    if (ghCurve != null && ghCurve.Value != null)
                    {
                        Curve crv = ghCurve.Value;
                        if (crv is PolylineCurve)
                            crv = crv.ToNurbsCurve();
                        
                        if (crv.IsClosed)
                            boundaries.Add(crv);
                        else
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Boundary must be a closed curve");
                    }
                }
            }

            if (boundaries.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No valid closed boundaries provided.");
                return;
            }

            foreach (Curve boundary in boundaries)
            {
                // Create transforms
                Transform toLocal = Transform.PlaneToPlane(originPlane, Plane.WorldXY);
                Transform toWorld = Transform.PlaneToPlane(Plane.WorldXY, originPlane);

                Curve localBoundary = boundary.DuplicateCurve();
                localBoundary.Transform(toLocal);
                BoundingBox bbox = localBoundary.GetBoundingBox(true);

                List<GridCell> cells = GenerateCells(bbox, x_dim, y_dim, gridType);

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
                                panelStatus.Add(1);
                                trimCount++;
                            }
                        }
                    }
                }
            }

            string capGridType = gridType.Length > 0 ? char.ToUpper(gridType[0]) + gridType.Substring(1).ToLower() : gridType;
            Message = $"{capGridType} Grid\n{fullCount} complete | {trimCount} trimmed";

            DA.SetDataList(0, allTiles);
            DA.SetDataList(1, panelStatus);
            DA.SetData(2, fullCount);
            DA.SetData(3, trimCount);
            DA.SetDataList(4, fullTiles);
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
                double x = Math.Floor(bbox.Min.X / cellWidth) * cellWidth;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / height) * height;
                    while (y < bbox.Max.Y)
                    {
                        cells.Add(CreateTriangularCell(x, y, false, cellWidth, height));
                        cells.Add(CreateTriangularCell(x, y, true, cellWidth, height));
                        y += height;
                    }
                    x += cellWidth;
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
            cell.Curve = Curve.CreateInterpolatedCurve(crvPts, 1);
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
                corners.Add(new Point3d(baseX + cellWidth / 2.0, baseY, 0));
                corners.Add(new Point3d(baseX + cellWidth, baseY + height, 0));
                corners.Add(new Point3d(baseX, baseY + height, 0));
            }
            cell.Corners = corners;
            cell.Center = new Point3d(corners.Sum(p => p.X) / 3.0, corners.Sum(p => p.Y) / 3.0, 0);

            List<Point3d> crvPts = new List<Point3d>(corners);
            crvPts.Add(corners[0]);
            cell.Curve = Curve.CreateInterpolatedCurve(crvPts, 1);
            return cell;
        }

        private bool PointContainmentTest(Point3d point, Curve curve, Plane plane, double tolerance = 0.001)
        {
            return curve.Contains(point, plane, tolerance) == PointContainment.Inside;
        }
    }
}
