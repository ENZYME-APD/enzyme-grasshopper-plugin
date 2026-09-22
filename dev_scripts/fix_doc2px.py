with open('Components/PaperSizeToPixels.cs', 'r') as f:
    content = f.read()

# Replace the digital calculation logic
old_logic = """            if (isPhysical)
            {
                // Ensure correct orientation
                double shortSide = Math.Min(widthMM, heightMM);
                double longSide = Math.Max(widthMM, heightMM);

                if (landscape)
                {
                    widthMM = longSide;
                    heightMM = shortSide;
                }
                else
                {
                    widthMM = shortSide;
                    heightMM = longSide;
                }

                exactWidth = (int)Math.Round((widthMM / 25.4) * dpi);
                exactHeight = (int)Math.Round((heightMM / 25.4) * dpi);
            }
            else
            {
                // For exact pixels, if user forces portrait, flip them.
                if (!landscape && exactWidth > exactHeight)
                {
                    int temp = exactWidth;
                    exactWidth = exactHeight;
                    exactHeight = temp;
                }
            }"""

new_logic = """            if (isPhysical)
            {
                // Ensure correct orientation
                double shortSide = Math.Min(widthMM, heightMM);
                double longSide = Math.Max(widthMM, heightMM);

                if (landscape)
                {
                    widthMM = longSide;
                    heightMM = shortSide;
                }
                else
                {
                    widthMM = shortSide;
                    heightMM = longSide;
                }

                exactWidth = (int)Math.Round((widthMM / 25.4) * dpi);
                exactHeight = (int)Math.Round((heightMM / 25.4) * dpi);
            }
            else
            {
                // Scale digital formats by DPI (baseline 72)
                double scale = dpi / 72.0;
                exactWidth = (int)Math.Round(exactWidth * scale);
                exactHeight = (int)Math.Round(exactHeight * scale);

                // For exact pixels, if user forces portrait, flip them.
                if (!landscape && exactWidth > exactHeight)
                {
                    int temp = exactWidth;
                    exactWidth = exactHeight;
                    exactHeight = temp;
                }
            }"""

content = content.replace(old_logic, new_logic)

with open('Components/PaperSizeToPixels.cs', 'w') as f:
    f.write(content)
