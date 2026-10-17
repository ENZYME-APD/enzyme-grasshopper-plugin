import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

old_block = """                    double zTerrain = pt.Z, zLeftT = left.Z, zRightT = right.Z;
                    
                    if (terrain != null)
                    {
                        Ray3d rC = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tC = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rC);
                        if (tC >= 0.0) zTerrain = rC.PointAt(tC).Z;

                        Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                        if (tL >= 0.0) zLeftT = rL.PointAt(tL).Z;
                        else zLeftT = zTerrain;

                        Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                        if (tR >= 0.0) zRightT = rR.PointAt(tR).Z;
                        else zRightT = zTerrain;
                    }

                    double deltaZ = pt.Z - zTerrain;

                    if (deltaZ > threshold)
                    {
                        double currDist = length * ((double)i / tParams.Length);
                        if (currDist - lastPillarDist >= pillarSep)
                        {
                            rd.pillars.Add(new LineCurve(pt, new Point3d(pt.X, pt.Y, zTerrain)));
                            lastPillarDist = currDist;
                        }
                        rd.roadProfiles.Add(new Point3d[] { left, left, pt, right, right });
                        rd.terrProfiles.Add(new Point3d[] { left, left, pt, right, right });
                    }
                    else
                    {
                        Point3d leftBlend = left;
                        if (Math.Abs(zLeftT - left.Z) > 0.1) {
                            Vector3d dir = normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad) * (zLeftT > left.Z ? 1 : -1);
                            double hit = terrain != null ? Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir)) : -1;
                            if (hit >= 0) {
                                Point3d hitPt = left + dir * hit;
                                double dist2D = new Point3d(hitPt.X, hitPt.Y, 0).DistanceTo(new Point3d(left.X, left.Y, 0));
                                leftBlend = new Point3d(left.X + normal.X * dist2D, left.Y + normal.Y * dist2D, hitPt.Z);
                            } else {
                                leftBlend = new Point3d(left.X + normal.X * 2.0, left.Y + normal.Y * 2.0, zLeftT);
                            }
                        } else {
                            leftBlend = new Point3d(left.X + normal.X * 2.0, left.Y + normal.Y * 2.0, zLeftT);
                        }

                        Point3d rightBlend = right;
                        if (Math.Abs(zRightT - right.Z) > 0.1) {
                            Vector3d dir = -normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad) * (zRightT > right.Z ? 1 : -1);
                            double hit = terrain != null ? Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir)) : -1;
                            if (hit >= 0) {
                                Point3d hitPt = right + dir * hit;
                                double dist2D = new Point3d(hitPt.X, hitPt.Y, 0).DistanceTo(new Point3d(right.X, right.Y, 0));
                                rightBlend = new Point3d(right.X - normal.X * dist2D, right.Y - normal.Y * dist2D, hitPt.Z);
                            } else {
                                rightBlend = new Point3d(right.X - normal.X * 2.0, right.Y - normal.Y * 2.0, zRightT);
                            }
                        } else {
                            rightBlend = new Point3d(right.X - normal.X * 2.0, right.Y - normal.Y * 2.0, zRightT);
                        }

                        rd.extraPoints.Add(new Tuple<Point3d, int>(pt, i));
                        rd.extraPoints.Add(new Tuple<Point3d, int>(left, i));
                        rd.extraPoints.Add(new Tuple<Point3d, int>(right, i));
                        rd.extraPoints.Add(new Tuple<Point3d, int>(leftBlend, i));
                        rd.extraPoints.Add(new Tuple<Point3d, int>(rightBlend, i));

                        double exclL = new Point3d(leftBlend.X, leftBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                        double exclR = new Point3d(rightBlend.X, rightBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                        rd.daylightFootprints.Add(new Tuple<Point3d, double>(new Point3d(pt.X, pt.Y, 0), Math.Max(exclL, exclR) + buffer));

                        Point3d leftT = new Point3d(left.X, left.Y, zLeftT);
                        Point3d rightT = new Point3d(right.X, right.Y, zRightT);
                        Point3d ptT = new Point3d(pt.X, pt.Y, zTerrain);

                        rd.roadProfiles.Add(new Point3d[] { leftBlend, left, pt, right, rightBlend });
                        rd.terrProfiles.Add(new Point3d[] { leftBlend, leftT, ptT, rightT, rightBlend });
                    }"""

