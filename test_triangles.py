# Let's map out the correct triangle centers and corners.
# An equilateral triangle of side length W has height H = W * sqrt(3) / 2.
# Let's write the loop to correctly generate them.

def generate_triangles(min_x, max_x, min_y, max_y, W, H):
    y = min_y
    row = 0
    cells = []
    while y < max_y:
        # In even rows, upright bases start at 0.
        # In odd rows, upright bases start at W/2.
        start_x = min_x if row % 2 == 0 else min_x + W/2.0
        
        x = start_x
        while x < max_x:
            # Upright triangle
            upright = [
                (x, y),
                (x + W, y),
                (x + W/2.0, y + H)
            ]
            cells.append(("upright", upright))
            
            # Inverted triangle (to the right of this upright one)
            inverted = [
                (x + W/2.0, y + H),
                (x + W*1.5, y + H),
                (x + W, y)
            ]
            cells.append(("inverted", inverted))
            
            x += W
        y += H
        row += 1
    return cells

cells = generate_triangles(0, 3, 0, 2, 1, 1)
for t, pts in cells[:4]:
    print(t, pts)
print("Row 1")
for t, pts in cells[6:10]:
    print(t, pts)
