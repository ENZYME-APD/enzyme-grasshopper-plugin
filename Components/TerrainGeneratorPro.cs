using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Enzyme;

namespace Enzyme.Components
{
    /// <summary>
    /// Generates topography with noise-masked procedural forest scattering and strict elevation limits.
    /// This component uses multiple layers of Perlin/Fractal noise and Delaunay tessellation to build 
    /// a colored mesh terrain, solid bases, and forest points.
    /// </summary>
    public class TerrainGeneratorPro : GH_Component
    {
        public TerrainGeneratorPro()
            : base("Terrain Generator Pro", "TRN-P",
                "Generates topography with noise-masked procedural forest scattering and strict elevation limits.",
                Enzyme.Utils.TabInfo.TabName, "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("TRN-P.png");
            }
        }

        public override Guid ComponentGuid => new Guid("E3F2D4A1-B9C8-4D7E-A5F1-92A3B4C5D6E7");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 0, "curve", 180, -440);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 200, 100.0, 330, -400);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 0.0, 330, -360);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 84, 42, 330, -320);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 4, "150\n50\n20", 250, -280, 100, 60);
                Enzyme.Utils.AutoWireHelper.WireMultilinePanel(this, document, 5, "1.0\n0.3\n0.1", 250, -210, 100, 60);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 2.0, 1.0, 330, -140);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 10.0, 5.0, 330, -100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 200, 100, 330, -60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10, false, 210, -20);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 11, System.Drawing.Color.DarkGray, 210, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 12, 0.0, 60, 30.0, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, 13, new string[]{"Realistic Soft Hills", "Ridged/Cellular Pattern"}, new string[]{"0", "1"}, 300, 100);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 14, false, 210, 140);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 15, System.Drawing.Color.DimGray, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 16, 0.0, 1.0, 0.0, 330, 220);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 17, 0.0, 1.0, 0.0, 330, 260);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 18, 0.0, 24690, 12345, 330, 300);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 19, 0.0, 1.0, 0.15, 330, 340);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 20, 0.0, 1.0, 0.85, 330, 380);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 300, -135);
                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 1, System.Drawing.Color.Gray, 0.05, 300, -45);
                Enzyme.Utils.AutoWireHelper.WireCurvePreview(this, document, 2, System.Drawing.Color.Black, 0.15, 300, 45);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 300, 135);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "Boundary", "Closed boundary limits", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxHeight", "MaxHeight", "Maximum elevation in meters", GH_ParamAccess.item, 100.0);
            pManager.AddNumberParameter("MinHeight", "MinHeight", "Minimum elevation in meters", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("Seed", "Seed", "Randomization seed", GH_ParamAccess.item, 42);
            pManager.AddNumberParameter("PatternSizeXY", "PatternSizeXY", "List of feature sizes in meters", GH_ParamAccess.list);
            pManager.AddNumberParameter("PatternHeightZ", "PatternHeightZ", "List of relative feature strengths", GH_ParamAccess.list);
            pManager.AddNumberParameter("ContourStep", "ContourStep", "Interval for normal contour lines", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("MainStep", "MainStep", "Interval for main contour lines", GH_ParamAccess.item, 5.0);
            pManager.AddColourParameter("Colors", "Colors", "List of gradient colors based on height", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Resolution", "Resolution", "Grid density", GH_ParamAccess.item, 100);
            pManager.AddBooleanParameter("UseSlopeColor", "UseSlopeColor", "Toggle steep slope coloring", GH_ParamAccess.item, false);
            pManager.AddColourParameter("SlopeColor", "SlopeColor", "Color applied to sheer cliffs/slopes", GH_ParamAccess.item, Color.DarkGray);
            pManager.AddNumberParameter("SlopeAngle", "SlopeAngle", "Angle where slope color starts", GH_ParamAccess.item, 30.0);
            pManager.AddIntegerParameter("TerrainStyle", "TerrainStyle", "0 = Realistic Soft Hills, 1 = Ridged/Cellular Pattern", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("Solid", "Solid", "Toggle closed mesh extrusion", GH_ParamAccess.item, false);
            pManager.AddColourParameter("BaseCol", "BaseCol", "Color for the extruded solid base section", GH_ParamAccess.item, Color.DimGray);
            pManager.AddNumberParameter("TreeMsk", "TreeMsk", "Coverage mask threshold 0.0 to 1.0", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("TreeDns", "TreeDns", "Density multiplier inside mask areas 0.0 to 1.0", GH_ParamAccess.item, 0.0);
            pManager.AddIntegerParameter("TreeSeed", "TreeSeed", "Dedicated seed for the forest noise map", GH_ParamAccess.item, 12345);
            pManager.AddNumberParameter("TreeZMin", "TreeZMin", "Minimum relative elevation for trees 0.0 to 1.0", GH_ParamAccess.item, 0.15);
            pManager.AddNumberParameter("TreeZMax", "TreeZMax", "Maximum relative elevation for trees 0.0 to 1.0", GH_ParamAccess.item, 0.85);

            pManager[4].Optional = true; // PatternSizeXY
            pManager[5].Optional = true; // PatternHeightZ
            pManager[8].Optional = true; // Colors
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "Gradient colored terrain geometry", GH_ParamAccess.item);
            pManager.AddCurveParameter("NormContours", "NormContours", "Standard contour lines", GH_ParamAccess.list);
            pManager.AddCurveParameter("MainContours", "MainContours", "Major interval contour lines", GH_ParamAccess.list);
            pManager.AddPointParameter("Trees", "Trees", "Scattered point coordinates for trees", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Curve boundary = null;
            if (!DA.GetData(0, ref boundary)) return;
            if (boundary == null || !boundary.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Boundary must be a closed curve.");
                return;
            }

            double max_h = 100.0, min_h = 0.0, c_step = 1.0, m_step = 5.0, slope_angle = 30.0, tree_msk = 0.0, tree_dns = 0.0, tree_zmin = 0.15, tree_zmax = 0.85;
            int seed = 42, res = 100, t_style = 0, tree_seed = 12345;
            bool use_slope = false, solid = false;
            Color slope_col = Color.DarkGray, base_col = Color.DimGray;
            List<double> sizes = new List<double>();
            List<double> weights = new List<double>();
            List<Color> colors = new List<Color>();

            DA.GetData(1, ref max_h);
            DA.GetData(2, ref min_h);
            DA.GetData(3, ref seed);
            DA.GetDataList(4, sizes);
            DA.GetDataList(5, weights);
            DA.GetData(6, ref c_step);
            DA.GetData(7, ref m_step);
            DA.GetDataList(8, colors);
            DA.GetData(9, ref res);
            DA.GetData(10, ref use_slope);
            DA.GetData(11, ref slope_col);
            DA.GetData(12, ref slope_angle);
            DA.GetData(13, ref t_style);
            DA.GetData(14, ref solid);
            DA.GetData(15, ref base_col);
            DA.GetData(16, ref tree_msk);
            DA.GetData(17, ref tree_dns);
            DA.GetData(18, ref tree_seed);
            DA.GetData(19, ref tree_zmin);
            DA.GetData(20, ref tree_zmax);

            if (sizes.Count == 0) sizes.AddRange(new double[] { 500.0, 150.0, 30.0 });
            if (weights.Count == 0) weights.AddRange(new double[] { 1.0, 0.3, 0.05 });
            if (colors.Count == 0) colors.AddRange(new Color[] { Color.LightGreen, Color.SaddleBrown, Color.White });

            int octaves = Math.Max(sizes.Count, weights.Count);

            BoundingBox bbox = boundary.GetBoundingBox(true);
            double w = bbox.Max.X - bbox.Min.X;
            double h = bbox.Max.Y - bbox.Min.Y;
            if (w <= 0 || h <= 0) return;

            int nx = Math.Max(2, res);
            int ny = Math.Max(2, (int)(nx * (h / w)));
            double grid_step = w / nx;

            Curve flat_crv = boundary.DuplicateCurve();
            flat_crv.Translate(new Vector3d(0, 0, -bbox.Min.Z));
            double crv_length = flat_crv.GetLength();
            int div_count = Math.Max(4, (int)(crv_length / grid_step));

            List<Point3d> pts_bnd = new List<Point3d>();
            double[] t_vals = flat_crv.DivideByCount(div_count, true);
            if (t_vals != null)
            {
                foreach (double t in t_vals) pts_bnd.Add(flat_crv.PointAt(t));
            }
            else
            {
                PolylineCurve nc = flat_crv.ToPolyline(0.01, 0.1, 0.0, 0.0);
                if (nc != null)
                {
                    for (int i = 0; i < nc.PointCount; i++) pts_bnd.Add(nc.Point(i));
                }
            }

            if (pts_bnd.Count > 0 && pts_bnd[0].DistanceTo(pts_bnd[pts_bnd.Count - 1]) > 0.001)
            {
                pts_bnd.Add(pts_bnd[0]);
            }
            if (pts_bnd.Count == 0) return;

            var net_boundaries = new List<IEnumerable<Point3d>> { pts_bnd };
            var net_all_pts = new List<Point3d>(pts_bnd);

            double min_dist = grid_step * 0.35;
            for (int j = 0; j <= ny; j++)
            {
                double y = bbox.Min.Y + ((double)j / ny) * h;
                for (int i = 0; i <= nx; i++)
                {
                    double x = bbox.Min.X + ((double)i / nx) * w;
                    Point3d pt = new Point3d(x, y, 0);
                    if (flat_crv.Contains(pt, Plane.WorldXY, 0.01) == PointContainment.Inside)
                    {
                        flat_crv.ClosestPoint(pt, out double t);
                        if (pt.DistanceTo(flat_crv.PointAt(t)) > min_dist)
                        {
                            net_all_pts.Add(pt);
                        }
                    }
                }
            }

            Mesh mesh = Mesh.CreateFromTessellation(net_all_pts, net_boundaries, Plane.WorldXY, false);
            if (mesh == null || !mesh.IsValid) return;

            double actual_min_z = double.PositiveInfinity;
            double actual_max_z = double.NegativeInfinity;

            for (int v_idx = 0; v_idx < mesh.Vertices.Count; v_idx++)
            {
                var v = mesh.Vertices[v_idx];
                double t_val = GenerateFractalNoise(v.X, v.Y, seed, octaves, weights, sizes, t_style);
                double z = min_h + t_val * (max_h - min_h);
                
                actual_min_z = Math.Min(actual_min_z, z);
                actual_max_z = Math.Max(actual_max_z, z);
                
                mesh.Vertices.SetVertex(v_idx, v.X, v.Y, z);
            }

            mesh.RebuildNormals();
            mesh.VertexColors.Clear();

            double actual_h_range = Math.Max(0.001, actual_max_z - actual_min_z);
            double slope_rad = Math.Max(0.0, Math.Min(slope_angle, 90.0)) * (Math.PI / 180.0);
            double threshold_z = Math.Cos(slope_rad);
            double falloff_range = 0.20;

            List<Point3d> treesOut = new List<Point3d>();
            Random rand = new Random(tree_seed);

            mesh.FaceNormals.ComputeFaceNormals();
            if (tree_msk > 0.0 && tree_dns > 0.0)
            {
                double tree_freq = 1.0 / 150.0;
                
                for (int f_idx = 0; f_idx < mesh.Faces.Count; f_idx++)
                {
                    var face = mesh.Faces[f_idx];
                    if (!face.IsTriangle) continue;

                    Point3d center = mesh.Faces.GetFaceCenter(f_idx);
                    Vector3f f_norm = mesh.FaceNormals[f_idx];

                    if (Math.Abs(f_norm.Z) < 0.7) continue;

                    double t_height = (center.Z - actual_min_z) / actual_h_range;
                    if (t_height < tree_zmin || t_height > tree_zmax) continue;

                    double patch_noise = PerlinNoise(center.X * tree_freq, center.Y * tree_freq, tree_seed);
                    double patch_val = (patch_noise * 0.5) + 0.5;

                    if (patch_val < tree_msk)
                    {
                        double intensity = 1.0 - (patch_val / tree_msk);
                        double prob = intensity * tree_dns * 3.0;
                        int spawn_count = (int)prob;

                        if (rand.NextDouble() < (prob - spawn_count))
                            spawn_count++;

                        Point3f pA = mesh.Vertices[face.A];
                        Point3f pB = mesh.Vertices[face.B];
                        Point3f pC = mesh.Vertices[face.C];

                        for (int k = 0; k < spawn_count; k++)
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
                            
                            treesOut.Add(new Point3d(tX, tY, tZ));
                        }
                    }
                }
            }

            for (int v_idx = 0; v_idx < mesh.Vertices.Count; v_idx++)
            {
                var pt = mesh.Vertices[v_idx];
                double t_height = (pt.Z - actual_min_z) / actual_h_range;
                Color base_color = GetGradientColor(t_height, colors);

                if (use_slope)
                {
                    Vector3f normal = mesh.Normals[v_idx];
                    float nz = Math.Abs(normal.Z);
                    if (nz < threshold_z)
                    {
                        double blend_factor = Math.Min((threshold_z - nz) / falloff_range, 1.0);
                        Color final_color = BlendColors(base_color, slope_col, blend_factor);
                        mesh.VertexColors.Add(final_color);
                    }
                    else
                    {
                        mesh.VertexColors.Add(base_color);
                    }
                }
                else
                {
                    mesh.VertexColors.Add(base_color);
                }
            }

            List<Curve> mainContoursOut = new List<Curve>();
            List<Curve> normContoursOut = new List<Curve>();

            if (c_step > 0.0)
            {
                BoundingBox mesh_box = mesh.GetBoundingBox(true);
                double start_z = Math.Floor(mesh_box.Min.Z / c_step) * c_step;
                Point3d p0 = new Point3d(0, 0, start_z);
                Point3d p1 = new Point3d(0, 0, mesh_box.Max.Z + c_step);
                Curve[] contours = Mesh.CreateContourCurves(mesh, p0, p1, c_step, 0.01);

                if (contours != null)
                {
                    foreach (var crv in contours)
                    {
                        double pt_z = crv.PointAtStart.Z;
                        double rem = Math.Abs(pt_z % m_step);
                        if (rem < 0.001 || Math.Abs(rem - m_step) < 0.001)
                            mainContoursOut.Add(crv);
                        else
                            normContoursOut.Add(crv);
                    }
                }
            }

            if (solid)
            {
                double base_z = actual_min_z - Math.Max(1.0, (actual_max_z - actual_min_z) * 0.1);
                
                Mesh bottom_mesh = mesh.DuplicateMesh();
                for (int i = 0; i < bottom_mesh.Vertices.Count; i++)
                {
                    var v = bottom_mesh.Vertices[i];
                    bottom_mesh.Vertices.SetVertex(i, v.X, v.Y, base_z);
                }
                
                bottom_mesh.Flip(true, true, true);
                bottom_mesh.VertexColors.Clear();
                for (int i = 0; i < bottom_mesh.Vertices.Count; i++)
                {
                    bottom_mesh.VertexColors.Add(base_col);
                }

                Mesh wall_mesh = new Mesh();
                Polyline[] naked_polys = mesh.GetNakedEdges();
                if (naked_polys != null)
                {
                    foreach (var poly in naked_polys)
                    {
                        for (int i = 0; i < poly.Count - 1; i++)
                        {
                            Point3d p0 = poly[i];
                            Point3d p1 = poly[i + 1];
                            Point3d p0_b = new Point3d(p0.X, p0.Y, base_z);
                            Point3d p1_b = new Point3d(p1.X, p1.Y, base_z);

                            int v0 = wall_mesh.Vertices.Add(p0);
                            int v1 = wall_mesh.Vertices.Add(p1);
                            int v2 = wall_mesh.Vertices.Add(p1_b);
                            int v3 = wall_mesh.Vertices.Add(p0_b);

                            wall_mesh.VertexColors.Add(base_col);
                            wall_mesh.VertexColors.Add(base_col);
                            wall_mesh.VertexColors.Add(base_col);
                            wall_mesh.VertexColors.Add(base_col);

                            wall_mesh.Faces.AddFace(v0, v1, v2, v3);
                        }
                    }
                }

                mesh.Append(bottom_mesh);
                mesh.Append(wall_mesh);
                mesh.Weld(Math.PI);
                mesh.UnifyNormals();

                if (mesh.SolidOrientation() == -1)
                {
                    mesh.Flip(true, true, true);
                }
                mesh.Normals.ComputeNormals();
            }

            mesh.Compact();
            
            stopwatch.Stop();

            DA.SetData(0, mesh);
            DA.SetDataList(1, normContoursOut);
            DA.SetDataList(2, mainContoursOut);
            DA.SetDataList(3, treesOut);

            double area = Rhino.Geometry.AreaMassProperties.Compute(boundary).Area;
            Message = $"{this.NickName}\nTime: {stopwatch.ElapsedMilliseconds} ms\nArea: {area:N0} m2\nTrees: {treesOut.Count}";
        }

        private double[] Hash2D(double x, double y, int seed)
        {
            double val = Math.Sin(x * 12.9898 + y * 78.233 + seed * 37.719) * 43758.5453;
            double angle = (val - Math.Floor(val)) * Math.PI * 2.0;
            return new double[] { Math.Cos(angle), Math.Sin(angle) };
        }

        private double PerlinNoise(double x, double y, int seed)
        {
            double ix = Math.Floor(x);
            double iy = Math.Floor(y);
            double fx = x - ix;
            double fy = y - iy;

            var g00 = Hash2D(ix, iy, seed);
            var g10 = Hash2D(ix + 1.0, iy, seed);
            var g01 = Hash2D(ix, iy + 1.0, seed);
            var g11 = Hash2D(ix + 1.0, iy + 1.0, seed);

            double d00 = fx * g00[0] + fy * g00[1];
            double d10 = (fx - 1.0) * g10[0] + fy * g10[1];
            double d01 = fx * g01[0] + (fy - 1.0) * g01[1];
            double d11 = (fx - 1.0) * g11[0] + (fy - 1.0) * g11[1];

            double u = fx * fx * fx * (fx * (fx * 6.0 - 15.0) + 10.0);
            double v = fy * fy * fy * (fy * (fy * 6.0 - 15.0) + 10.0);

            double nx0 = d00 * (1.0 - u) + d10 * u;
            double nx1 = d01 * (1.0 - u) + d11 * u;

            return (nx0 * (1.0 - v) + nx1 * v) * 1.25;
        }

        private double GenerateFractalNoise(double x, double y, int seed, int octaves, List<double> weights, List<double> sizes, int style)
        {
            double z = 0.0;
            double weight_sum = 0.0;
            double cos_r = 0.8, sin_r = 0.6;
            double cx = x, cy = y;

            for (int i = 0; i < octaves; i++)
            {
                double w = i < weights.Count ? weights[i] : weights.Last() * Math.Pow(0.5, i - weights.Count + 1);
                double size = i < sizes.Count ? sizes[i] : sizes.Last() * Math.Pow(0.5, i - sizes.Count + 1);
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

                weight_sum += w;
                double nx = cx * cos_r - cy * sin_r;
                double ny = cx * sin_r + cy * cos_r;
                cx = nx;
                cy = ny;
            }

            if (style == 1)
            {
                return z / Math.Max(0.001, weight_sum);
            }
            else
            {
                double val = (z / Math.Max(0.001, weight_sum)) * 0.5 + 0.5;
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
            
            if (i >= colors.Count - 1) return colors.Last();

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
    }
}
