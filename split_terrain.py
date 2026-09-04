import re

with open("Components/MeshHeightAnalysis.cs", "r") as f:
    orig = f.read()

# Make TerrainSections.cs
sections_code = orig.replace("MeshHeightAnalysis", "TerrainSections")
sections_code = sections_code.replace('base("Terrain Analyzer",', 'base("Terrain Sections",')
sections_code = sections_code.replace('IconLoader.Load("MeshHeightAnalisys.png")', 'null') # don't have icon for sections yet
sections_code = sections_code.replace('8F1604B0-C27B-4966-9FC9-5DE911C3E20F', '8F1604B0-C27B-4966-9FC9-5DE911C3E21F') # new guid

# Update RegisterInputParams for Sections
input_params_sec = """        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "M", "The meshes to section.", GH_ParamAccess.tree);
            pManager.AddPlaneParameter("RotationPlane", "RP", "Orientation plane for the bounding box sectioning.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("SectionsX", "SX", "Number of sections running parallel to the X-axis.", GH_ParamAccess.item, 5);
            pManager.AddIntegerParameter("SectionsY", "SY", "Number of sections running parallel to the Y-axis.", GH_ParamAccess.item, 5);
            pManager.AddBooleanParameter("LayoutFlat", "LF", "Toggle to generate 2D XY print layouts next to the mesh.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Bake", "B", "Bake trigger", GH_ParamAccess.item, false);
            pManager.AddTextParameter("BakeName", "BN", "Bake group/layer name", GH_ParamAccess.item, "TerrainSections");
        }"""
sections_code = re.sub(r'protected override void RegisterInputParams.*?\}', input_params_sec, sections_code, flags=re.DOTALL, count=1)

output_params_sec = """        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("SectionOutlinesX", "SOX", "3D Polylines running parallel to the X-axis.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("SectionOutlinesY", "SOY", "3D Polylines running parallel to the Y-axis.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsX", "FSX", "2D X-Sections stacked downwards (-Y direction).", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsY", "FSY", "2D Y-Sections stacked leftwards (-X direction).", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelText3D", "LT3D", "Text strings for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPoints3D", "LP3D", "Points for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelTextFlat", "LTF", "Text strings for the flattened section layout.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPointsFlat", "LPF", "Points for the flattened section layout.", GH_ParamAccess.tree);
            pManager.AddTextParameter("SectionMetadata", "SM", "Dictionary keys containing spatial transform & ID data.", GH_ParamAccess.tree);
        }"""
sections_code = re.sub(r'protected override void RegisterOutputParams.*?\}', output_params_sec, sections_code, flags=re.DOTALL, count=1)

