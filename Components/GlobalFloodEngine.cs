using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class GlobalFloodEngine : GH_Component
    {
        public GlobalFloodEngine()
            : base("Global Volumetric Flood Engine", "GlobalFlood",
                "Simulates rainfall accumulation pooling into local valleys and depressions.",
                Enzyme.Utils.TabInfo.TabName, "Terrain")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TM", "Input Terrain Mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Rainfall", "RF", "Rainfall intensity in Liters/m2/hour (mm/h)", GH_ParamAccess.item, 50.0);
            pManager.AddNumberParameter("Duration", "T", "Duration of the rain event in hours", GH_ParamAccess.item, 2.0);
            pManager.AddIntegerParameter("Iterations", "I", "Simulation steps for water flow", GH_ParamAccess.item, 200);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("FloodMesh", "FM", "Flooded terrain heatmap mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("WaterDepths", "WD", "Water depths at each vertex in meters", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh TerrainMesh = null;
            if (!DA.GetData(0, ref TerrainMesh)) return;

            double Rainfall = 50.0;
            if (!DA.GetData(1, ref Rainfall)) return;

            double Duration = 2.0;
            if (!DA.GetData(2, ref Duration)) return;

            int Iterations = 200;
            if (!DA.GetData(3, ref Iterations)) return;

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            if (TerrainMesh == null) return;
            Mesh outMesh = TerrainMesh.DuplicateMesh();
            outMesh.VertexColors.Clear();
            
            int numVertices = outMesh.Vertices.Count;
            int numTopVertices = outMesh.TopologyVertices.Count;

            // Initialize water depth (Rainfall is mm/h. 1 mm = 0.001 m)
            // Depth in meters = (Rainfall * Duration) / 1000.0
            double rainDepthMeters = (Rainfall * Duration) / 1000.0;

            double[] topWaterDepth = new double[numTopVertices];
            double[] topZ = new double[numTopVertices];

            for (int i = 0; i < numTopVertices; i++)
            {
                topWaterDepth[i] = rainDepthMeters;
                // Get Z of the topology vertex
                int vIdx = outMesh.TopologyVertices.MeshVertexIndices(i)[0];
                topZ[i] = outMesh.Vertices[vIdx].Z;
            }

            // Flow simulation
            double[] nextWater = new double[numTopVertices];
            for (int iter = 0; iter < Iterations; iter++)
            {
                Array.Copy(topWaterDepth, nextWater, numTopVertices);
                bool moved = false;

                for (int i = 0; i < numTopVertices; i++)
                {
                    if (topWaterDepth[i] <= 0.0001) continue;

                    double myLevel = topZ[i] + topWaterDepth[i];
                    int[] connected = outMesh.TopologyVertices.ConnectedTopologyVertices(i);
                    
                    int lowestNeighbor = -1;
                    double lowestLevel = myLevel;

                    for (int n = 0; n < connected.Length; n++)
                    {
                        int j = connected[n];
                        double neighborLevel = topZ[j] + topWaterDepth[j];
                        if (neighborLevel < lowestLevel)
                        {
                            lowestLevel = neighborLevel;
                            lowestNeighbor = j;
                        }
                    }

                    if (lowestNeighbor != -1)
                    {
                        // Flow half the difference to the lowest neighbor
                        double delta = (myLevel - lowestLevel) / 2.0;
                        if (delta > topWaterDepth[i]) delta = topWaterDepth[i]; // Can't flow more than we have
                        
                        nextWater[i] -= delta;
                        nextWater[lowestNeighbor] += delta;
                        moved = true;
                    }
                }
                
                Array.Copy(nextWater, topWaterDepth, numTopVertices);
                if (!moved) break;
            }

            // Map back to mesh vertices and colorize
            List<double> finalDepths = new List<double>(numVertices);
            int floodedVertexCount = 0;
            double maxDepth = 0.0;

            for (int i = 0; i < numVertices; i++)
            {
                int topIdx = outMesh.TopologyVertices.TopologyVertexIndex(i);
                double depth = topWaterDepth[topIdx];
                finalDepths.Add(depth);
                if (depth > maxDepth) maxDepth = depth;
            }

            if (maxDepth < 0.001) maxDepth = 0.001;

            for (int i = 0; i < numVertices; i++)
            {
                double depth = finalDepths[i];
                if (depth < 0.01) // less than 1cm water -> dry
                {
                    outMesh.VertexColors.Add(System.Drawing.Color.FromArgb(255, 230, 230, 230));
                }
                else
                {
                    floodedVertexCount++;
                    double intensity = Math.Min(1.0, depth / maxDepth);
                    
                    int r = 0;
                    int g = (int)(200 * (1.0 - intensity));
                    int b = (int)(255 - (105 * intensity)); 
                    
                    outMesh.VertexColors.Add(System.Drawing.Color.FromArgb(255, r, g, b));
                }
            }

            DA.SetData(0, outMesh);
            DA.SetDataList(1, finalDepths);

            sw.Stop();
            Message = $"{this.NickName}\nTime: {sw.ElapsedMilliseconds} ms\n---\n● Flooded: {floodedVertexCount} | ○ Dry: {numVertices - floodedVertexCount}";
        }
        
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 500.0, 50.0, 330, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 48.0, 2.0, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 10, 1000, 200, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 220, -10);
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return IconLoader.Load("GlobalFlood.png"); }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("A1B2C3D4-E5F6-4789-9A0B-1C2D3E4F5A6B"); }
        }
    }
}
