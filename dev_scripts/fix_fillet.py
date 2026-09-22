import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

safe_fillet = """        // Custom Clamped Filleting
        private Curve SafeFilletPolyline(Polyline poly, double targetRadius)
        {
            if (poly.Count < 3) return new PolylineCurve(poly);
            
            bool isClosed = poly.IsClosed;
            int count = isClosed ? poly.Count - 1 : poly.Count;
            
            PolyCurve pc = new PolyCurve();
            
            Point3d[] p = poly.ToArray();
            Point3d[] newPts = new Point3d[count * 2]; // For segments and arcs
            
            // First, find the valid tangent distance for every corner
            double[] maxT = new double[count];
            for (int i = 0; i < count; i++)
            {
                if (!isClosed && (i == 0 || i == count - 1)) continue;
                
                Point3d prev = p[i == 0 ? count - 1 : i - 1];
                Point3d curr = p[i];
                Point3d next = p[i + 1]; // Works because p has count+1 if closed
                
                Vector3d vIn = prev - curr;
                Vector3d vOut = next - curr;
                double lenIn = vIn.Length;
                double lenOut = vOut.Length;
                
                if (lenIn < 0.01 || lenOut < 0.01) { maxT[i] = 0; continue; }
                
                vIn.Unitize();
                vOut.Unitize();
                
                double angle = Vector3d.VectorAngle(vIn, vOut);
                if (angle < 0.01 || angle > Math.PI - 0.01) { maxT[i] = 0; continue; }
                
                double reqT = targetRadius / Math.Tan(angle / 2.0);
                
                // Segment length constraint (49% of shortest adjacent segment to leave room for the other corner)
                double allowedT = Math.Min(lenIn, lenOut) * 0.49; 
                maxT[i] = Math.Min(reqT, allowedT);
            }
            
            // Build the segments and arcs
            for (int i = 0; i < count; i++)
            {
                if (!isClosed && i == count - 1) break; // last point handled
                
                Point3d curr = p[i];
                Point3d next = p[i + 1];
                
                double tStart = maxT[i];
                double tEnd = maxT[(i + 1) % count];
                
                if (!isClosed && i == 0) tStart = 0;
                if (!isClosed && i == count - 2) tEnd = 0;
                
                Vector3d edge = next - curr;
                double edgeLen = edge.Length;
                edge.Unitize();
                
                Point3d p1 = curr + edge * tStart;
                Point3d p2 = next - edge * tEnd;
                
                if (p1.DistanceTo(p2) > 0.001)
                    pc.Append(new LineCurve(p1, p2));
                    
                // Generate Arc at 'next' vertex
                if (tEnd > 0.001)
                {
                    Point3d nextNext = p[(i + 2) % (isClosed ? count : count + 1)]; // Careful with open array
                    if (isClosed || i + 2 < p.Length)
                    {
                        nextNext = p[i + 2];
                        Vector3d nextEdge = nextNext - next;
                        nextEdge.Unitize();
                        Point3d p3 = next + nextEdge * tEnd; // Start of next segment
                        
                        // Create Arc from p2 to p3 tangent to segments
                        // Simplest way is a 3-point arc. Midpoint can be found using bisector.
                        Vector3d vIn = curr - next;
                        Vector3d vOut = nextNext - next;
                        vIn.Unitize(); vOut.Unitize();
                        Vector3d bisector = (vIn + vOut) / 2.0;
                        bisector.Unitize();
                        
                        // Distance to arc center: d = tEnd / sin(angle/2)
                        // Distance from corner to arc midpoint: tEnd * tan(angle/4)
                        double halfAngle = Vector3d.VectorAngle(vIn, vOut) / 2.0;
                        double midDist = tEnd * Math.Tan(halfAngle / 2.0);
                        Point3d pMid = next + bisector * midDist;
                        
                        Arc arc = new Arc(p2, pMid, p3);
                        if (arc.IsValid) pc.Append(new ArcCurve(arc));
                    }
                }
            }
            
            if (isClosed && pc.IsClosed == false && pc.SegmentCount > 0)
            {
                // Ensure it closes
                Line l = new Line(pc.PointAtEnd, pc.PointAtStart);
                if (l.Length > 0.001) pc.Append(new LineCurve(l));
            }
            
            if (pc.SegmentCount > 0) return pc;
            return new PolylineCurve(poly);
        }"""

content = re.sub(r'// Custom Clamped Filleting.*?(?=\n        private Point3d)', safe_fillet, content, flags=re.DOTALL)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
