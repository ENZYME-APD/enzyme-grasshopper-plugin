using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Enzyme;

namespace Enzyme.Components
{
    public class WindEngine : GH_Component
    {
        public WindEngine()
            : base("Urban Wind Vector Engine", "WindEngine",
                "Simulates urban wind fields using terrain-parallel raycasting. Outputs a perfectly flat, crisp XY pixel-screen heatmap at a custom elevation.",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("WindEngine.png");
            }
        }

        public override Guid ComponentGuid => new Guid("C3D4E5F6-A7B8-490A-1B2C-3D4E5F6A7B8C");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ix = 220, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, ix, -150);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 20.0, 10.0, ix, -120);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 10.0, 1.5, ix, -90);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 10.0, 5.0, ix, -60);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), ox, -100);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", ox, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "point", ox, 100);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 6, System.Drawing.Color.FromArgb(230, 230, 230), ox, 140);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Global execution toggle switch", GH_ParamAccess.item, false);
            pManager.AddMeshParameter("TerrainMesh", "TerrainMesh", "The underlying site topography", GH_ParamAccess.item);
            pManager.AddMeshParameter("ContextBuildings", "ContextBuildings", "Lightweight mesh context structures", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddVectorParameter("WindDirection", "WindDirection", "Travel vector of incoming air", GH_ParamAccess.item, new Vector3d(1, 1, 0));
            pManager.AddNumberParameter("WindSpeed", "WindSpeed", "Baseline velocity metric", GH_ParamAccess.item, 10.0);
            pManager.AddNumberParameter("AnalysisHeight", "AnalysisHeight", "Human pedestrian offset", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("GridSpacing", "GridSpacing", "Resolution size of pixel elements", GH_ParamAccess.item, 5.0);
            pManager.AddColourParameter("CustomColors", "CustomColors", "Custom color spectrum override", GH_ParamAccess.list);
            pManager[7].Optional = true;
            pManager.AddNumberParameter("HeatmapHeight", "HeatmapHeight", "Z-axis elevation to project the flat heatmap", GH_ParamAccess.item);
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("VelocityHeatmap", "VelocityHeatmap", "Flat, unwelded crisp horizontal pixel-tile matrix", GH_ParamAccess.item);
            pManager.AddLineParameter("WindVectors", "WindVectors", "Spatial direction markers", GH_ParamAccess.list);
            pManager.AddColourParameter("VectorColors", "VectorColors", "Velocity color map matching lines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Streamlines", "Streamlines", "Continuous particle flow paths", GH_ParamAccess.list);
            pManager.AddTextParameter("VelocityData", "VelocityData", "Raw velocity values formatted", GH_ParamAccess.list);
            pManager.AddPointParameter("TagPoints", "TagPoints", "Anchor coordinates for Text Tag", GH_ParamAccess.list);
            pManager.AddMeshParameter("PlainMesh", "PlainMesh", "Original topography mesh without vertex colors", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool execute = false;
            DA.GetData(0, ref execute);

            Mesh terrain = null;
            DA.GetData(1, ref terrain);

            List<Mesh> buildings = new List<Mesh>();
            DA.GetDataList(2, buildings);

            Vector3d baseWindDir = new Vector3d(1, 1, 0);
            DA.GetData(3, ref baseWindDir);

            double speed = 10.0;
            DA.GetData(4, ref speed);

            double height = 1.5;
            DA.GetData(5, ref height);

            double spacing = 5.0;
            DA.GetData(6, ref spacing);

            List<Color> userColors = new List<Color>();
            DA.GetDataList(7, userColors);

            double heatmapHeight = 0.0;
            bool hasHeatmapHeight = DA.GetData(8, ref heatmapHeight);

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            Mesh heatmapMesh = new Mesh();
            List<Line> vectorLines = new List<Line>();
            List<Color> vectorColorList = new List<Color>();
            List<Color> meshColorList = new List<Color>(); 
            List<PolylineCurve> computedStreamlines = new List<PolylineCurve>();
            List<string> velocityTextData = new List<string>();
            List<Point3d> tagAnchorPoints = new List<Point3d>();

            double minObservedSpeed = double.MaxValue;
            double maxObservedSpeed = double.MinValue;
            int comfortablePointCount = 0;
            int activeSensorCount = 0;

            if (execute && terrain != null && baseWindDir.IsValid && speed > 0 && spacing > 0)
            {
                baseWindDir.Unitize();
                BoundingBox bbox = terrain.GetBoundingBox(true);
                
                List<Point3d> gridPoints = new List<Point3d>();
                List<Vector3d> topoDirs = new List<Vector3d>();
                List<double> baselineSpeeds = new List<double>();
                List<bool> solidMasks = new List<bool>();

                terrain.FaceNormals.ComputeFaceNormals();

                double currentX = bbox.Min.X;
                while (currentX <= bbox.Max.X)
                {
                    double currentY = bbox.Min.Y;
                    while (currentY <= bbox.Max.Y)
                    {
                        Point3d rayStart = new Point3d(currentX, currentY, bbox.Max.Z + 10.0);
                        Ray3d downRay = new Ray3d(rayStart, -Vector3d.ZAxis);
                        double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, downRay);
                        
                        if (hit >= 0.0)
                        {
                            Point3d exactSurfacePt = downRay.PointAt(hit);
                            Point3d pt = exactSurfacePt + new Vector3d(0, 0, height);
                            gridPoints.Add(pt);

                            Vector3d terrainNormal = Vector3d.ZAxis; 
                            MeshPoint mp = terrain.ClosestMeshPoint(exactSurfacePt, 0.1);
                            if (mp != null)
                            {
                                terrainNormal = new Vector3d(terrain.FaceNormals[mp.FaceIndex]);
                            }
                            terrainNormal.Unitize();
                            
                            Vector3d slopedWindDir = baseWindDir - (terrainNormal * (baseWindDir * terrainNormal));
                            
                            double localSpeed = speed;
                            if (slopedWindDir.Length > 0.001)
                            {
                                slopedWindDir.Unitize();
                                localSpeed *= (1.0 + (slopedWindDir.Z * 0.35)); 
                            }
                            else
                            {
                                slopedWindDir = baseWindDir;
                            }

                            topoDirs.Add(slopedWindDir);
                            baselineSpeeds.Add(localSpeed);
                        }
                        else
                        {
                            Point3d pt = new Point3d(currentX, currentY, bbox.Min.Z + height);
                            gridPoints.Add(pt);
                            topoDirs.Add(baseWindDir);
                            baselineSpeeds.Add(speed);
                        }
                        currentY += spacing;
                    }
                    currentX += spacing;
                }

                double maxShadowRange = speed * 8.0;

                for (int i = 0; i < gridPoints.Count; i++)
                {
                    Point3d pt = gridPoints[i];
                    Vector3d localDir = topoDirs[i];
                    double localSpeed = baselineSpeeds[i];

                    bool isInsideSolid = false;
                    foreach (Mesh building in buildings)
                    {
                        if (building != null && building.IsPointInside(pt, 0.01, false))
                        {
                            isInsideSolid = true;
                            break;
                        }
                    }

                    solidMasks.Add(isInsideSolid);

                    if (isInsideSolid)
                    {
                        localSpeed = 0.0;
                        localDir = Vector3d.Zero;
                    }
                    else
                    {
                        activeSensorCount++;

                        Ray3d backRay = new Ray3d(pt, -localDir);
                        double closestHit = double.MaxValue;

                        foreach (Mesh building in buildings)
                        {
                            if (building == null) continue;
                            double t = Rhino.Geometry.Intersect.Intersection.MeshRay(building, backRay);
                            if (t >= 0.0 && t < closestHit) closestHit = t;
                        }

                        if (closestHit < maxShadowRange)
                        {
                            double wakeIntensity = closestHit / maxShadowRange;
                            localSpeed *= Math.Max(0.12, wakeIntensity * wakeIntensity); 
                        }
                        else
                        {
                            foreach (Mesh building in buildings)
                            {
                                if (building == null) continue;
                                Point3d closestPt;
                                Vector3d normal;
                                int faceIdx = building.ClosestPoint(pt, out closestPt, out normal, speed * 2.5);

                                if (faceIdx >= 0 && closestPt.IsValid)
                                {
                                    double dist = pt.DistanceTo(closestPt);
                                    double infRadius = spacing * 2.5;

                                    if (dist < infRadius && dist > 0.001)
                                    {
                                        normal.Unitize();
                                        if (Math.Abs(normal * localDir) < 0.35) 
                                        {
                                            double blend = 1.0 - (dist / infRadius);
                                            Vector3d bypass = Vector3d.CrossProduct(normal, new Vector3d(0, 0, 1));
                                            if ((bypass * localDir) < 0) bypass = -bypass; 
                                            bypass.Unitize();

                                            localDir = (localDir * (1.0 - blend)) + (bypass * blend);
                                            localDir.Unitize();
                                            localSpeed *= (1.0 + (0.45 * blend)); 
                                        }
                                    }
                                }
                            }
                        }

                        if (localSpeed < minObservedSpeed) minObservedSpeed = localSpeed;
                        if (localSpeed > maxObservedSpeed) maxObservedSpeed = localSpeed;
                        if (localSpeed <= 5.0) comfortablePointCount++;
                    }

                    topoDirs[i] = localDir;
                    baselineSpeeds[i] = localSpeed;
                }

                double speedRange = maxObservedSpeed - minObservedSpeed;
                if (speedRange < 0.01) speedRange = 1.0;

                for (int i = 0; i < gridPoints.Count; i++)
                {
                    Point3d pt = gridPoints[i];
                    Vector3d localDir = topoDirs[i];
                    double localSpeed = baselineSpeeds[i];
                    bool isInsideSolid = solidMasks[i];

                    double intensity = (localSpeed - minObservedSpeed) / speedRange;
                    intensity = Math.Min(1.0, Math.Max(0.0, intensity));
                    Color mappedColor;

                    if (isInsideSolid)
                    {
                        mappedColor = Color.FromArgb(255, 12, 22, 52); 
                    }
                    else if (userColors.Count >= 2)
                    {
                        double position = intensity * (userColors.Count - 1);
                        int lowIdx = (int)Math.Floor(position);
                        int highIdx = (int)Math.Ceiling(position);
                        double blend = position - lowIdx;

                        Color c1 = userColors[lowIdx];
                        Color c2 = userColors[highIdx];

                        int r = (int)(c1.R * (1.0 - blend) + c2.R * blend);
                        int g = (int)(c1.G * (1.0 - blend) + c2.G * blend);
                        int b = (int)(c1.B * (1.0 - blend) + c2.B * blend);
                        mappedColor = Color.FromArgb(255, r, g, b);
                    }
                    else if (userColors.Count == 1)
                    {
                        mappedColor = userColors[0];
                    }
                    else
                    {
                        int r = (int)(15 * (1.0 - intensity) + 255 * intensity);
                        int g = (int)(45 * (1.0 - intensity) + 200 * intensity);
                        int b = (int)(120 * (1.0 - intensity) + 255 * intensity);
                        mappedColor = Color.FromArgb(255, r, g, b);
                    }
                    
                    meshColorList.Add(mappedColor);

                    if (localSpeed > 0.01)
                    {
                        vectorLines.Add(new Line(pt, localDir * (localSpeed * 0.5)));
                        velocityTextData.Add(localSpeed.ToString("F1"));
                        tagAnchorPoints.Add(pt);
                        vectorColorList.Add(mappedColor); 
                    }
                }

                double halfGrid = spacing * 0.5;
                
                double flatZ = bbox.Min.Z;
                if (hasHeatmapHeight)
                {
                    flatZ = heatmapHeight;
                }

                for (int i = 0; i < gridPoints.Count; i++)
                {
                    Point3d centerPt = gridPoints[i];
                    Color tileColor = meshColorList[i];

                    int vIndex = heatmapMesh.Vertices.Count;
                    
                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X - halfGrid, centerPt.Y - halfGrid, flatZ));
                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X + halfGrid, centerPt.Y - halfGrid, flatZ));
                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X + halfGrid, centerPt.Y + halfGrid, flatZ));
                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X - halfGrid, centerPt.Y + halfGrid, flatZ));

                    heatmapMesh.VertexColors.Add(tileColor);
                    heatmapMesh.VertexColors.Add(tileColor);
                    heatmapMesh.VertexColors.Add(tileColor);
                    heatmapMesh.VertexColors.Add(tileColor);

                    heatmapMesh.Faces.AddFace(vIndex, vIndex + 1, vIndex + 2, vIndex + 3);
                }
                
                int uCount = (int)Math.Ceiling((bbox.Max.X - bbox.Min.X) / spacing) + 1;
                for (int i = 0; i < gridPoints.Count; i += uCount * 2) 
                {
                    if (solidMasks[i]) continue; 

                    List<Point3d> pathVertices = new List<Point3d>();
                    Point3d trackingParticle = gridPoints[i];
                    pathVertices.Add(trackingParticle);

                    for (int step = 0; step < 50; step++) 
                    {
                        int closestIdx = -1;
                        double minDist = double.MaxValue;
                        for (int k = 0; k < gridPoints.Count; k++)
                        {
                            double d = trackingParticle.DistanceTo(gridPoints[k]);
                            if (d < minDist)
                            {
                                minDist = d;
                                closestIdx = k;
                            }
                        }

                        if (closestIdx != -1 && minDist < spacing * 2.0 && !solidMasks[closestIdx])
                        {
                            Vector3d stepVec = topoDirs[closestIdx];
                            if (stepVec.Length < 0.05) break; 

                            trackingParticle += stepVec * 0.2; 
                            pathVertices.Add(trackingParticle);
                        }
                        else
                        {
                            break; 
                        }
                    }

                    if (pathVertices.Count > 1)
                    {
                        computedStreamlines.Add(new PolylineCurve(pathVertices));
                    }
                }
            }

            DA.SetData(0, heatmapMesh);
            DA.SetDataList(1, vectorLines);
            DA.SetDataList(2, vectorColorList);
            DA.SetDataList(3, computedStreamlines);
            DA.SetDataList(4, velocityTextData);
            DA.SetDataList(5, tagAnchorPoints);

            if (terrain != null)
            {
                Mesh cleanMesh = terrain.DuplicateMesh();
                cleanMesh.VertexColors.Clear();
                DA.SetData(6, cleanMesh);
            }

            sw.Stop();
            if (execute)
            {
                double finalComfortPercent = activeSensorCount > 0 ? ((double)comfortablePointCount / activeSensorCount) * 100.0 : 0.0;
                Message = $"{this.NickName}\nTime: {sw.ElapsedMilliseconds} ms\n---\n● Min Speed: {(minObservedSpeed == double.MaxValue ? 0.0 : minObservedSpeed):F1} m/s\n○ Max Speed: {(maxObservedSpeed == double.MinValue ? 0.0 : maxObservedSpeed):F1} m/s\n● Comfort: {finalComfortPercent:F1}% (≤ 5.0 m/s)";
            }
            else
            {
                Message = $"{this.NickName}\nSTATUS: SLEEPING";
            }
        }
    }
}
