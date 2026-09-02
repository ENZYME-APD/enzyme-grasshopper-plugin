import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# 1. Add ExclusionNode struct at the top of RoadGenerator class
excl_struct = """        private struct ExclusionNode
        {
            public Point3d Pt2D;
            public double Radius;
        }"""
content = content.replace("public class RoadGenerator : GH_Component\n    {", "public class RoadGenerator : GH_Component\n    {\n" + excl_struct)

# 2. Add the list inside SolveInstance
content = content.replace("List<Point3d> extraPoints = new List<Point3d>();", "List<Point3d> extraPoints = new List<Point3d>();\n            List<ExclusionNode> exclNodes = new List<ExclusionNode>();")

# 3. Store Exclusion Nodes
excl_logic = """                            // Blend points
                            double horizontalBlend = Math.Abs(deltaZ) / tanAngle;
                            
                            exclNodes.Add(new ExclusionNode { Pt2D = new Point3d(pt.X, pt.Y, 0), Radius = totalHalfWidth + horizontalBlend + 0.5 });
                            
                            if (horizontalBlend > 0.1)"""
content = content.replace("""                            // Blend points
                            double horizontalBlend = Math.Abs(deltaZ) / tanAngle;
                            if (horizontalBlend > 0.1)""", excl_logic)

# 4. Replace the filtering logic
old_filter = """                foreach (var op in origPts)
                {
                    bool tooClose = false;
                    // Simple distance check (can be slow for huge meshes, but okay for moderate)
                    // Optimised: we only check Z if XY is close
                    for (int i=0; i<extraPoints.Count; i+=5) // sparse check
                    {
                        var ep = extraPoints[i];
                        if (Math.Abs(op.X - ep.X) < totalHalfWidth * 2 && Math.Abs(op.Y - ep.Y) < totalHalfWidth * 2)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose) pts.Add(op);
                }"""

new_filter = """                foreach (var op in origPts)
                {
                    bool tooClose = false;
                    Point3d op2D = new Point3d(op.X, op.Y, 0);
                    
                    foreach (var node in exclNodes)
                    {
                        if (Math.Abs(op2D.X - node.Pt2D.X) < node.Radius && Math.Abs(op2D.Y - node.Pt2D.Y) < node.Radius)
                        {
                            if (op2D.DistanceTo(node.Pt2D) < node.Radius)
                            {
                                tooClose = true;
                                break;
                            }
                        }
                    }
                    if (!tooClose) pts.Add(op);
                }"""
content = content.replace(old_filter, new_filter)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
