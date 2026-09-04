using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Enzyme;

namespace Enzyme.Components
{
    public class AdaptiveTerrainGrader : GH_Component
    {
        public AdaptiveTerrainGrader()
            : base("Adaptive Terrain Grader", "TERRAIN GRADER",
                "Generates adaptive grading meshes, volumes, and crisp cut/fill colors.",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("AdaptiveTerrainGrader.png");
            }
        }

        public override Guid ComponentGuid => new Guid("B5D1A2C3-E8F7-4A6B-9C0D-1E2F3A4B5C6D");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 90, 45.0, 330, -80);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 3, System.Drawing.Color.Red, 210, -40);
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 4, System.Drawing.Color.Blue, 210, 0);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 20, 10.0, 330, 40);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, false, 210, 80);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -98);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(230, 230, 230), 220, -23);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", 220, 52);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "curve", 220, 97);
            }
        }

        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "Mesh", "Original Topography", GH_ParamAccess.item);
            pManager.AddCurveParameter("BoundaryCurves", "BoundaryCurves", "Closed pads (Optional)", GH_ParamAccess.list);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("BlendAngle", "BlendAngle", "Max allowable slope in degrees", GH_ParamAccess.item, 45.0);
            pManager.AddColourParameter("CutColor", "CutColor", "Cut zones color", GH_ParamAccess.item, Color.Red);
            pManager.AddColourParameter("FillColor", "FillColor", "Fill zones color", GH_ParamAccess.item, Color.Blue);
            pManager.AddNumberParameter("MeshResolution", "MeshResolution", "Base grid size", GH_ParamAccess.item, 10.0);
            pManager.AddBooleanParameter("ShowContours", "ShowContours", "Toggle contour generation", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("ModMesh", "ModMesh", "Adaptive Tri-Mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("ColoredMesh", "ColoredMesh", "Unwelded crisp cut/fill visualizer", GH_ParamAccess.item);
            pManager.AddNumberParameter("CutVolume", "CutVolume", "Total Cut (Negative Z)", GH_ParamAccess.item);
            pManager.AddNumberParameter("FillVolume", "FillVolume", "Total Fill (Positive Z)", GH_ParamAccess.item);
            pManager.AddCurveParameter("Contours", "Contours", "1m Interval Curves", GH_ParamAccess.list);
            pManager.AddCurveParameter("MainContours", "MainContours", "5m Interval Curves", GH_ParamAccess.list);
                    pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh Mesh = null;
            if (!DA.GetData(0, ref Mesh)) return;

            List<Curve> BoundaryCurves = new List<Curve>();
            DA.GetDataList(1, BoundaryCurves);

            double BlendAngle = 45.0;
            DA.GetData(2, ref BlendAngle);

            Color CutColor = Color.Red;
            DA.GetData(3, ref CutColor);

            Color FillColor = Color.Blue;
            DA.GetData(4, ref FillColor);

            double MeshResolution = 10.0;
            DA.GetData(5, ref MeshResolution);

            bool ShowContours = false;
            DA.GetData(6, ref ShowContours);

            if (MeshResolution <= 0.01) MeshResolution = 10.0;
            if (BlendAngle <= 0.01) BlendAngle = 45.0;
            if (CutColor.IsEmpty || CutColor.A == 0) CutColor = Color.Red;
            if (FillColor.IsEmpty || FillColor.A == 0) FillColor = Color.Blue;

            if (Mesh == null || !Mesh.IsValid)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "INVALID MESH");
                return;
                            
            }

            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

            if (BoundaryCurves == null || BoundaryCurves.Count == 0)
            {
                Mesh passthroughColor = Mesh.DuplicateMesh();
                passthroughColor.VertexColors.CreateMonotoneMesh(Color.White);
                
                DA.SetData(0, Mesh.DuplicateMesh());
                DA.SetData(1, passthroughColor);
                DA.SetData(2, 0.0);
                DA.SetData(3, 0.0);
                
                timer.Stop();
                Message = $"{this.NickName}\nTime: {timer.ElapsedMilliseconds} ms\n---\nNO PADS: PASSTHROUGH";
                return;
            }

            List<Curve> validCurves = new List<Curve>();
            List<Plane> padPlanes = new List<Plane>();

            foreach (Curve crv in BoundaryCurves)
            {
                if (crv != null && crv.IsClosed)
                {
                    validCurves.Add(crv);
                    Plane fitPlane;
                    Plane.FitPlaneToPoints(crv.TryGetPolyline(out Polyline pl) ? pl.ToArray() : crv.DivideByCount(20, true).Select(p => crv.PointAt(p)).ToArray(), out fitPlane);
                    padPlanes.Add(fitPlane);
                }
            }

            if (validCurves.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "NO CLOSED CURVES");
                return;
            }

            BoundingBox bbox = Mesh.GetBoundingBox(true);
            double tanAngle = Math.Tan(BlendAngle * (Math.PI / 180.0));

            System.Collections.Concurrent.ConcurrentBag<Point3d> ptsBag = new System.Collections.Concurrent.ConcurrentBag<Point3d>();

            int xCount = (int)Math.Ceiling((bbox.Max.X - bbox.Min.X) / MeshResolution);
            int yCount = (int)Math.Ceiling((bbox.Max.Y - bbox.Min.Y) / MeshResolution);

            System.Threading.Tasks.Parallel.For(0, xCount + 1, i =>
            {
                for (int j = 0; j <= yCount; j++)
                {
                    double x = bbox.Min.X + i * MeshResolution;
                    double y = bbox.Min.Y + j * MeshResolution;
                    
                    Ray3d ray = new Ray3d(new Point3d(x, y, bbox.Max.Z + 100), Vector3d.ZAxis * -1);
                    double rayParam = Rhino.Geometry.Intersect.Intersection.MeshRay(Mesh, ray);
                    
                    if (rayParam >= 0.0)
                    {
                        Point3d pt = ray.PointAt(rayParam);
                        ptsBag.Add(pt);
                        
                        Mesh.ClosestPoint(pt, out Point3d closest, out Vector3d normal, 0.0);
                        if (Vector3d.VectorAngle(normal, Vector3d.ZAxis) > (30.0 * Math.PI / 180.0))
                        {
                            double halfRes = MeshResolution * 0.5;
                            Point3d subPt1 = new Point3d(x + halfRes, y, bbox.Max.Z + 100);
                            Point3d subPt2 = new Point3d(x, y + halfRes, bbox.Max.Z + 100);
                            
                            double raySub1 = Rhino.Geometry.Intersect.Intersection.MeshRay(Mesh, new Ray3d(subPt1, Vector3d.ZAxis * -1));
                            double raySub2 = Rhino.Geometry.Intersect.Intersection.MeshRay(Mesh, new Ray3d(subPt2, Vector3d.ZAxis * -1));
                            
                            if (raySub1 >= 0) ptsBag.Add(new Point3d(subPt1.X, subPt1.Y, bbox.Max.Z + 100 - raySub1));
                            if (raySub2 >= 0) ptsBag.Add(new Point3d(subPt2.X, subPt2.Y, bbox.Max.Z + 100 - raySub2));
                        }
                    }
                }
            });

            foreach(Curve crv in validCurves)
            {
                Point3d[] divPts;
                crv.DivideByLength(MeshResolution * 0.25, true, out divPts);
                if (divPts != null)
                {
                    foreach(Point3d p in divPts) ptsBag.Add(p);
                }
            }

            List<Point3d> basePoints = ptsBag.ToList();
            Point3d[] modifiedPoints = new Point3d[basePoints.Count];

            System.Threading.Tasks.Parallel.For(0, basePoints.Count, i =>
            {
                Point3d pt = basePoints[i];
                bool insideAnyPad = false;
                double zOffset = pt.Z;

                for (int c = 0; c < validCurves.Count; c++)
                {
                    Curve crv = validCurves[c];
                    var containTest = crv.Contains(pt, Plane.WorldXY, 0.01);
                    
                    if (containTest == PointContainment.Inside || containTest == PointContainment.Coincident)
                    {
                        padPlanes[c].ClosestParameter(pt, out double u, out double v);
                        zOffset = padPlanes[c].PointAt(u, v).Z;
                        insideAnyPad = true;
                        break;
                    }
                }

                if (!insideAnyPad)
                {
                    double maxZAllowed = double.MaxValue;
                    double minZAllowed = double.MinValue;

                    for (int c = 0; c < validCurves.Count; c++)
                    {
                        Curve crv = validCurves[c];
                        crv.ClosestPoint(pt, out double t);
                        Point3d closestCrvPt = crv.PointAt(t);
                        
                        padPlanes[c].ClosestParameter(closestCrvPt, out double u, out double v);
                        double padZ = padPlanes[c].PointAt(u, v).Z;
                        
                        double dist2D = new Point3d(pt.X, pt.Y, 0).DistanceTo(new Point3d(closestCrvPt.X, closestCrvPt.Y, 0));
                        double maxElevationChange = dist2D * tanAngle;

                        maxZAllowed = Math.Min(maxZAllowed, padZ + maxElevationChange);
                        minZAllowed = Math.Max(minZAllowed, padZ - maxElevationChange);
                    }

                    if (pt.Z > maxZAllowed) zOffset = maxZAllowed;
                    else if (pt.Z < minZAllowed) zOffset = minZAllowed;
                }

                modifiedPoints[i] = new Point3d(pt.X, pt.Y, zOffset);
            });

            Mesh resultMesh = Rhino.Geometry.Mesh.CreateFromTessellation(modifiedPoints, null, Plane.WorldXY, false);
            resultMesh.Normals.ComputeNormals();
            resultMesh.Compact();

            double cutAcc = 0.0;
            double fillAcc = 0.0;
            double cellArea = MeshResolution * MeshResolution;
            object lockObj = new object();

            System.Threading.Tasks.Parallel.For(0, xCount, i =>
            {
                double localCut = 0;
                double localFill = 0;

                for (int j = 0; j < yCount; j++)
                {
                    double cx = bbox.Min.X + (i + 0.5) * MeshResolution;
                    double cy = bbox.Min.Y + (j + 0.5) * MeshResolution;
                    
                    Ray3d ray = new Ray3d(new Point3d(cx, cy, bbox.Max.Z + 100), Vector3d.ZAxis * -1);
                    
                    double tBase = Rhino.Geometry.Intersect.Intersection.MeshRay(Mesh, ray);
                    double tMod = Rhino.Geometry.Intersect.Intersection.MeshRay(resultMesh, ray);

                    if (tBase >= 0 && tMod >= 0)
                    {
                        double zBase = bbox.Max.Z + 100 - tBase;
                        double zMod = bbox.Max.Z + 100 - tMod;
                        double diff = zMod - zBase;

                        if (diff > 0.01) localFill += (diff * cellArea);
                        else if (diff < -0.01) localCut += (Math.Abs(diff) * cellArea);
                    }
                }

                lock (lockObj)
                {
                    cutAcc += localCut;
                    fillAcc += localFill;
                }
            });

            Mesh colored = resultMesh.DuplicateMesh();
            colored.Unweld(0.0, true); 
            colored.VertexColors.CreateMonotoneMesh(Color.White);

            System.Threading.Tasks.Parallel.For(0, colored.Faces.Count, i =>
            {
                MeshFace face = colored.Faces[i];
                
                int[] vIndices = face.IsQuad ? new int[] { face.A, face.B, face.C, face.D } : new int[] { face.A, face.B, face.C };
                double totalDiff = 0.0;
                int validHits = 0;
                
                foreach(int vIdx in vIndices)
                {
                    Point3d vPt = colored.Vertices[vIdx];
                    Ray3d ray = new Ray3d(new Point3d(vPt.X, vPt.Y, bbox.Max.Z + 100), Vector3d.ZAxis * -1);
                    double tBase = Rhino.Geometry.Intersect.Intersection.MeshRay(Mesh, ray);
                    
                    if (tBase >= 0)
                    {
                        double zBase = bbox.Max.Z + 100 - tBase;
                        totalDiff += (vPt.Z - zBase);
                        validHits++;
                    }
                }
                
                if (validHits > 0)
                {
                    double avgDiff = totalDiff / validHits;

                    if (avgDiff > 0.05)
                    {
                        colored.VertexColors[face.A] = FillColor;
                        colored.VertexColors[face.B] = FillColor;
                        colored.VertexColors[face.C] = FillColor;
                        if (face.IsQuad) colored.VertexColors[face.D] = FillColor;
                    }
                    else if (avgDiff < -0.05)
                    {
                        colored.VertexColors[face.A] = CutColor;
                        colored.VertexColors[face.B] = CutColor;
                        colored.VertexColors[face.C] = CutColor;
                        if (face.IsQuad) colored.VertexColors[face.D] = CutColor;
                    }
                }
            });

            List<Curve> minorContours = new List<Curve>();
            List<Curve> majorContours = new List<Curve>();

            if (ShowContours)
            {
                int zMin = (int)Math.Floor(bbox.Min.Z) - 1;
                int zMax = (int)Math.Ceiling(bbox.Max.Z) + 1;

                System.Threading.Tasks.Parallel.For(zMin, zMax + 1, z =>
                {
                    Plane slicePlane = new Plane(new Point3d(0, 0, z), Vector3d.ZAxis);
                    Curve[] crvs = Rhino.Geometry.Mesh.CreateContourCurves(resultMesh, slicePlane, 0.01);

                    if (crvs != null && crvs.Length > 0)
                    {
                        bool isMajor = (z % 5 == 0);
                        lock (lockObj)
                        {
                            if (isMajor) majorContours.AddRange(crvs);
                            else minorContours.AddRange(crvs);
                        }
                    }
                });
            }

            DA.SetData(0, resultMesh);
            DA.SetData(1, colored);
            DA.SetData(2, cutAcc);
            DA.SetData(3, fillAcc);
            DA.SetDataList(4, minorContours);
            DA.SetDataList(5, majorContours);

            timer.Stop();

            double siteArea = (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y);

            Message = $"{this.NickName}\nTime: {timer.ElapsedMilliseconds} ms\n---\nSITE: {siteArea:N0} m²\nGRID: {MeshResolution:N1} m\nCUT: {cutAcc:N1} m³\nFILL: {fillAcc:N1} m³";
                    DA.SetData(6, "ADAPTIVE TERRAIN GRADER\n" + "\n" + "HOW IT WORKS:\n" + "Calculates localized cut-and-fill operations by projecting building pads or roads onto the terrain mesh. It adapts the mesh topology to create flat plateaus and sloped retaining embankments.\n\n" + "INTERPRETATION & IMPORTANCE:\n" + "Essential for calculating earthworks (cut/fill volumes) early in the design phase. It shows how much soil must be moved to accommodate the masterplan, directly impacting project cost and environmental disruption.");
        }
    }
}
