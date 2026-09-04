import re

with open("Components/TerrainSections.cs", "r") as f:
    ts = f.read()

# 1. Replace globalBB logic
global_bb_old = '''            BoundingBox globalBB = BoundingBox.Empty;
            foreach (var path in targetMeshes.Paths)
            {
                foreach (var obj in targetMeshes.get_Branch(path))
                {
                    var ghMesh = obj as GH_Mesh;
                    if (ghMesh != null && ghMesh.Value != null && ghMesh.Value.IsValid)
                        globalBB.Union(ghMesh.Value.GetBoundingBox(true));
                }
            }'''

global_bb_new = '''            BoundingBox globalRotBB = BoundingBox.Empty;
            Transform xformToRot = Transform.ChangeBasis(Rhino.Geometry.Plane.WorldXY, rotPlane);
            foreach (var path in targetMeshes.Paths)
            {
                foreach (var obj in targetMeshes.get_Branch(path))
                {
                    var ghMesh = obj as GH_Mesh;
                    if (ghMesh != null && ghMesh.Value != null && ghMesh.Value.IsValid)
                    {
                        var m = ghMesh.Value.DuplicateMesh();
                        m.Transform(xformToRot);
                        globalRotBB.Union(m.GetBoundingBox(true));
                    }
                }
            }
            
            Rhino.Geometry.Vector3d layoutX = rotPlane.XAxis;
            layoutX.Z = 0;
            if (layoutX.Length < 1e-6) layoutX = Rhino.Geometry.Vector3d.XAxis;
            layoutX.Unitize();
            Rhino.Geometry.Vector3d layoutY = Rhino.Geometry.Vector3d.CrossProduct(Rhino.Geometry.Vector3d.ZAxis, layoutX);
            Rhino.Geometry.Point3d layoutOrigin = new Rhino.Geometry.Point3d(rotPlane.Origin.X, rotPlane.Origin.Y, 0);
            Rhino.Geometry.Plane layoutPlane = new Rhino.Geometry.Plane(layoutOrigin, layoutX, layoutY);
            Rhino.Geometry.Transform finalLayoutXform = Rhino.Geometry.Transform.PlaneToPlane(Rhino.Geometry.Plane.WorldXY, layoutPlane);
            '''

ts = ts.replace(global_bb_old, global_bb_new)

# 2. Replace globalBB with globalRotBB for padding and cursors
cursors_old = '''                        double padding = globalBB.IsValid ? globalBB.Diagonal.Length * 0.05 : 10.0;
                        double cursorYXSecs = globalBB.IsValid ? globalBB.Max.Y + padding : padding;
                        double cursorXYSecs = globalBB.IsValid ? globalBB.Max.X + padding : padding;'''

cursors_new = '''                        double padding = globalRotBB.IsValid ? globalRotBB.Diagonal.Length * 0.05 : 10.0;
                        double cursorYXSecs = globalRotBB.IsValid ? globalRotBB.Max.Y + padding : padding;
                        double cursorXYSecs = globalRotBB.IsValid ? globalRotBB.Max.X + padding : padding;'''

ts = ts.replace(cursors_old, cursors_new)

# 3. Apply finalLayoutXform inside layoutFlat for X sections
x_trans_old = '''                                            var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorYXSecs - bbFlat.Min.Y, 0));
                                            foreach (var flatCrv in flatCrvs)
                                            {
                                                flatCrv.Transform(xformMove);
                                                flatSectionsX.Append(new GH_Curve(flatCrv), currentPath);
                                            }

                                            Point3d ptStartFlat = new Point3d(ptStart3D);
                                            Point3d ptEndFlat = new Point3d(ptEnd3D);
                                            ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove);
                                            ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove);'''

x_trans_new = '''                                            var xformMove = Transform.Translation(new Vector3d(globalRotBB.Min.X - bbFlat.Min.X, cursorYXSecs - bbFlat.Min.Y, 0));
                                            foreach (var flatCrv in flatCrvs)
                                            {
                                                flatCrv.Transform(xformMove);
                                                flatCrv.Transform(finalLayoutXform);
                                                flatSectionsX.Append(new GH_Curve(flatCrv), currentPath);
                                            }

                                            Point3d ptStartFlat = new Point3d(ptStart3D);
                                            Point3d ptEndFlat = new Point3d(ptEnd3D);
                                            ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove); ptStartFlat.Transform(finalLayoutXform);
                                            ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove); ptEndFlat.Transform(finalLayoutXform);'''

ts = ts.replace(x_trans_old, x_trans_new)

# 4. Apply finalLayoutXform inside layoutFlat for Y sections
y_trans_old = '''                                            var xformMove = Transform.Translation(new Vector3d(cursorXYSecs - bbFlat.Min.X, globalBB.Min.Y - bbFlat.Min.Y, 0));
                                            foreach (var flatCrv in flatCrvs)
                                            {
                                                flatCrv.Transform(xformMove);
                                                flatSectionsY.Append(new GH_Curve(flatCrv), currentPath);
                                            }

                                            Point3d ptStartFlat = new Point3d(ptStart3D);
                                            Point3d ptEndFlat = new Point3d(ptEnd3D);
                                            ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove);
                                            ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove);'''

y_trans_new = '''                                            var xformMove = Transform.Translation(new Vector3d(cursorXYSecs - bbFlat.Min.X, globalRotBB.Min.Y - bbFlat.Min.Y, 0));
                                            foreach (var flatCrv in flatCrvs)
                                            {
                                                flatCrv.Transform(xformMove);
                                                flatCrv.Transform(finalLayoutXform);
                                                flatSectionsY.Append(new GH_Curve(flatCrv), currentPath);
                                            }

                                            Point3d ptStartFlat = new Point3d(ptStart3D);
                                            Point3d ptEndFlat = new Point3d(ptEnd3D);
                                            ptStartFlat.Transform(xformToWorld); ptStartFlat.Transform(xformMove); ptStartFlat.Transform(finalLayoutXform);
                                            ptEndFlat.Transform(xformToWorld); ptEndFlat.Transform(xformMove); ptEndFlat.Transform(finalLayoutXform);'''

ts = ts.replace(y_trans_old, y_trans_new)

with open("Components/TerrainSections.cs", "w") as f:
    f.write(ts)

