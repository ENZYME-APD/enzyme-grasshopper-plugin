// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Notes
    /* 
      Members:
        RhinoDoc RhinoDocument
        GH_Document GrasshopperDocument
        IGH_Component Component
        int Iteration

      Methods (Virtual & overridable):
        Print(string text)
        Print(string format, params object[] args)
        Reflect(object obj)
        Reflect(object obj, string method_name)
    */
    #endregion

    private void RunScript(
		Mesh Mesh,
		List<Curve> BoundaryCurves,
		double BlendAngle,
		Color CutColor,
		Color FillColor,
		double MeshResolution,
		bool ShowContours,
		ref object Instructions_Out,
		ref object ModMesh,
		ref object ColoredMesh,
		ref object CutVolume,
		ref object FillVolume,
		ref object Contours,
		ref object MainContours)
    {


// 1. HARDCODED COMPONENT METADATA
Component.Name = "Adaptive Terrain Grader";
Component.NickName = "TERRAIN GRADER";
Component.Description = "Generates adaptive grading meshes, volumes, and crisp cut/fill colors.";

// 2. INSTRUCTIONS OUT STRING
string instructions = 
    "--- ADAPTIVE TERRAIN GRADER ---\n" +
    "REQUIRED INPUTS:\n" +
    "  Mesh           : [Mesh] Original Topography\n" +
    "  BoundaryCurves : [List<Curve>] Closed pads (Optional)\n" +
    "  BlendAngle     : [Number] Max allowable slope (Default: 45)\n" +
    "  CutColor       : [Color] Cut zones (Default: Red)\n" +
    "  FillColor      : [Color] Fill zones (Default: Blue)\n" +
    "  MeshResolution : [Number] Base grid size (Default: 10.0)\n" +
    "  ShowContours   : [Boolean] Toggle contour generation\n\n" +
    "OUTPUTS:\n" +
    "  ModMesh        : Adaptive Tri-Mesh\n" +
    "  ColoredMesh    : Unwelded crisp cut/fill visualizer\n" +
    "  CutVolume      : Total Cut (Negative Z)\n" +
    "  FillVolume     : Total Fill (Positive Z)\n" +
    "  Contours       : 1m Interval Curves\n" +
    "  MainContours   : 5m Interval Curves\n";
Instructions_Out = instructions;

// 3. FALLBACK DEFAULTS
if (MeshResolution <= 0.01) MeshResolution = 10.0;
if (BlendAngle <= 0.01) BlendAngle = 45.0;
if (CutColor.IsEmpty || CutColor.A == 0) CutColor = System.Drawing.Color.Red;
if (FillColor.IsEmpty || FillColor.A == 0) FillColor = System.Drawing.Color.Blue;
if (BoundaryCurves == null) BoundaryCurves = new List<Curve>();

// 4. VALIDATION
if (Mesh == null || !Mesh.IsValid)
{
    Component.Message = "INVALID MESH";
    return;
}

System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

// 5. PASSTHROUGH FOR EMPTY CURVES
if (BoundaryCurves.Count == 0)
{
    ModMesh = Mesh.DuplicateMesh();
    Mesh passthroughColor = Mesh.DuplicateMesh();
    passthroughColor.VertexColors.CreateMonotoneMesh(System.Drawing.Color.White);
    
    ColoredMesh = passthroughColor;
    CutVolume = 0.0;
    FillVolume = 0.0;
    Contours = new List<Curve>();
    MainContours = new List<Curve>();
    
    timer.Stop();
    Component.Message = 
        $"{Component.NickName}\n" +
        $"Time: {timer.ElapsedMilliseconds} ms\n" +
        $"---\n" +
        $"NO PADS: PASSTHROUGH";
    return;
}

// 6. PREPARE BOUNDARIES AND PLANES
List<Curve> validCurves = new List<Curve>();
List<Plane> padPlanes = new List<Plane>();

foreach (Curve crv in BoundaryCurves)
{
    if (crv != null && crv.IsClosed)
    {
        validCurves.Add(crv);
        Plane fitPlane;
        Plane.FitPlaneToPoints(crv.TryGetPolyline(out Polyline pl) ? pl : crv.DivideByCount(20, true).Select(p => crv.PointAt(p)).ToList(), out fitPlane);
        padPlanes.Add(fitPlane);
    }
}

if (validCurves.Count == 0)
{
    Component.Message = "NO CLOSED CURVES";
    return;
}

BoundingBox bbox = Mesh.GetBoundingBox(true);
double tanAngle = Math.Tan(BlendAngle * (Math.PI / 180.0));

// 7. POINT CLOUD GENERATION (Adaptive Grid)
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

// 8. PAD PROJECTION AND BLEND LOGIC
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

// 9. DELAUNAY TESSELLATION
Mesh resultMesh = Rhino.Geometry.Mesh.CreateFromTessellation(modifiedPoints, null, Plane.WorldXY, false);
resultMesh.Normals.ComputeNormals();
resultMesh.Compact();

// 10. GRID-BASED VOLUME MATH
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

// 11. CRISP FLAT FACE COLORING (Unwelded)
Mesh colored = resultMesh.DuplicateMesh();
colored.Unweld(0.0, true); // Detaches all faces so colors cannot bleed
colored.VertexColors.CreateMonotoneMesh(System.Drawing.Color.White);

System.Threading.Tasks.Parallel.For(0, colored.Faces.Count, i =>
{
    MeshFace face = colored.Faces[i];
    
    // Evaluate the explicit vertices of the face instead of the centroid
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

        // If the vertices have shifted by more than 0.05 units, tag the face
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

// 12. CONTOUR GENERATION
List<Curve> minorContours = new List<Curve>();
List<Curve> majorContours = new List<Curve>();

if (ShowContours)
{
    int zMin = (int)Math.Floor(bbox.Min.Z) - 1;
    int zMax = (int)Math.Ceiling(bbox.Max.Z) + 1;

    System.Threading.Tasks.Parallel.For(zMin, zMax + 1, z =>
    {
        Plane slicePlane = new Plane(new Point3d(0, 0, z), Vector3d.ZAxis);
        Curve[] crvs = Rhino.Geometry.Mesh.CreateContourCurves(resultMesh, slicePlane);

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

// 13. SET OUTPUTS
ModMesh = resultMesh;
ColoredMesh = colored;
CutVolume = cutAcc;
FillVolume = fillAcc;
Contours = minorContours;
MainContours = majorContours;

timer.Stop();

// Calculate the 2D Site Area based on the mesh extents
double siteArea = (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y);

// 14. UPDATE COMPONENT HUD (Multi-line layout)
Component.Message = 
    $"{Component.NickName}\n" +
    $"Time: {timer.ElapsedMilliseconds} ms\n" +
    $"---\n" +
    $"SITE: {siteArea:N0} m²\n" +
    $"GRID: {MeshResolution:N1} m\n" +
    $"CUT: {cutAcc:N1} m³\n" +
    $"FILL: {fillAcc:N1} m³";

    }
}
