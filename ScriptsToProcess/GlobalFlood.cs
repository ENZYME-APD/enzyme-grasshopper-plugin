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
		Mesh TerrainMesh,
		double TargetVolume,
		double ZStep,
		ref object Instructions_Out,
		ref object FloodMesh,
		ref object WaterLevel)
    {
        // Write your logic here
       // --- [ASSISTANT-GENERATED COMPONENT METADATA] ---
Component.Name = "Global Volumetric Flood Engine";
Component.NickName = "GlobalFlood";
Component.Description = "Simulates global terrain flooding based on a target water volume and generates a depth heatmap.";

Instructions_Out = @"[INPUTS]
TerrainMesh  : Mesh (Item Access)
TargetVolume : double (Item Access)
ZStep        : double (Item Access)
[OUTPUTS]
FloodMesh        : Mesh (Item Access)
WaterLevel       : double (Item Access)
Instructions_Out : string (Item Access)";

// --- [EXECUTION] ---
System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

Mesh outMesh = null;
double finalWaterLevel = 0.0;
int floodedVertexCount = 0;
int totalVertices = 0;

if (TerrainMesh != null && TargetVolume > 0 && ZStep > 0)
{
    outMesh = TerrainMesh.DuplicateMesh();
    outMesh.VertexColors.Clear(); // Mandatory purge of inherited color arrays

    totalVertices = outMesh.Vertices.Count;
    double[] vertexAreas = new double[totalVertices];
    double minZ = double.MaxValue;
    double maxZ = double.MinValue;

    // 1. Pre-compute Z-bounds and extract vertex heights
    for (int i = 0; i < totalVertices; i++)
    {
        double z = outMesh.Vertices[i].Z;
        if (z < minZ) minZ = z;
        if (z > maxZ) maxZ = z;
    }

    // 2. Calculate the 2D projected tributary area for every vertex
    foreach (MeshFace face in outMesh.Faces)
    {
        Point3f A = outMesh.Vertices[face.A];
        Point3f B = outMesh.Vertices[face.B];
        Point3f C = outMesh.Vertices[face.C];
        
        // Fast 2D cross-product area for triangle
        double areaABC = 0.5 * Math.Abs((B.X - A.X) * (C.Y - A.Y) - (C.X - A.X) * (B.Y - A.Y));
        double thirdAreaABC = areaABC / 3.0;
        
        vertexAreas[face.A] += thirdAreaABC;
        vertexAreas[face.B] += thirdAreaABC;
        vertexAreas[face.C] += thirdAreaABC;

        if (face.IsQuad)
        {
            Point3f D = outMesh.Vertices[face.D];
            double areaACD = 0.5 * Math.Abs((C.X - A.X) * (D.Y - A.Y) - (D.X - A.X) * (C.Y - A.Y));
            double thirdAreaACD = areaACD / 3.0;
            
            vertexAreas[face.A] += thirdAreaACD;
            vertexAreas[face.C] += thirdAreaACD;
            vertexAreas[face.D] += thirdAreaACD;
        }
    }

    // 3. Brute-Force Volumetric Iteration (The Bathtub Solver)
    double currentZ = minZ;
    double calcVolume = 0.0;
    
    while (currentZ <= maxZ)
    {
        calcVolume = 0.0;
        for (int i = 0; i < totalVertices; i++)
        {
            double dz = currentZ - outMesh.Vertices[i].Z;
            if (dz > 0) 
            {
                calcVolume += dz * vertexAreas[i];
            }
        }

        if (calcVolume >= TargetVolume)
            break;

        currentZ += ZStep;
    }

    finalWaterLevel = currentZ;

    // 4. Paint the Heatmap
    double maxDepth = finalWaterLevel - minZ;
    if (maxDepth <= 0) maxDepth = 0.001; // Prevent division by zero

    for (int i = 0; i < totalVertices; i++)
    {
        double z = outMesh.Vertices[i].Z;
        if (z >= finalWaterLevel)
        {
            // Dry Land: Light Gray
            outMesh.VertexColors.Add(System.Drawing.Color.FromArgb(255, 230, 230, 230));
        }
        else
        {
            // Flooded Area: Map depth to a Cyan -> Deep Blue gradient
            floodedVertexCount++;
            double depth = finalWaterLevel - z;
            double intensity = Math.Min(1.0, depth / maxDepth);
            
            int r = 0;
            int g = (int)(200 * (1.0 - intensity));
            int b = (int)(255 - (105 * intensity)); 
            
            outMesh.VertexColors.Add(System.Drawing.Color.FromArgb(255, r, g, b));
        }
    }
}

// Assign explicitly to the generated output references
FloodMesh = outMesh;
WaterLevel = finalWaterLevel;

// --- [TELEMETRY & HUD] ---
sw.Stop();
Component.Message = $@"{Component.NickName}
Time: {sw.ElapsedMilliseconds} ms
---
Branches: 1
Total Items: 1
● Flooded: {floodedVertexCount} | ○ Dry: {totalVertices - floodedVertexCount}";
    }
}
