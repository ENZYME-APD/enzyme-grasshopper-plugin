import re

with open("Components/TerrainSections.cs", "r") as f:
    ts = f.read()

# For X sections loop:
# Replace:
# Point3d origin = rotPlane.PointAt(localBox.X.Min + stepX * i, localBox.Y.Mid, localBox.Z.Mid);
# Plane cutPlane = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);
# With:
# Point3d origin = rotPlane.PointAt(localBox.X.Mid, localBox.Y.Min + stepY * i, localBox.Z.Mid);
# Plane cutPlane = new Plane(origin, rotPlane.XAxis, rotPlane.ZAxis);

# But we need to use a step variable. Currently stepX is used for sectionsX loop.
# We'll just define stepYForX = localBox.Y.Length / (sectionsX + 1);
# Let's replace the whole block again, but this time use regex carefully.

start = ts.find("                        double stepX = 0;")
end = ts.find("            if (runBake)", start)

old_block = ts[start:end]

new_block = '''                        double stepYForX = 0;
                        double stepXForY = 0;
                        if (sectionsX > 0) stepYForX = localBox.Y.Length / (sectionsX + 1);
                        if (sectionsY > 0) stepXForY = localBox.X.Length / (sectionsY + 1);

                        if (sectionsX > 0)
                        {
                            for (int i = 1; i <= sectionsX; i++)
                            {
                                Point3d origin = rotPlane.PointAt(localBox.X.Mid, localBox.Y.Min + stepYForX * i, localBox.Z.Mid);
                                Plane cutPlane = new Plane(origin, rotPlane.XAxis, rotPlane.ZAxis);
                                
                                Polyline[] xSecs = Rhino.Geometry.Intersect.Intersection.MeshPlane(mesh, cutPlane);
                                if (xSecs != null && xSecs.Length > 0)
                                {
                                    string secId = $"X-SEC {i}";
                                    BoundingBox bbFlat = BoundingBox.Unset;
                                    Plane cutPlaneXDir = new Plane(origin, rotPlane.XAxis, rotPlane.ZAxis);
                                    Transform xformToWorld = Transform.PlaneToPlane(cutPlaneXDir, Plane.WorldXY);

                                    List<Curve> flatCrvs = new List<Curve>();
                                    List<Curve> validCrvs = new List<Curve>();
                                    
                                    foreach (var pl in xSecs)
                                    {
                                        if (pl.Count < 2) continue;
                                        Curve crv = pl.ToNurbsCurve();
                                        sectionOutlinesX.Append(new GH_Curve(crv), currentPath);
                                        validCrvs.Add(crv);

                                        if (layoutFlat)
                                        {
                                            Curve flatCrv = crv.DuplicateCurve();
                                            flatCrv.Transform(xformToWorld);
                                            
                                            var tempBB = flatCrv.GetBoundingBox(true);
                                            if (bbFlat.IsValid) bbFlat.Union(tempBB);
                                            else bbFlat = tempBB;
                                            
                                            flatCrvs.Add(flatCrv);
                                        }
                                    }

                                    if (validCrvs.Count > 0)
                                    {
                                        var firstCrv = validCrvs[0];
                                        var lastCrv = validCrvs[validCrvs.Count - 1];
                                        Point3d ptStart3D = firstCrv.PointAtStart - cutPlaneXDir.XAxis * 2.0;
                                        Point3d ptEnd3D = lastCrv.PointAtEnd + cutPlaneXDir.XAxis * 2.0;

                                        labelText3D.Append(new GH_String(secId), currentPath);
                                        labelText3D.Append(new GH_String(secId), currentPath);
                                        labelPoints3D.Append(new GH_Point(ptStart3D), currentPath);
                                        labelPoints3D.Append(new GH_Point(ptEnd3D), currentPath);

                                        if (layoutFlat)
                                        {
                                            var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorYXSecs - bbFlat.Max.Y, 0));
                                            foreach (var flatCrv in flatCrvs)
                                            {
                                                flatCrv.Transform(xformMove);
                                                flatSectionsX.Append(new GH_Curve(flatCrv), currentPath);
                                            }

                                            Point3d ptStartFlat = new Point3d(ptStart3D);
                                            Point3d ptEndFlat = new Point3d(ptEnd3D);
                                            ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove);
                                            ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove);

                                            labelTextFlat.Append(new GH_String(secId), currentPath);
                                            labelTextFlat.Append(new GH_String(secId), currentPath);
                                            labelPointsFlat.Append(new GH_Point(ptStartFlat), currentPath);
                                            labelPointsFlat.Append(new GH_Point(ptEndFlat), currentPath);

                                            string meta = $"{{\\"id\\": \\"{secId}\\", \\"plane_origin\\": \\"{origin}\\", \\"direction\\": \\"X_Section\\"}}";
                                            sectionMetadata.Append(new GH_String(meta), currentPath);

                                            cursorYXSecs -= ((bbFlat.Max.Y - bbFlat.Min.Y) + globalBB.Diagonal.Length * 0.05);
                                        }
                                    }
                                }
                            }
                        }

                        if (sectionsY > 0)
                        {
                            for (int i = 1; i <= sectionsY; i++)
                            {
                                Point3d origin = rotPlane.PointAt(localBox.X.Min + stepXForY * i, localBox.Y.Mid, localBox.Z.Mid);
                                Plane cutPlane = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);
                                
                                Polyline[] ySecs = Rhino.Geometry.Intersect.Intersection.MeshPlane(mesh, cutPlane);
                                if (ySecs != null && ySecs.Length > 0)
                                {
                                    string secId = $"Y-SEC {i}";
                                    BoundingBox bbFlat = BoundingBox.Unset;
                                    Plane cutPlaneYDir = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);
                                    Plane targetPlaneY = Plane.WorldXY;
                                    targetPlaneY.Rotate(Math.PI / 2, Rhino.Geometry.Vector3d.ZAxis);
                                    Transform xformToWorld = Transform.PlaneToPlane(cutPlaneYDir, targetPlaneY);

                                    List<Curve> flatCrvs = new List<Curve>();
                                    List<Curve> validCrvs = new List<Curve>();
                                    
                                    foreach (var pl in ySecs)
                                    {
                                        if (pl.Count < 2) continue;
                                        Curve crv = pl.ToNurbsCurve();
                                        sectionOutlinesY.Append(new GH_Curve(crv), currentPath);
                                        validCrvs.Add(crv);

                                        if (layoutFlat)
                                        {
                                            Curve flatCrv = crv.DuplicateCurve();
                                            flatCrv.Transform(xformToWorld);
                                            
                                            var tempBB = flatCrv.GetBoundingBox(true);
                                            if (bbFlat.IsValid) bbFlat.Union(tempBB);
                                            else bbFlat = tempBB;
                                            
                                            flatCrvs.Add(flatCrv);
                                        }
                                    }

                                    if (validCrvs.Count > 0)
                                    {
                                        var firstCrv = validCrvs[0];
                                        var lastCrv = validCrvs[validCrvs.Count - 1];
                                        Point3d ptStart3D = firstCrv.PointAtStart - cutPlaneYDir.XAxis * 2.0;
                                        Point3d ptEnd3D = lastCrv.PointAtEnd + cutPlaneYDir.XAxis * 2.0;

                                        labelText3D.Append(new GH_String(secId), currentPath);
                                        labelText3D.Append(new GH_String(secId), currentPath);
                                        labelPoints3D.Append(new GH_Point(ptStart3D), currentPath);
                                        labelPoints3D.Append(new GH_Point(ptEnd3D), currentPath);

                                        if (layoutFlat)
                                        {
                                            var xformMove = Transform.Translation(new Vector3d(cursorXYSecs - bbFlat.Max.X, globalBB.Min.Y - bbFlat.Min.Y, 0));
                                            foreach (var flatCrv in flatCrvs)
                                            {
                                                flatCrv.Transform(xformMove);
                                                flatSectionsY.Append(new GH_Curve(flatCrv), currentPath);
                                            }

                                            Point3d ptStartFlat = new Point3d(ptStart3D);
                                            Point3d ptEndFlat = new Point3d(ptEnd3D);
                                            ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove);
                                            ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove);

                                            labelTextFlat.Append(new GH_String(secId), currentPath);
                                            labelTextFlat.Append(new GH_String(secId), currentPath);
                                            labelPointsFlat.Append(new GH_Point(ptStartFlat), currentPath);
                                            labelPointsFlat.Append(new GH_Point(ptEndFlat), currentPath);

                                            string meta = $"{{\\"id\\": \\"{secId}\\", \\"plane_origin\\": \\"{origin}\\", \\"direction\\": \\"Y_Section\\"}}";
                                            sectionMetadata.Append(new GH_String(meta), currentPath);

                                            cursorXYSecs -= ((bbFlat.Max.X - bbFlat.Min.X) + globalBB.Diagonal.Length * 0.05);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

'''

ts = ts.replace(old_block, new_block)

with open("Components/TerrainSections.cs", "w") as f:
    f.write(ts)

