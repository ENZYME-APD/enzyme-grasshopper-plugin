import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

# 1. Rectangular
old_rect = """                double pitchX = cellWidth + grout;
                double pitchY = cellHeight + grout;
                double x = Math.Floor(bbox.Min.X / pitchX) * pitchX;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / pitchY) * pitchY;
                    int rowIndex = (int)Math.Round((y - bbox.Min.Y) / pitchY);

                    while (y < bbox.Max.Y)"""
new_rect = """                double pitchX = cellWidth + grout;
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

                    while (y <= endY)"""
content = content.replace(old_rect, new_rect)

# 2. Hexagonal
old_hex = """                double hSpacing = pitchW * 3.0 / 4.0;
                double vSpacing = pitchW * Math.Sqrt(3) / 2.0;
                double x = Math.Floor(bbox.Min.X / hSpacing) * hSpacing;
                int rowCount = 0;
                while (x < bbox.Max.X)
                {
                    double y = Math.Floor(bbox.Min.Y / vSpacing) * vSpacing;
                    double offset = (rowCount % 2 == 0) ? 0 : vSpacing / 2.0;
                    while (y < bbox.Max.Y)"""
new_hex = """                double hSpacing = pitchW * 3.0 / 4.0;
                double vSpacing = pitchW * Math.Sqrt(3) / 2.0;
                double startX = Math.Floor((bbox.Min.X - pitchW) / hSpacing) * hSpacing;
                double endX = bbox.Max.X + pitchW;
                double startY = Math.Floor((bbox.Min.Y - pitchW) / vSpacing) * vSpacing;
                double endY = bbox.Max.Y + pitchW;
                
                double x = startX;
                int rowCount = 0;
                while (x <= endX)
                {
                    double y = startY;
                    double offset = (rowCount % 2 == 0) ? 0 : vSpacing / 2.0;
                    while (y <= endY)"""
content = content.replace(old_hex, new_hex)

# 3. Triangular
old_tri = """                double pitchW = cellWidth + Math.Sqrt(3.0) * grout;
                double pitchH = pitchW * Math.Sqrt(3) / 2.0;
                double startY = Math.Floor(bbox.Min.Y / pitchH) * pitchH;
                double startX = Math.Floor(bbox.Min.X / pitchW) * pitchW - pitchW;
                
                double y = startY;
                int row = 0;
                while (y < bbox.Max.Y)
                {
                    double x = startX + (row % 2 == 0 ? 0 : pitchW / 2.0);
                    while (x < bbox.Max.X)"""
new_tri = """                double pitchW = cellWidth + Math.Sqrt(3.0) * grout;
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
                    while (x <= endX)"""
content = content.replace(old_tri, new_tri)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)
