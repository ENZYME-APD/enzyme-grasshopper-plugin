using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Terrain
{
    public class TerrainGeneratorProComponent : GH_Component
    {
        public TerrainGeneratorProComponent()
          : base("Terrain Generator Pro", "TRN-P",
              "Generates topography with noise-masked procedural forest scattering and strict elevation limits.",
              "Enzyme", "Terrain")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "Boundary", "Closed boundary limits", GH_ParamAccess.tree);
            pManager.AddNumberParameter("MaxHeight", "MaxHeight", "Maximum elevation in meters", GH_ParamAccess.tree, 100.0);
            pManager.AddNumberParameter("MinHeight", "MinHeight", "Minimum elevation in meters", GH_ParamAccess.tree, 0.0);
            pManager.AddIntegerParameter("Seed", "Seed", "Randomization seed", GH_ParamAccess.tree, 42);
            pManager.AddNumberParameter("PatternSizeXY", "PatternSizeXY", "List of feature sizes in meters", GH_ParamAccess.tree, 500.0);
            pManager.AddNumberParameter("PatternHeightZ", "PatternHeightZ", "List of relative feature strengths", GH_ParamAccess.tree, 1.0);
            pManager.AddNumberParameter("ContourStep", "ContourStep", "Interval for normal contour lines", GH_ParamAccess.tree, 1.0);
            pManager.AddNumberParameter("MainStep", "MainStep", "Interval for main contour lines", GH_ParamAccess.tree, 5.0);
            pManager.AddColourParameter("Colors", "Colors", "List of gradient colors based on height", GH_ParamAccess.tree, Color.LightGreen);
            pManager.AddIntegerParameter("Resolution", "Resolution", "Grid density, default is 100", GH_ParamAccess.tree, 100);
            pManager.AddBooleanParameter("UseSlopeColor", "UseSlopeColor", "Toggle steep slope coloring", GH_ParamAccess.tree, false);
            pManager.AddColourParameter("SlopeColor", "SlopeColor", "Color applied to sheer cliffs/slopes", GH_ParamAccess.tree, Color.DarkGray);
            pManager.AddNumberParameter("SlopeAngle", "SlopeAngle", "Angle where slope color starts", GH_ParamAccess.tree, 30.0);
            pManager.AddIntegerParameter("TerrainStyle", "TerrainStyle", "0 = Realistic Soft Hills, 1 = Ridged/Cellular Pattern", GH_ParamAccess.tree, 0);
            pManager.AddBooleanParameter("Solid", "Solid", "Toggle closed mesh extrusion", GH_ParamAccess.tree, false);
            pManager.AddColourParameter("BaseCol", "BaseCol", "Color for the extruded solid base section", GH_ParamAccess.tree, Color.DimGray);
            pManager.AddNumberParameter("TreeMsk", "TreeMsk", "Coverage mask threshold 0.0 to 1.0", GH_ParamAccess.tree, 0.0);
            pManager.AddNumberParameter("TreeDns", "TreeDns", "Density multiplier inside mask areas", GH_ParamAccess.tree, 0.0);
            pManager.AddIntegerParameter("TreeSeed", "TreeSeed", "Dedicated seed for the forest noise map", GH_ParamAccess.tree, 12345);
            pManager.AddNumberParameter("TreeZMin", "TreeZMin", "Minimum relative elevation for trees 0.0 to 1.0", GH_ParamAccess.tree, 0.15);
            pManager.AddNumberParameter("TreeZMax", "TreeZMax", "Maximum relative elevation for trees 0.0 to 1.0", GH_ParamAccess.tree, 0.85);

            for (int i = 1; i < pManager.ParamCount; i++)
            {
                pManager[i].Optional = true;
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions", "Instructions", "Interface mapping guide", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "Mesh", "Gradient colored terrain geometry", GH_ParamAccess.tree);
            pManager.AddCurveParameter("NormContours", "NormContours", "Standard contour lines", GH_ParamAccess.tree);
            pManager.AddCurveParameter("MainContours", "MainContours", "Major interval contour lines", GH_ParamAccess.tree);
            pManager.AddPointParameter("Trees", "Trees", "Scattered point coordinates for trees", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            
            GH_Structure<GH_Curve> boundaryTree;
            if (!DA.GetDataTree(0, out boundaryTree)) return;

            GH_Structure<GH_Number> maxHeightTree; DA.GetDataTree(1, out maxHeightTree);
            GH_Structure<GH_Number> minHeightTree; DA.GetDataTree(2, out minHeightTree);
            GH_Structure<GH_Integer> seedTree; DA.GetDataTree(3, out seedTree);
            GH_Structure<GH_Number> sizeTree; DA.GetDataTree(4, out sizeTree);
            GH_Structure<GH_Number> weightTree; DA.GetDataTree(5, out weightTree);
            GH_Structure<GH_Number> cStepTree; DA.GetDataTree(6, out cStepTree);
            GH_Structure<GH_Number> mStepTree; DA.GetDataTree(7, out mStepTree);
            GH_Structure<GH_Colour> colorsTree; DA.GetDataTree(8, out colorsTree);
            GH_Structure<GH_Integer> resTree; DA.GetDataTree(9, out resTree);
            GH_Structure<GH_Boolean> useSlopeTree; DA.GetDataTree(10, out useSlopeTree);
            GH_Structure<GH_Colour> slopeColTree; DA.GetDataTree(11, out slopeColTree);
            GH_Structure<GH_Number> slopeAngTree; DA.GetDataTree(12, out slopeAngTree);
            GH_Structure<GH_Integer> styleTree; DA.GetDataTree(13, out styleTree);
            GH_Structure<GH_Boolean> solidTree; DA.GetDataTree(14, out solidTree);
            GH_Structure<GH_Colour> baseColTree; DA.GetDataTree(15, out baseColTree);
            GH_Structure<GH_Number> treeMskTree; DA.GetDataTree(16, out treeMskTree);
            GH_Structure<GH_Number> treeDnsTree; DA.GetDataTree(17, out treeDnsTree);
            GH_Structure<GH_Integer> treeSeedTree; DA.GetDataTree(18, out treeSeedTree);
            GH_Structure<GH_Number> treeZMinTree; DA.GetDataTree(19, out treeZMinTree);
            GH_Structure<GH_Number> treeZMaxTree; DA.GetDataTree(20, out treeZMaxTree);

            var meshOut = new GH_Structure<GH_Mesh>();
            var normContoursOut = new GH_Structure<GH_Curve>();
            var mainContoursOut = new GH_Structure<GH_Curve>();
            var treesOut = new GH_Structure<GH_Point>();

            int totalItems = 0;
            int totalTrees = 0;
            double totalArea = 0.0;
            double globalMinH = double.PositiveInfinity;
            double globalMaxH = double.NegativeInfinity;

            Random rand = new Random();

            for (int bIdx = 0; bIdx < boundaryTree.PathCount; bIdx++)
            {
                GH_Path path = boundaryTree.Paths[bIdx];
                var boundaries = boundaryTree.get_Branch(path);
                if (boundaries == null || boundaries.Count == 0) continue;

                double maxH = GetSafeTreeItem(maxHeightTree, bIdx, 0, 100.0);
                double minH = GetSafeTreeItem(minHeightTree, bIdx, 0, 0.0);
                int seed = GetSafeTreeItem(seedTree, bIdx, 0, 42);
                int res = GetSafeTreeItem(resTree, bIdx, 0, 100);
                double cStep = GetSafeTreeItem(cStepTree, bIdx, 0, 1.0);
                double mStep = GetSafeTreeItem(mStepTree, bIdx, 0, 5.0);
                
                bool useSlope = GetSafeTreeItem(useSlopeTree, bIdx, 0, false);
                Color slopeCol = GetSafeTreeItem(slopeColTree, bIdx, 0, Color.DarkGray);
                double slopeAngle = GetSafeTreeItem(slopeAngTree, bIdx, 0, 30.0);
                int tStyle = GetSafeTreeItem(styleTree, bIdx, 0, 0);
                bool makeSolid = GetSafeTreeItem(solidTree, bIdx, 0, false);
                Color baseCol = GetSafeTreeItem(baseColTree, bIdx, 0, Color.DimGray);
                
                double maskVal = Math.Max(0.0, Math.Min(1.0, GetSafeTreeItem(treeMskTree, bIdx, 0, 0.0)));
                double densVal = Math.Max(0.0, GetSafeTreeItem(treeDnsTree, bIdx, 0, 0.0));
                
                int treeSeed = GetSafeTreeItem(treeSeedTree, bIdx, 0, 12345);
                double treeZMin = Math.Max(0.0, Math.Min(1.0, GetSafeTreeItem(treeZMinTree, bIdx, 0, 0.15)));
                double treeZMax = Math.Max(0.0, Math.Min(1.0, GetSafeTreeItem(treeZMaxTree, bIdx, 0, 0.85)));
                
                List<double> sizes = GetSafeTreeList(sizeTree, bIdx, new List<double> { 500.0, 150.0, 30.0 });
                List<double> weights = GetSafeTreeList(weightTree, bIdx, new List<double> { 1.0, 0.3, 0.05 });
                List<Color> colors = GetSafeTreeList(colorsTree, bIdx, new List<Color> { Color.LightGreen, Color.SaddleBrown, Color.White });
                int octaves = Math.Max(sizes.Count, weights.Count);

                foreach (var curveGoo in boundaries)
                {
                    var ghCurve = curveGoo as GH_Curve;
                    Curve curve = ghCurve?.Value;
                    if (curve == null || !curve.IsClosed) continue;
                    
                    BoundingBox bbox = curve.GetBoundingBox(true);
                    double w = bbox.Max.X - bbox.Min.X;
                    double h = bbox.Max.Y - bbox.Min.Y;
                    if (w <= 0 || h <= 0) continue;
                    
                    var amp = AreaMassProperties.Compute(curve);
                    if (amp != null) totalArea += amp.Area;
                    
                    int nx = Math.Max(2, res);
                    int ny = Math.Max(2, (int)(nx * (h / w)));
                    double gridStep = w / nx;
                    
                    Curve flatCrv = curve.DuplicateCurve();
                    flatCrv.Translate(new Vector3d(0, 0, -bbox.Min.Z));
                    double crvLength = flatCrv.GetLength();
                    int divCount = Math.Max(4, (int)(crvLength / gridStep));
                    
                    Point3d[] ptsBndArr;
                    double[] tVals = flatCrv.DivideByCount(divCount, true, out ptsBndArr);
                    List<Point3d> ptsBnd = new List<Point3d>();
                    if (ptsBndArr != null && ptsBndArr.Length > 0)
                    {
                        ptsBnd = ptsBndArr.ToList();
                    }
                    else
                    {
                        Polyline nc = flatCrv.ToPolyline(0.01, 0.1, 0.0, 0.0)?.ToPolyline();
                        if (nc != null) ptsBnd = nc.ToList();
                    }
                        
                    if (ptsBnd.Count > 0 && ptsBnd[0].DistanceTo(ptsBnd[ptsBnd.Count - 1]) > 0.001)
                    {
                        ptsBnd.Add(ptsBnd[0]);
                    }
                        
                    if (ptsBnd.Count == 0) continue;
                        
                    List<Point3d> netAllPts = new List<Point3d>(ptsBnd);
                    
                    double minDist = gridStep * 0.35;
                    for (int j = 0; j <= ny; j++)
                    {
                        double y = bbox.Min.Y + ((double)j / ny) * h;
                        for (int i = 0; i <= nx; i++)
                        {
                            double x = bbox.Min.X + ((double)i / nx) * w;
                            Point3d pt = new Point3d(x, y, 0);
                            if (flatCrv.Contains(pt, Plane.WorldXY, 0.01) == PointContainment.Inside)
                            {
                                double t;
                                if (flatCrv.ClosestPoint(pt, out t) && pt.DistanceTo(flatCrv.PointAt(t)) > minDist)
                                {
                                    netAllPts.Add(pt);
                                }
                            }
                        }
                    }
                            
                    Mesh mesh = Mesh.CreateFromTessellation(netAllPts, new List<IEnumerable<Point3d>> { ptsBnd }, Plane.WorldXY, false);
                    if (mesh == null || !mesh.IsValid) continue;
                    
                    double actualMinZ = double.PositiveInfinity;
                    double actualMaxZ = double.NegativeInfinity;
                    
                    for (int vIdx = 0; vIdx < mesh.Vertices.Count; vIdx++)
                    {
                        var v = mesh.Vertices[vIdx];
                        double tVal = GenerateFractalNoise(v.X, v.Y, seed, octaves, weights, sizes, tStyle);
                        double z = minH + tVal * (maxH - minH);
                        
                        actualMinZ = Math.Min(actualMinZ, z);
                        actualMaxZ = Math.Max(actualMaxZ, z);
                        globalMinH = Math.Min(globalMinH, z);
                        globalMaxH = Math.Max(globalMaxH, z);
                        
                        mesh.Vertices.SetVertex(vIdx, v.X, v.Y, z);
                    }
                    
                    mesh.RebuildNormals();
                    mesh.VertexColors.Clear();
                    
                    double actualHRange = (actualMaxZ - actualMinZ) > 0.001 ? (actualMaxZ - actualMinZ) : 0.001;
                    double slopeRad = Math.Max(0.0, Math.Min(slopeAngle, 90.0)) * Math.PI / 180.0;
                    double thresholdZ = Math.Cos(slopeRad);
                    double falloffRange = 0.20;
                    
                    mesh.FaceNormals.ComputeFaceNormals();
                    if (maskVal > 0.0 && densVal > 0.0)
                    {
                        double treeFreq = 1.0 / 150.0;
                        
                        for (int fIdx = 0; fIdx < mesh.Faces.Count; fIdx++)
                        {
                            var face = mesh.Faces[fIdx];
                            if (!face.IsTriangle) continue;
                            
                            Point3d center = mesh.Faces.GetFaceCenter(fIdx);
                            Vector3f fNorm = mesh.FaceNormals[fIdx];
                            
                            if (Math.Abs(fNorm.Z) < 0.7) continue;
                            
                            double tHeight = (center.Z - actualMinZ) / actualHRange;
                            if (tHeight < treeZMin || tHeight > treeZMax) continue;
                            
                            double patchNoise = PerlinNoise(center.X * treeFreq, center.Y * treeFreq, treeSeed);
                            double patchVal = (patchNoise * 0.5) + 0.5;
                            
                            if (patchVal < maskVal)
                            {
                                double intensity = 1.0 - (patchVal / maskVal);
                                double prob = intensity * densVal * 3.0;
                                int spawnCount = (int)prob;
                                
                                if (rand.NextDouble() < (prob - spawnCount))
                                {
                                    spawnCount++;
                                }
                                
                                Point3f pA = mesh.Vertices[face.A];
                                Point3f pB = mesh.Vertices[face.B];
                                Point3f pC = mesh.Vertices[face.C];
                                
                                for (int k = 0; k < spawnCount; k++)
                                {
                                    double r1 = rand.NextDouble();
                                    double r2 = rand.NextDouble();
                                    if (r1 + r2 > 1.0)
                                    {
                                        r1 = 1.0 - r1;
                                        r2 = 1.0 - r2;
                                    }
                                        
                                    double tX = pA.X * (1.0 - r1 - r2) + pB.X * r1 + pC.X * r2;
                                    double tY = pA.Y * (1.0 - r1 - r2) + pB.Y * r1 + pC.Y * r2;
                                    double tZ = pA.Z * (1.0 - r1 - r2) + pB.Z * r1 + pC.Z * r2;
                                    
                                    treesOut.Append(new GH_Point(new Point3d(tX, tY, tZ)), path);
                                    totalTrees++;
                                }
                            }
                        }
                    }
                    
                    for (int vIdx = 0; vIdx < mesh.Vertices.Count; vIdx++)
                    {
                        var pt = mesh.Vertices[vIdx];
                        double tHeight = (pt.Z - actualMinZ) / actualHRange;
                        Color baseColor = GetGradientColor(tHeight, colors);
                        
                        if (useSlope)
                        {
                            var normal = mesh.Normals[vIdx];
                            double nz = Math.Abs(normal.Z);
                            if (nz < thresholdZ)
                            {
                                double blendFactor = Math.Min((thresholdZ - nz) / falloffRange, 1.0);
                                Color finalColor = BlendColors(baseColor, slopeCol, blendFactor);
                                mesh.VertexColors.Add(finalColor);
                            }
                            else
                            {
                                mesh.VertexColors.Add(baseColor);
                            }
                        }
                        else
                        {
                            mesh.VertexColors.Add(baseColor);
                        }
                    }
                    
                    if (cStep > 0.0)
                    {
                        BoundingBox meshBox = mesh.GetBoundingBox(true);
                        double startZ = Math.Floor(meshBox.Min.Z / cStep) * cStep;
                        Point3d p0 = new Point3d(0, 0, startZ);
                        Point3d p1 = new Point3d(0, 0, meshBox.Max.Z + cStep);
                        Curve[] contours = Mesh.CreateContourCurves(mesh, p0, p1, cStep, 0.01);
                        
                        if (contours != null)
                        {
                            foreach (var crv in contours)
                            {
                                double ptZ = crv.PointAtStart.Z;
                                double rem = Math.Abs(ptZ % mStep);
                                if (rem < 0.001 || Math.Abs(rem - mStep) < 0.001)
                                {
                                    mainContoursOut.Append(new GH_Curve(crv), path);
                                }
                                else
                                {
                                    normContoursOut.Append(new GH_Curve(crv), path);
                                }
                            }
                        }
                    }

                    if (makeSolid)
                    {
                        double baseZ = actualMinZ - Math.Max(1.0, (actualMaxZ - actualMinZ) * 0.1);
                        
                        Mesh bottomMesh = mesh.DuplicateMesh();
                        for (int i = 0; i < bottomMesh.Vertices.Count; i++)
                        {
                            var v = bottomMesh.Vertices[i];
                            bottomMesh.Vertices.SetVertex(i, v.X, v.Y, baseZ);
                        }
                        
                        bottomMesh.Flip(true, true, true);
                        
                        bottomMesh.VertexColors.Clear();
                        for (int i = 0; i < bottomMesh.Vertices.Count; i++)
                        {
                            bottomMesh.VertexColors.Add(baseCol);
                        }
                        
                        Mesh wallMesh = new Mesh();
                        Polyline[] nakedPolys = mesh.GetNakedEdges();
                        if (nakedPolys != null)
                        {
                            foreach (var poly in nakedPolys)
                            {
                                for (int i = 0; i < poly.Count - 1; i++)
                                {
                                    var p0 = poly[i];
                                    var p1 = poly[i + 1];
                                    var p0_b = new Point3d(p0.X, p0.Y, baseZ);
                                    var p1_b = new Point3d(p1.X, p1.Y, baseZ);
                                    
                                    int v0 = wallMesh.Vertices.Add(p0);
                                    int v1 = wallMesh.Vertices.Add(p1);
                                    int v2 = wallMesh.Vertices.Add(p1_b);
                                    int v3 = wallMesh.Vertices.Add(p0_b);
                                    
                                    wallMesh.VertexColors.Add(baseCol);
                                    wallMesh.VertexColors.Add(baseCol);
                                    wallMesh.VertexColors.Add(baseCol);
                                    wallMesh.VertexColors.Add(baseCol);
                                    
                                    wallMesh.Faces.AddFace(v0, v1, v2, v3);
                                }
                            }
                        }
                        
                        mesh.Append(bottomMesh);
                        mesh.Append(wallMesh);
                        mesh.Weld(Math.PI);
                        
                        mesh.UnifyNormals();
                        
                        if (mesh.SolidOrientation() == -1)
                        {
                            mesh.Flip(true, true, true);
                        }
                            
                        mesh.Normals.ComputeNormals();
                    }
                        
                    mesh.Compact();
                    meshOut.Append(new GH_Mesh(mesh), path);
                    totalItems++;
                }
            }

            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds;

            if (double.IsInfinity(globalMinH)) { globalMinH = 0; globalMaxH = 0; }

            Message = $"{NickName}\n" +
                      $"Time: {ms:F1} ms\n" +
                      $"---\n" +
                      $"Items: {totalItems}\n" +
                      $"Trees: {totalTrees}\n" +
                      $"Height: {globalMinH:F1}m - {globalMaxH:F1}m\n" +
                      $"Area: {totalArea:N0}m2";

            string instructionsOut = 
@"=== TERRAIN GENERATOR GRIPS ===
Boundary       -> Curve (Tree)
MaxHeight      -> Number (Tree)
MinHeight      -> Number (Tree)
Seed           -> Integer (Tree)
PatternSizeXY  -> Number (Tree)
PatternHeightZ -> Number (Tree)
ContourStep    -> Number (Tree)
MainStep       -> Number (Tree)
Colors         -> Color (Tree)
Resolution     -> Integer (Tree)
UseSlopeColor  -> Boolean (Tree)
SlopeColor     -> Color (Tree)
SlopeAngle     -> Number (Tree)
TerrainStyle   -> Integer (Tree)
Solid          -> Boolean (Tree)
BaseCol        -> Color (Tree)
TreeMsk        -> Number (Tree)
TreeDns        -> Number (Tree)
TreeSeed       -> Integer (Tree)
TreeZMin       -> Number (Tree) [e.g. 0.15]
TreeZMax       -> Number (Tree) [e.g. 0.85]";

            DA.SetData(0, instructionsOut);
            DA.SetDataTree(1, meshOut);
            DA.SetDataTree(2, normContoursOut);
            DA.SetDataTree(3, mainContoursOut);
            DA.SetDataTree(4, treesOut);
        }

        private T GetTreeItem<T, G>(GH_Structure<G> tree, int branchIdx, int itemIdx, T defaultValue) where G : IGH_Goo
        {
            if (tree == null || branchIdx >= tree.PathCount) return defaultValue;
            var branch = tree.get_Branch(branchIdx);
            if (branch == null || branch.Count == 0) return defaultValue;
            var goo = (itemIdx < branch.Count) ? branch[itemIdx] : branch[branch.Count - 1];
            if (goo == null) return defaultValue;
            T val = defaultValue;
            if (goo is IGH_Goo ghGoo && ghGoo.CastTo<T>(out val)) return val;
            return defaultValue;
        }

        private T GetSafeTreeItem<T, G>(GH_Structure<G> tree, int branchIdx, int itemIdx, T defaultValue) where G : IGH_Goo
        {
            return GetTreeItem(tree, branchIdx, itemIdx, defaultValue);
        }

        private List<T> GetSafeTreeList<T, G>(GH_Structure<G> tree, int branchIdx, List<T> defaultList) where G : IGH_Goo
        {
            if (tree == null || branchIdx >= tree.PathCount) return defaultList;
            var branch = tree.get_Branch(branchIdx);
            if (branch == null || branch.Count == 0) return defaultList;
            List<T> list = new List<T>();
            foreach (var goo in branch)
            {
                if (goo == null) continue;
                T val = default;
                if (goo is IGH_Goo ghGoo && ghGoo.CastTo<T>(out val)) list.Add(val);
            }
            if (list.Count == 0) return defaultList;
            return list;
        }

        private (double, double) Hash2D(double x, double y, double seed)
        {
            double val = Math.Sin(x * 12.9898 + y * 78.233 + seed * 37.719) * 43758.5453;
            double angle = (val - Math.Floor(val)) * Math.PI * 2.0;
            return (Math.Cos(angle), Math.Sin(angle));
        }

        private double PerlinNoise(double x, double y, double seed)
        {
            double ix = Math.Floor(x);
            double iy = Math.Floor(y);
            double fx = x - ix;
            double fy = y - iy;
            
            var g00 = Hash2D(ix, iy, seed);
            var g10 = Hash2D(ix + 1, iy, seed);
            var g01 = Hash2D(ix, iy + 1, seed);
            var g11 = Hash2D(ix + 1, iy + 1, seed);
            
            double d00 = fx * g00.Item1 + fy * g00.Item2;
            double d10 = (fx - 1.0) * g10.Item1 + fy * g10.Item2;
            double d01 = fx * g01.Item1 + (fy - 1.0) * g01.Item2;
            double d11 = (fx - 1.0) * g11.Item1 + (fy - 1.0) * g11.Item2;
            
            double u = fx * fx * fx * (fx * (fx * 6.0 - 15.0) + 10.0);
            double v = fy * fy * fy * (fy * (fy * 6.0 - 15.0) + 10.0);
            
            double nx0 = d00 * (1.0 - u) + d10 * u;
            double nx1 = d01 * (1.0 - u) + d11 * u;
            
            return (nx0 * (1.0 - v) + nx1 * v) * 1.25;
        }

        private double GenerateFractalNoise(double x, double y, double seed, int octaves, List<double> weights, List<double> sizes, int style)
        {
            double z = 0.0, weightSum = 0.0;
            double cosR = 0.8, sinR = 0.6;
            double cx = x, cy = y;
            
            for (int i = 0; i < octaves; i++)
            {
                double w = (i < weights.Count) ? weights[i] : weights[weights.Count - 1] * Math.Pow(0.5, i - weights.Count + 1);
                double size = (i < sizes.Count) ? sizes[i] : sizes[sizes.Count - 1] * Math.Pow(0.5, i - sizes.Count + 1);
                double freq = 1.0 / Math.Max(0.001, size);
                
                double n = PerlinNoise(cx * freq, cy * freq, seed + i);
                
                if (style == 1)
                {
                    n = 1.0 - Math.Abs(n);
                    n *= n;
                    z += n * w;
                }
                else
                {
                    z += n * w;
                }
                    
                weightSum += w;
                double nx = cx * cosR - cy * sinR;
                double ny = cx * sinR + cy * cosR;
                cx = nx;
                cy = ny;
            }
            
            if (style == 1)
            {
                return z / Math.Max(0.001, weightSum);
            }
            else
            {
                double val = (z / Math.Max(0.001, weightSum)) * 0.5 + 0.5;
                return Math.Max(0.0, Math.Min(1.0, val));
            }
        }

        private Color GetGradientColor(double t, List<Color> colors)
        {
            if (colors == null || colors.Count == 0) return Color.White;
            if (colors.Count == 1) return colors[0];
            t = Math.Max(0.0, Math.Min(1.0, t));
            double idx = t * (colors.Count - 1);
            int i = (int)Math.Floor(idx);
            double frac = idx - i;
            if (i >= colors.Count - 1) return colors[colors.Count - 1];
            
            Color c1 = colors[i];
            Color c2 = colors[i + 1];
            int r = (int)(c1.R + (c2.R - c1.R) * frac);
            int g = (int)(c1.G + (c2.G - c1.G) * frac);
            int b = (int)(c1.B + (c2.B - c1.B) * frac);
            return Color.FromArgb(r, g, b);
        }

        private Color BlendColors(Color c1, Color c2, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            int r = (int)(c1.R + (c2.R - c1.R) * t);
            int g = (int)(c1.G + (c2.G - c1.G) * t);
            int b = (int)(c1.B + (c2.B - c1.B) * t);
            return Color.FromArgb(r, g, b);
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("B5D0F9B7-23A4-48E3-A5B2-1D4CD77A34C7");
    }
}
