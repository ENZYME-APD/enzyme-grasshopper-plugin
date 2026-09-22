import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# 1. Remove the dangerous Curve Rebuild that causes wobbling
subdiv_old = """                double length = nCrv.GetLength();
                int divs = Math.Max(2, (int)(length / subDist));
                
                // NATIVE ARC-LENGTH DIVISION: No Chord-Jumping across hairpins, and no domain-interpolation fallback twists
                double[] tParams = nCrv.DivideByCount(divs, false); // false = strictly arc-length
                
                if (tParams == null || tParams.Length < 2) {
                    // Absolute fallback: Rebuild the curve to ensure perfectly uniform parameterization and try again
                    nCrv = nCrv.Rebuild(Math.Max(10, divs), 3, true);
                    tParams = nCrv.DivideByCount(divs, false);
                }
                
                if (tParams == null || tParams.Length < 2) {
                    // Final safety net
                    tParams = new double[divs + 1];
                    for (int i = 0; i <= divs; i++) {
                        tParams[i] = nCrv.Domain.T0 + (nCrv.Domain.T1 - nCrv.Domain.T0) * ((double)i / divs);
                    }
                }"""

subdiv_new = """                double length = nCrv.GetLength();
                int divs = Math.Max(2, (int)(length / subDist));
                
                double[] tParams = nCrv.DivideByCount(divs, true); 
                if (tParams == null || tParams.Length < 2) tParams = nCrv.DivideByCount(divs, false);
                
                if (tParams == null || tParams.Length < 2) {
                    tParams = new double[divs + 1];
                    tParams[0] = nCrv.Domain.T0;
                    tParams[divs] = nCrv.Domain.T1;
                    for (int i = 1; i < divs; i++) {
                        if (nCrv.LengthParameter((length * i) / divs, out double t) && t > tParams[i-1]) {
                            tParams[i] = t;
                        } else {
                            tParams[i] = tParams[i-1] + 1e-5;
                        }
                    }
                }"""

content = content.replace(subdiv_old, subdiv_new)

# 2. Fix the Raycast skip bug and enforce strictly perpendicular embankments
raycast_old = """                    if (terrain != null)
                    {
                        double zTerrain = pt.Z, zLeftT = left.Z, zRightT = right.Z;
                        
                        Ray3d rC = new Ray3d(new Point3d(pt.X, pt.Y, pt.Z + 10000), -Vector3d.ZAxis);
                        double tC = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rC);
                        bool onTerrain = (tC >= 0.0);
                        if (onTerrain) zTerrain = rC.PointAt(tC).Z;

                        if (onTerrain)
                        {
                            Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                            if (tL >= 0.0) zLeftT = rL.PointAt(tL).Z;
                            else zLeftT = zTerrain; // Fallback to center terrain height to prevent 0-width embankments if raycast misses a tiny hole

                            Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                            if (tR >= 0.0) zRightT = rR.PointAt(tR).Z;
                            else zRightT = zTerrain;

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
                                if (zLeftT > left.Z + 0.1) {
                                    Vector3d dir = normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir));
                                    leftBlend = hit >= 0 ? left + dir * hit : new Point3d(left.X, left.Y, zLeftT);
                                } else if (zLeftT < left.Z - 0.1) {
                                    Vector3d dir = normal * Math.Cos(angleRad) - Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(left, dir));
                                    leftBlend = hit >= 0 ? left + dir * hit : new Point3d(left.X, left.Y, zLeftT);
                                }

                                Point3d rightBlend = right;
                                if (zRightT > right.Z + 0.1) {
                                    Vector3d dir = -normal * Math.Cos(angleRad) + Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir));
                                    rightBlend = hit >= 0 ? right + dir * hit : new Point3d(right.X, right.Y, zRightT);
                                } else if (zRightT < right.Z - 0.1) {
                                    Vector3d dir = -normal * Math.Cos(angleRad) - Vector3d.ZAxis * Math.Sin(angleRad);
                                    double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, new Ray3d(right, dir));
                                    rightBlend = hit >= 0 ? right + dir * hit : new Point3d(right.X, right.Y, zRightT);
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
                        }
                    }"""

raycast_new = """                    double zTerrain = pt.Z, zLeftT = left.Z, zRightT = right.Z;
                    
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

content = content.replace(raycast_old, raycast_new)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
