import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

# 1. Update RegisterInputParams
old_reg = """            pManager.AddNumberParameter("Cell Height", "Y Dim", "Cell height", GH_ParamAccess.item, 1.0);
        }"""
new_reg = """            pManager.AddNumberParameter("Cell Height", "Y Dim", "Cell height", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Grout Width", "Grout", "Width of the grout joint between tiles. Default 0.", GH_ParamAccess.item, 0.0);
        }"""
content = content.replace(old_reg, new_reg)

# 2. Update SolveInstance
old_solve1 = """            double x_dim = 1.0, y_dim = 1.0;
            string gridType = "rectangular";

            if (!DA.GetData(0, ref baseGeom)) return;
            DA.GetData(1, ref setoutPt);
            DA.GetData(2, ref gridType);
            DA.GetData(3, ref x_dim);
            DA.GetData(4, ref y_dim);"""
new_solve1 = """            double x_dim = 1.0, y_dim = 1.0, grout = 0.0;
            string gridType = "rectangular";

            if (!DA.GetData(0, ref baseGeom)) return;
            DA.GetData(1, ref setoutPt);
            DA.GetData(2, ref gridType);
            DA.GetData(3, ref x_dim);
            DA.GetData(4, ref y_dim);
            DA.GetData(5, ref grout);
            if (grout < 0) grout = 0;"""
content = content.replace(old_solve1, new_solve1)

old_solve2 = "List<GridCell> cells = GenerateCells(bbox, x_dim, y_dim, gridType.ToLower());"
new_solve2 = "List<GridCell> cells = GenerateCells(bbox, x_dim, y_dim, gridType.ToLower(), grout);"
content = content.replace(old_solve2, new_solve2)

# 3. Update GenerateCells Signature and Calls
old_gen = "private List<GridCell> GenerateCells(BoundingBox bbox, double cellWidth, double cellHeight, string gridType)"
new_gen = "private List<GridCell> GenerateCells(BoundingBox bbox, double cellWidth, double cellHeight, string gridType, double grout)"
content = content.replace(old_gen, new_gen)

content = content.replace("CreateRectangularCell(x, y, 0, cellWidth, cellHeight)", "CreateRectangularCell(x, y, 0, cellWidth, cellHeight, grout)")
content = content.replace("CreateRectangularCell(x, y, offset, cellWidth, cellHeight)", "CreateRectangularCell(x, y, offset, cellWidth, cellHeight, grout)")
content = content.replace("CreateHexagonalCell(x, y + offset, cellWidth)", "CreateHexagonalCell(x, y + offset, cellWidth, grout)")
content = content.replace("CreateTriangularCell(x, y, false, cellWidth, height)", "CreateTriangularCell(x, y, false, cellWidth, height, grout)")
content = content.replace("CreateTriangularCell(x, y, true, cellWidth, height)", "CreateTriangularCell(x, y, true, cellWidth, height, grout)")

# 4. Update CreateRectangularCell
old_rect = """        private GridCell CreateRectangularCell(double x, double y, double offset, double cellWidth, double cellHeight)
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
        }"""
new_rect = """        private GridCell CreateRectangularCell(double x, double y, double offset, double cellWidth, double cellHeight, double grout)
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
        }"""
content = content.replace(old_rect, new_rect)

# 5. Update CreateHexagonalCell
old_hex = """        private GridCell CreateHexagonalCell(double centerX, double centerY, double cellWidth)
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
        }"""
new_hex = """        private GridCell CreateHexagonalCell(double centerX, double centerY, double cellWidth, double grout)
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
        }"""
content = content.replace(old_hex, new_hex)

# 6. Update CreateTriangularCell
old_tri = """        private GridCell CreateTriangularCell(double baseX, double baseY, bool inverted, double cellWidth, double height)
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
        }"""
new_tri = """        private GridCell CreateTriangularCell(double baseX, double baseY, bool inverted, double cellWidth, double height, double grout)
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
        }"""
content = content.replace(old_tri, new_tri)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)

