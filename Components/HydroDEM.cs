using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class HydroDEM : GH_Component
    {
        public HydroDEM()
          : base("Hydro-DEM Engine", "HydroDEM",
              "Calculates Flow Direction and Flow Accumulation on a terrain mesh to extract stream networks.",
              "Enzyme", "LEAP")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "M", "The base topography mesh", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Threshold", "T", "Minimum flow accumulation to form a stream", GH_ParamAccess.item, 50);
            
            pManager[1].Optional = true;
        }

        private bool hasSources = false;
        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 1, 1000, 50, 330, 20);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Streams", "S", "Extracted stream networks (Polylines)", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Accumulation", "A", "Flow accumulation value per topology vertex", GH_ParamAccess.list);
            pManager.AddPointParameter("Topology Points", "P", "Topology vertices matching the accumulation list", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData(0, ref mesh) || mesh == null) return;

            int threshold = 50;
            DA.GetData(1, ref threshold);
            if (threshold < 1) threshold = 1;

            // Ensure mesh topology is generated
            mesh.TopologyVertices.SortEdges();
            int vCount = mesh.TopologyVertices.Count;

            int[] flowDirection = new int[vCount];
            int[] accumulation = new int[vCount];
            
            // Step 1: Calculate Flow Direction (Steepest Descent)
            for (int i = 0; i < vCount; i++)
            {
                accumulation[i] = 1; // Every vertex receives at least its own rain
                Point3f p = mesh.TopologyVertices[i];
                int[] neighbors = mesh.TopologyVertices.ConnectedTopologyVertices(i);
                
                float maxSlope = 0;
                int targetIndex = -1;
                
                foreach (int n in neighbors)
                {
                    Point3f np = mesh.TopologyVertices[n];
                    float drop = p.Z - np.Z;
                    
                    if (drop > 0)
                    {
                        float dist = (float)p.DistanceTo(np);
                        float slope = drop / dist;
                        if (slope > maxSlope)
                        {
                            maxSlope = slope;
                            targetIndex = n;
                        }
                    }
                }
                flowDirection[i] = targetIndex;
            }
            
            // Step 2: Calculate Flow Accumulation (Top-Down approach)
            // Sort vertices by Z elevation (highest to lowest)
            var sortedIndices = Enumerable.Range(0, vCount)
                                          .OrderByDescending(i => mesh.TopologyVertices[i].Z)
                                          .ToList();
                                          
            foreach (int i in sortedIndices)
            {
                int target = flowDirection[i];
                if (target != -1)
                {
                    accumulation[target] += accumulation[i];
                }
            }
            
            // Step 3: Extract Stream Networks
            List<Curve> streams = new List<Curve>();
            bool[] visitedEdges = new bool[vCount];
            
            // Start tracing from the highest stream nodes first
            var streamNodes = sortedIndices.Where(i => accumulation[i] >= threshold).ToList();
            
            foreach (int i in streamNodes)
            {
                if (visitedEdges[i]) continue;
                
                Polyline pline = new Polyline();
                int curr = i;
                
                while (curr != -1 && accumulation[curr] >= threshold)
                {
                    pline.Add(mesh.TopologyVertices[curr]);
                    visitedEdges[curr] = true;
                    
                    int next = flowDirection[curr];
                    if (next != -1 && visitedEdges[next])
                    {
                        // Connect to existing traced stream and stop to prevent overlaps
                        pline.Add(mesh.TopologyVertices[next]);
                        break;
                    }
                    curr = next;
                }
                
                if (pline.Count > 1)
                {
                    streams.Add(pline.ToPolylineCurve());
                }
            }
            
            // Step 4: Output data
            List<Point3d> topPoints = new List<Point3d>();
            for(int i = 0; i < vCount; i++)
            {
                Point3f p = mesh.TopologyVertices[i];
                topPoints.Add(new Point3d(p.X, p.Y, p.Z));
            }

            Message = $"Hydro-DEM\n---\nThreshold: {threshold}\nStreams: {streams.Count}";
            DA.SetDataList(0, streams);
            DA.SetDataList(1, accumulation.ToList());
            DA.SetDataList(2, topPoints);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("E5A9D3B2-81C4-4B39-A1B2-C3D4E5F6A7B8"); }
        }
    }
}