solve_instance_sec = """        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var t_start = System.Diagnostics.Stopwatch.StartNew();
            
            GH_Structure<GH_Mesh> targetMeshes = new GH_Structure<GH_Mesh>();
            if (!DA.GetDataTree(0, out targetMeshes)) return;

            Plane rotPlane = Plane.WorldXY;
            DA.GetData(1, ref rotPlane);

            int sectionsX = 5;
            DA.GetData(2, ref sectionsX);

            int sectionsY = 5;
            DA.GetData(3, ref sectionsY);

            bool layoutFlat = false;
            DA.GetData(4, ref layoutFlat);
            
            bool runBake = false;
            DA.GetData(5, ref runBake);
            
            string bakeName = "TerrainSections";
            DA.GetData(6, ref bakeName);

            GH_Structure<GH_Curve> sectionOutlinesX = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Curve> sectionOutlinesY = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Curve> flatSectionsX = new GH_Structure<GH_Curve>();
            GH_Structure<GH_Curve> flatSectionsY = new GH_Structure<GH_Curve>();

            GH_Structure<GH_String> labelText3D = new GH_Structure<GH_String>();
            GH_Structure<GH_Point> labelPoints3D = new GH_Structure<GH_Point>();
            GH_Structure<GH_String> labelTextFlat = new GH_Structure<GH_String>();
            GH_Structure<GH_Point> labelPointsFlat = new GH_Structure<GH_Point>();
            GH_Structure<GH_String> sectionMetadata = new GH_Structure<GH_String>();

            int totalSectionsX = 0;
            int totalSectionsY = 0;

            for (int pathIdx = 0; pathIdx < targetMeshes.Paths.Count; pathIdx++)
            {
                GH_Path currentPath = targetMeshes.Paths[pathIdx];
                var meshBranch = targetMeshes.Branches[pathIdx];

                foreach (var ghMesh in meshBranch)
                {
                    if (ghMesh == null || ghMesh.Value == null) continue;
                    Mesh mesh = ghMesh.Value;

                    if (sectionsX > 0 || sectionsY > 0)
                    {
                        BoundingBox globalBB = mesh.GetBoundingBox(true);
                        Box localBox;
                        mesh.GetBoundingBox(rotPlane, out localBox);

                        double lenX = localBox.X.Length;
                        double lenY = localBox.Y.Length;
                        
                        double cursorXSecs = globalBB.Max.Y + 20.0;
                        double cursorXYSecs = globalBB.Min.X - 20.0;

                        if (sectionsX > 0)
                        {
                            double stepX = lenX / (sectionsX + 1);
                            for (int i = 1; i <= sectionsX; i++)
                            {
                                Point3d origin = rotPlane.PointAt(localBox.X.Min + stepX * i, localBox.Y.Center, localBox.Z.Center);
                                Plane cutPlane = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);
                                
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
                                        totalSectionsX++;

                                        if (layoutFlat)
                                        {
                                            Curve flatCrv = crv.DuplicateCurve();
                                            flatCrv.Transform(xformToWorld);
                                            bbFlat.Union(flatCrv.GetBoundingBox(true));
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
                                            var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorXSecs - bbFlat.Max.Y, 0));
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
                                            
                                            double padding = 10.0;
                                            cursorXSecs -= ((bbFlat.Max.Y - bbFlat.Min.Y) + padding);
                                        }
                                    }
                                }
                            }
                        }

                        if (sectionsY > 0)
                        {
                            double stepY = lenY / (sectionsY + 1);
                            for (int i = 1; i <= sectionsY; i++)
                            {
                                Point3d origin = rotPlane.PointAt(localBox.X.Center, localBox.Y.Min + stepY * i, localBox.Z.Center);
                                Plane cutPlane = new Plane(origin, rotPlane.XAxis, rotPlane.ZAxis);
                                
                                Polyline[] ySecs = Rhino.Geometry.Intersect.Intersection.MeshPlane(mesh, cutPlane);
                                if (ySecs != null && ySecs.Length > 0)
                                {
                                    string secId = $"Y-SEC {i}";
                                    BoundingBox bbFlat = BoundingBox.Unset;
                                    Plane cutPlaneYDir = new Plane(origin, rotPlane.YAxis, rotPlane.ZAxis);
                                    Transform xformToWorld = Transform.PlaneToPlane(cutPlaneYDir, Plane.WorldXY);

                                    List<Curve> flatCrvs = new List<Curve>();
                                    List<Curve> validCrvs = new List<Curve>();
                                    double padding = 10.0;

                                    foreach (var pl in ySecs)
                                    {
                                        if (pl.Count < 2) continue;
                                        Curve crv = pl.ToNurbsCurve();
                                        sectionOutlinesY.Append(new GH_Curve(crv), currentPath);
                                        validCrvs.Add(crv);
                                        totalSectionsY++;

                                        if (layoutFlat)
                                        {
                                            Curve flatCrv = crv.DuplicateCurve();
                                            flatCrv.Transform(xformToWorld);
                                            bbFlat.Union(flatCrv.GetBoundingBox(true));
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

                                            cursorXYSecs -= ((bbFlat.Max.X - bbFlat.Min.X) + padding);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (runBake)
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    int items_replaced = 0;
                    if (!string.IsNullOrEmpty(bakeName))
                    {
                        var existing_objs = doc.Objects.FindByUserString("ElefrontBakeName", bakeName, false);
                        if (existing_objs != null && existing_objs.Length > 0)
                        {
                            foreach (var obj in existing_objs)
                            {
                                doc.Objects.Delete(obj.Id, true);
                                items_replaced++;
                            }
                        }
                    }

                    string parent_name = "TerrainSections";
                    int parent_idx = doc.Layers.Find(parent_name, true);
                    if (parent_idx < 0)
                    {
                        var parent_layer = new Rhino.DocObjects.Layer();
                        parent_layer.Name = parent_name;
                        parent_idx = doc.Layers.Add(parent_layer);
                    }
                    
                    var groupIndex = doc.Groups.Add(bakeName);

                    for (int i = 0; i < sectionOutlinesX.Branches.Count; i++)
                    {
                        var branch = sectionOutlinesX.Branches[i];
                        foreach (var ghCrv in branch)
                        {
                            var crv = ghCrv.Value;
                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = parent_idx;
                            if (!string.IsNullOrEmpty(bakeName)) attr.SetUserString("ElefrontBakeName", bakeName);
                            attr.AddToGroup(groupIndex);
                            doc.Objects.AddCurve(crv, attr);
                        }
                    }
                    for (int i = 0; i < sectionOutlinesY.Branches.Count; i++)
                    {
                        var branch = sectionOutlinesY.Branches[i];
                        foreach (var ghCrv in branch)
                        {
                            var crv = ghCrv.Value;
                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = parent_idx;
                            if (!string.IsNullOrEmpty(bakeName)) attr.SetUserString("ElefrontBakeName", bakeName);
                            attr.AddToGroup(groupIndex);
                            doc.Objects.AddCurve(crv, attr);
                        }
                    }
                }
            }

            DA.SetDataTree(0, sectionOutlinesX);
            DA.SetDataTree(1, sectionOutlinesY);
            DA.SetDataTree(2, flatSectionsX);
            DA.SetDataTree(3, flatSectionsY);
            DA.SetDataTree(4, labelText3D);
            DA.SetDataTree(5, labelPoints3D);
            DA.SetDataTree(6, labelTextFlat);
            DA.SetDataTree(7, labelPointsFlat);
            DA.SetDataTree(8, sectionMetadata);
            
            t_start.Stop();
            string layoutStatus = layoutFlat ? "ON" : "OFF";
            Message = $"TERRAIN SECTIONS\\nTime: {t_start.ElapsedMilliseconds:F2} ms\\n---\\nSections X: {totalSectionsX}\\nSections Y: {totalSectionsY}\\nXY Layout: {layoutStatus}";
        }"""
        
# We need to drop GetTopoNeighbors and ComputeHeatmapColor since we don't use them in sections.
sections_code = re.sub(r'protected override void SolveInstance.*?\}[\s\n]*private HashSet<int> GetTopoNeighbors', solve_instance_sec + '\n\n        private HashSet<int> GetTopoNeighbors', sections_code, flags=re.DOTALL)
sections_code = re.sub(r'private HashSet<int> GetTopoNeighbors.*?\}[\s\n]*private Color ComputeHeatmapColor.*?\}', '', sections_code, flags=re.DOTALL)
sections_code = sections_code.replace("public override void AddedToDocument(GH_Document document)", "public override void AddedToDocument(GH_Document document)\n        {\n            base.AddedToDocument(document);\n        }\n\n        public void OldAddedToDocument()")

with open("Components/TerrainSections.cs", "w") as f:
    f.write(sections_code)
