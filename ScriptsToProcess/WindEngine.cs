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
		bool Run,
		Mesh TerrainMesh,
		List<Mesh> ContextBuildings,
		Vector3d WindDirection,
		object WindSpeed,
		double AnalysisHeight,
		double GridSpacing,
		List<Color> CustomColors,
		double HeatmapHeight,
		ref object Instructions_Out,
		ref object VelocityHeatmap,
		ref object VelocityData,
		ref object TagPoints,
		ref object WindVectors,
		ref object VectorColors,
		ref object Streamlines,
		ref object PlainMesh)
    {
        // Write your logic here

     // --- [ASSISTANT-GENERATED COMPONENT METADATA] ---
Component.Name = "Urban Wind Vector Engine (Adjustable Heatmap)";
Component.NickName = "WindEngine";
Component.Description = "Simulates urban wind fields using terrain-parallel raycasting. Outputs a perfectly flat, crisp XY pixel-screen heatmap at a custom elevation.";

Instructions_Out = @"[INTERFACE CONTRACT]
Inputs:
  TerrainMesh      : Mesh (Item) - The underlying site topography.
  ContextBuildings : Mesh (List) - Lightweight mesh context structures.
  WindDirection    : Vector3d (Item) - Travel vector of incoming air.
  WindSpeed        : double (Item) - Baseline velocity metric.
  AnalysisHeight   : double (Item) - Human pedestrian offset.
  GridSpacing      : double (Item) - Resolution size of pixel elements.
  HeatmapHeight    : double (Item) - Z-axis elevation to project the flat heatmap.
  Run              : bool (Item) - Global execution toggle switch.
  CustomColors     : Color (List) [OPTIONAL] - Custom color spectrum override.
Outputs:
  VelocityHeatmap  : Mesh (Item) - Flat, unwelded crisp horizontal pixel-tile matrix.
  WindVectors      : Line (List) - Spatial direction markers.
  VectorColors     : Color (List) - Velocity color map matching lines 1-to-1.
  Streamlines      : PolylineCurve (List) - Continuous particle flow paths.
  VelocityData     : string (List) - Raw velocity values formatted to 1 decimal place.
  TagPoints        : Point3d (List) - Anchor coordinates for Text Tag 3D integration.
  PlainMesh        : Mesh (Item) - Original topography mesh without vertex colors.";

// --- [UPSTREAM TYPE-SAFETY CASTING] ---
Mesh terrain = TerrainMesh as Mesh;
Vector3d baseWindDir = WindDirection;
double speed = Convert.ToDouble(WindSpeed);
double height = Convert.ToDouble(AnalysisHeight);
double spacing = Convert.ToDouble(GridSpacing);
bool execute = Convert.ToBoolean(Run);

List<Mesh> buildings = new List<Mesh>();
if (ContextBuildings is System.Collections.IEnumerable enumerableContext)
{
    foreach (var item in enumerableContext)
    {
        if (item is Mesh m) buildings.Add(m);
    }
}

List<System.Drawing.Color> userColors = new List<System.Drawing.Color>();
if (CustomColors is System.Collections.IEnumerable enumerableColors)
{
    foreach (var item in enumerableColors)
    {
        if (item is System.Drawing.Color c) userColors.Add(c);
    }
}

// --- [EXECUTION BOUNDARY] ---
System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

Mesh heatmapMesh = new Mesh();
List<Line> vectorLines = new List<Line>();
List<System.Drawing.Color> vectorColorList = new List<System.Drawing.Color>();
List<System.Drawing.Color> meshColorList = new List<System.Drawing.Color>(); 
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

    // 1. Generate the Rigid Terrain-Hugging Grid
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

    // 2. Evaluate Exact Geometric Intersections
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

    // 3. Synchronized Color Output
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
        System.Drawing.Color mappedColor;

        if (isInsideSolid)
        {
            mappedColor = System.Drawing.Color.FromArgb(255, 12, 22, 52); 
        }
        else if (userColors.Count >= 2)
        {
            double position = intensity * (userColors.Count - 1);
            int lowIdx = (int)Math.Floor(position);
            int highIdx = (int)Math.Ceiling(position);
            double blend = position - lowIdx;

            System.Drawing.Color c1 = userColors[lowIdx];
            System.Drawing.Color c2 = userColors[highIdx];

            int r = (int)(c1.R * (1.0 - blend) + c2.R * blend);
            int g = (int)(c1.G * (1.0 - blend) + c2.G * blend);
            int b = (int)(c1.B * (1.0 - blend) + c2.B * blend);
            mappedColor = System.Drawing.Color.FromArgb(255, r, g, b);
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
            mappedColor = System.Drawing.Color.FromArgb(255, r, g, b);
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

    // 4. Construct Unwelded Crisp Heatmap Mesh at Custom Z-Height
    double halfGrid = spacing * 0.5;
    
    // Safely assign the user's custom HeatmapHeight, defaulting to bbox.Min.Z if empty
    double flatZ = bbox.Min.Z;
    if (HeatmapHeight != null)
    {
        try { flatZ = Convert.ToDouble(HeatmapHeight); }
        catch { /* fail silently, use default */ }
    }

    for (int i = 0; i < gridPoints.Count; i++)
    {
        Point3d centerPt = gridPoints[i];
        System.Drawing.Color tileColor = meshColorList[i];

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
    
    // 5. Generate Kinetic Streamlines
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

VelocityHeatmap = heatmapMesh;
WindVectors = vectorLines;
VectorColors = vectorColorList;
Streamlines = computedStreamlines;
VelocityData = velocityTextData;
TagPoints = tagAnchorPoints;

if (terrain != null)
{
    Mesh cleanMesh = terrain.DuplicateMesh();
    cleanMesh.VertexColors.Clear();
    PlainMesh = cleanMesh;
}

sw.Stop();
if (execute)
{
    double finalComfortPercent = activeSensorCount > 0 ? ((double)comfortablePointCount / activeSensorCount) * 100.0 : 0.0;
    Component.Message = $@"{Component.NickName}
Time: {sw.ElapsedMilliseconds} ms
---
● Min Speed  : {(minObservedSpeed == double.MaxValue ? 0.0 : minObservedSpeed):F1} m/s
○ Max Speed  : {(maxObservedSpeed == double.MinValue ? 0.0 : maxObservedSpeed):F1} m/s
● Comfort Rating : {finalComfortPercent:F1}% (≤ 5.0 m/s)";
}
else
{
    Component.Message = $@"{Component.NickName}
STATUS: SLEEPING";
}

      }
}