new_block = """                    double zTerrain = pt.Z, zLeftT = left.Z, zRightT = right.Z;
                    bool onTerrain = false;
                    
                    if (terrain != null)
                    {
                        Ray3d rC = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tC = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rC);
                        if (tC >= 0.0) { zTerrain = rC.PointAt(tC).Z; onTerrain = true; }

                        Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                        if (tL >= 0.0) { zLeftT = rL.PointAt(tL).Z; onTerrain = true; }
                        else zLeftT = zTerrain;

                        Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                        if (tR >= 0.0) { zRightT = rR.PointAt(tR).Z; onTerrain = true; }
                        else zRightT = zTerrain;
                    }

                    // STRICT BOUNDARY FILTER: Only modify terrain if the road segment is actually over the original mesh!
                    if (onTerrain)
                    {
                        double deltaZ = pt.Z - zTerrain;

                        if (deltaZ > threshold)
                        {
                            double currDist = length * ((double)i / tParams.Length);
                            if (currDist - lastPillarDist >= pillarSep)
                            {
                                rd.pillars.Add(new LineCurve(pt, new Point3d(pt.X, pt.Y, zTerrain)));
                                lastPillarDist = currDist;
                            }
                            rd.roadProfiles.Add(new Point3d[] { left, left, pt, right, right });
                            rd.terrProfiles.Add(new Point3d[] { left, left, pt, right, right });
                        }
                        else
                        {
                            Point3d leftBlend = left;
                            if (Math.Abs(zLeftT - left.Z) > 0.1) {
                                Vector3d dir = normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad) * (zLeftT > left.Z ? 1 : -1);
                                double hit = terrain != null ? Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir)) : -1;
                                if (hit >= 0) {
                                    Point3d hitPt = left + dir * hit;
                                    double dist2D = new Point3d(hitPt.X, hitPt.Y, 0).DistanceTo(new Point3d(left.X, left.Y, 0));
                                    leftBlend = new Point3d(left.X + normal.X * dist2D, left.Y + normal.Y * dist2D, hitPt.Z);
                                } else {
                                    leftBlend = new Point3d(left.X + normal.X * 2.0, left.Y + normal.Y * 2.0, zLeftT);
                                }
                            } else {
                                leftBlend = new Point3d(left.X + normal.X * 2.0, left.Y + normal.Y * 2.0, zLeftT);
                            }

                            Point3d rightBlend = right;
                            if (Math.Abs(zRightT - right.Z) > 0.1) {
                                Vector3d dir = -normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad) * (zRightT > right.Z ? 1 : -1);
                                double hit = terrain != null ? Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir)) : -1;
                                if (hit >= 0) {
                                    Point3d hitPt = right + dir * hit;
                                    double dist2D = new Point3d(hitPt.X, hitPt.Y, 0).DistanceTo(new Point3d(right.X, right.Y, 0));
                                    rightBlend = new Point3d(right.X - normal.X * dist2D, right.Y - normal.Y * dist2D, hitPt.Z);
                                } else {
                                    rightBlend = new Point3d(right.X - normal.X * 2.0, right.Y - normal.Y * 2.0, zRightT);
                                }
                            } else {
                                rightBlend = new Point3d(right.X - normal.X * 2.0, right.Y - normal.Y * 2.0, zRightT);
                            }

                            rd.extraPoints.Add(new Tuple<Point3d, int>(pt, i));
                            rd.extraPoints.Add(new Tuple<Point3d, int>(left, i));
                            rd.extraPoints.Add(new Tuple<Point3d, int>(right, i));
                            rd.extraPoints.Add(new Tuple<Point3d, int>(leftBlend, i));
                            rd.extraPoints.Add(new Tuple<Point3d, int>(rightBlend, i));

                            double exclL = new Point3d(leftBlend.X, leftBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                            double exclR = new Point3d(rightBlend.X, rightBlend.Y, 0).DistanceTo(new Point3d(pt.X, pt.Y, 0));
                            rd.daylightFootprints.Add(new Tuple<Point3d, double>(new Point3d(pt.X, pt.Y, 0), Math.Max(exclL, exclR) + buffer));

                            Point3d leftT = new Point3d(left.X, left.Y, zLeftT);
                            Point3d rightT = new Point3d(right.X, right.Y, zRightT);
                            Point3d ptT = new Point3d(pt.X, pt.Y, zTerrain);

                            rd.roadProfiles.Add(new Point3d[] { leftBlend, left, pt, right, rightBlend });
                            rd.terrProfiles.Add(new Point3d[] { leftBlend, leftT, ptT, rightT, rightBlend });
                        }
                    }"""

content = content.replace(old_block, new_block)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
