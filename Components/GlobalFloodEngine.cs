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
    public class GlobalFloodEngine : GH_Component
    {
        public GlobalFloodEngine()
            : base("Global Volumetric Flood Engine", "GlobalFlood",
                "Simulates global terrain flooding based on a target water volume and generates a depth heatmap.",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("GlobalFlood.png");
            }
        }

        public override Guid ComponentGuid => new Guid("A1B2C3D4-E5F6-4789-9A0B-1C2D3E4F5A6B");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ix = 200, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 0.1, ix, -120);
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TerrainMesh", "Input Terrain Mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("TargetVolume", "TargetVolume", "Target volume of water", GH_ParamAccess.item);
            pManager.AddNumberParameter("ZStep", "ZStep", "Iteration step for Z level", GH_ParamAccess.item, 0.1);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("FloodMesh", "FloodMesh", "Flooded terrain heatmap mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("WaterLevel", "WaterLevel", "Final water elevation", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh TerrainMesh = null;
            if (!DA.GetData(0, ref TerrainMesh)) return;

            double TargetVolume = 0.0;
            if (!DA.GetData(1, ref TargetVolume)) return;

            double ZStep = 0.1;
            DA.GetData(2, ref ZStep);

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            Mesh outMesh = null;
            double finalWaterLevel = 0.0;
            int floodedVertexCount = 0;
            int totalVertices = 0;

            if (TerrainMesh != null && TargetVolume > 0 && ZStep > 0)
            {
                outMesh = TerrainMesh.DuplicateMesh();
                outMesh.VertexColors.Clear();

                totalVertices = outMesh.Vertices.Count;
                double[] vertexAreas = new double[totalVertices];
                double minZ = double.MaxValue;
                double maxZ = double.MinValue;

                for (int i = 0; i < totalVertices; i++)
                {
                    double z = outMesh.Vertices[i].Z;
                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;
                }

                foreach (MeshFace face in outMesh.Faces)
                {
                    Point3f A = outMesh.Vertices[face.A];
                    Point3f B = outMesh.Vertices[face.B];
                    Point3f C = outMesh.Vertices[face.C];
                    
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

                double maxDepth = finalWaterLevel - minZ;
                if (maxDepth <= 0) maxDepth = 0.001; 

                for (int i = 0; i < totalVertices; i++)
                {
                    double z = outMesh.Vertices[i].Z;
                    if (z >= finalWaterLevel)
                    {
                        outMesh.VertexColors.Add(System.Drawing.Color.FromArgb(255, 230, 230, 230));
                    }
                    else
                    {
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

            DA.SetData(0, outMesh);
            DA.SetData(1, finalWaterLevel);

            sw.Stop();
            Message = $"{this.NickName}\nTime: {sw.ElapsedMilliseconds} ms\n---\n● Flooded: {floodedVertexCount} | ○ Dry: {totalVertices - floodedVertexCount}";
        }
    }
}
