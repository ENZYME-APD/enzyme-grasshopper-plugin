import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

# 1. Rectangular
old_rect = """            if (gridType == "rectangular" || gridType == "offset_rectangular")
            {
                double x = Math.Floor(bbox.Min.X / cellWidth) * cellWidth;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / cellHeight) * cellHeight;
                    int rowIndex = (int)Math.Round((y - bbox.Min.Y) / cellHeight);

                    while (y < bbox.Max.Y)
                    {
                        double offset = (gridType == "offset_rectangular" && rowIndex % 2 == 1) ? 0.5 * cellWidth : 0;
                        cells.Add(CreateRectangularCell(x, y, offset, cellWidth, cellHeight, grout));
                        y += cellHeight;
                        rowIndex++;
                    }
                    x += cellWidth;
                }
            }"""
new_rect = """            if (gridType == "rectangular" || gridType == "offset_rectangular")
            {
                double pitchX = cellWidth + grout;
                double pitchY = cellHeight + grout;
                double x = Math.Floor(bbox.Min.X / pitchX) * pitchX;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / pitchY) * pitchY;
                    int rowIndex = (int)Math.Round((y - bbox.Min.Y) / pitchY);

                    while (y < bbox.Max.Y)
                    {
                        double offset = (gridType == "offset_rectangular" && rowIndex % 2 == 1) ? 0.5 * pitchX : 0;
                        cells.Add(CreateRectangularCell(x, y, offset, pitchX, pitchY, grout));
                        y += pitchY;
                        rowIndex++;
                    }
                    x += pitchX;
                }
            }"""
content = content.replace(old_rect, new_rect)

# 2. Hexagonal
old_hex = """            else if (gridType == "hexagonal")
            {
                double hSpacing = cellWidth * 3.0 / 4.0;
                double vSpacing = cellWidth * Math.Sqrt(3) / 2.0;
                double x = Math.Floor(bbox.Min.X / hSpacing) * hSpacing;
                int rowCount = 0;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / vSpacing) * vSpacing;
                    double offset = (rowCount % 2 == 0) ? 0 : vSpacing / 2.0;
                    while (y < bbox.Max.Y)
                    {
                        cells.Add(CreateHexagonalCell(x, y + offset, cellWidth, grout));
                        y += vSpacing;
                    }
                    rowCount++;
                    x += hSpacing;
                }
            }"""
new_hex = """            else if (gridType == "hexagonal")
            {
                double pitchW = cellWidth + 2.0 * grout / Math.Sqrt(3.0);
                double hSpacing = pitchW * 3.0 / 4.0;
                double vSpacing = pitchW * Math.Sqrt(3) / 2.0;
                double x = Math.Floor(bbox.Min.X / hSpacing) * hSpacing;
                int rowCount = 0;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / vSpacing) * vSpacing;
                    double offset = (rowCount % 2 == 0) ? 0 : vSpacing / 2.0;
                    while (y < bbox.Max.Y)
                    {
                        cells.Add(CreateHexagonalCell(x, y + offset, pitchW, grout));
                        y += vSpacing;
                    }
                    rowCount++;
                    x += hSpacing;
                }
            }"""
content = content.replace(old_hex, new_hex)

# 3. Triangular
old_tri = """            else if (gridType == "triangular")
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
                        cells.Add(CreateTriangularCell(x, y, false, cellWidth, height, grout));
                        cells.Add(CreateTriangularCell(x, y, true, cellWidth, height, grout));
                        x += cellWidth;
                    }
                    y += height;
                    row++;
                }
            }"""
new_tri = """            else if (gridType == "triangular")
            {
                double pitchW = cellWidth + Math.Sqrt(3.0) * grout;
                double pitchH = pitchW * Math.Sqrt(3) / 2.0;
                double startY = Math.Floor(bbox.Min.Y / pitchH) * pitchH;
                double startX = Math.Floor(bbox.Min.X / pitchW) * pitchW - pitchW;
                
                double y = startY;
                int row = 0;
                while (y < bbox.Max.Y)
                {
                    double x = startX + (row % 2 == 0 ? 0 : pitchW / 2.0);
                    while (x < bbox.Max.X)
                    {
                        cells.Add(CreateTriangularCell(x, y, false, pitchW, pitchH, grout));
                        cells.Add(CreateTriangularCell(x, y, true, pitchW, pitchH, grout));
                        x += pitchW;
                    }
                    y += pitchH;
                    row++;
                }
            }"""
content = content.replace(old_tri, new_tri)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)
