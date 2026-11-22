using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Enzyme.Utils;

namespace Enzyme.Components
{
    public class HydroDEM : EnzymeComponent
    {
        protected override bool UsesRunGate => true;

        public HydroDEM()
          : base("Hydro-DEM Engine", "HydroDEM",
              "Calculates Flow Direction and Flow Accumulation on a terrain mesh to extract stream networks. Depressions are filled first (Priority-Flood) so streams run through hollows to the terrain edge; the hollows are reported as ponds.",
              "Enzyme", "Site Analysis")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "M", "The base topography mesh", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Threshold", "T", "Minimum flow accumulation to form a stream", GH_ParamAccess.item, 50);
            
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Run", "Run", "Optional. False = skip the analysis and keep showing the last results (cached). Share one toggle with the Analysis Dashboard to switch everything on/off together.", GH_ParamAccess.item, true);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddBooleanParameter("Fill Pits", "Fill", "Fill depressions before routing (Priority-Flood, Barnes et al. 2014). False = old behaviour: flow stops in every hollow.", GH_ParamAccess.item, true);
            pManager.AddNumberParameter("Min Pond Depth", "MinD", "Hollows shallower than this (m) are not reported as ponds (still routed through).", GH_ParamAccess.item, 0.05);
            pManager[pManager.ParamCount - 2].Optional = true;
            pManager[pManager.ParamCount - 1].Optional = true;
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
                AutoWireDefaultInputs(document);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Streams", "S", "Extracted stream networks (Polylines)", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Accumulation", "A", "Flow accumulation value per topology vertex", GH_ParamAccess.list);
            pManager.AddPointParameter("Topology Points", "P", "Topology vertices matching the accumulation list", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Method, assumptions, simplifications and references.", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "Unified JSON payload containing legend and key analysis metrics for the Analysis Dashboard", GH_ParamAccess.item);
            pManager.AddNumberParameter("Pond Depth", "Depth", "Water depth per topology vertex when hollows fill up (0 = dry).", GH_ParamAccess.list);
            pManager.AddMeshParameter("Ponds", "Ponds", "Water surface of every pond at its spill level.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Catchment", "Catch", "Catchment id per topology vertex: the outlet vertex its water leaves the terrain at.", GH_ParamAccess.list);
        }

        protected override void Solve(IGH_DataAccess DA, System.Diagnostics.Stopwatch hudSw)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Mesh mesh = null;
            if (!DA.GetData(0, ref mesh) || mesh == null) return;

            int threshold = 50;
            DA.GetData(1, ref threshold);
            if (threshold < 1) threshold = 1;

            bool fillPits = true; double minDepth = 0.05;
            DA.GetData(3, ref fillPits);
            DA.GetData(4, ref minDepth);

            // Topology graph: one node per topology vertex
            mesh.TopologyVertices.SortEdges();
            int vCount = mesh.TopologyVertices.Count;
            var z = new double[vCount];
            var nbrs = new int[vCount][];
            var lens = new double[vCount][];
            for (int i = 0; i < vCount; i++)
            {
                Point3f p = mesh.TopologyVertices[i];
                z[i] = p.Z;
                nbrs[i] = mesh.TopologyVertices.ConnectedTopologyVertices(i);
                lens[i] = new double[nbrs[i].Length];
                for (int k = 0; k < nbrs[i].Length; k++)
                {
                    Point3f q = mesh.TopologyVertices[nbrs[i][k]];
                    lens[i][k] = Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
                }
            }

            // Outlets: the open edge of the terrain, where water leaves the model
            var outlet = new bool[vCount];
            bool[] naked = mesh.GetNakedEdgePointStatus();
            if (naked != null)
                for (int i = 0; i < vCount; i++)
                    foreach (int mv in mesh.TopologyVertices.MeshVertexIndices(i))
                        if (mv < naked.Length && naked[mv]) { outlet[i] = true; break; }

            // Steps 1-2: Priority-Flood fill, flow direction on the filled surface, accumulation
            var route = Hydrology.Route(z, nbrs, lens, outlet, fillPits);
            int[] flowDirection = route.Down;
            double[] accD = Hydrology.Accumulate(route, Enumerable.Repeat(1.0, vCount).ToArray());
            int[] accumulation = accD.Select(v => (int)Math.Round(v)).ToArray();
            var sortedIndices = route.UpToDown.ToList();

            // Ponds: depth, water surface, volume (vertex share of each face's plan area)
            var depth = new double[vCount];
            for (int i = 0; i < vCount; i++) depth[i] = Math.Max(0.0, route.Filled[i] - z[i]);
            int[] pondId = Hydrology.Ponds(z, route.Filled, nbrs, minDepth, out int pondCount);
            var vArea = new double[vCount];
            var water = new Mesh();
            var wIndex = new Dictionary<int, int>();
            for (int f = 0; f < mesh.Faces.Count; f++)
            {
                var face = mesh.Faces[f];
                int nv = face.IsQuad ? 4 : 3;
                var tv = new int[nv];
                for (int k = 0; k < nv; k++) tv[k] = mesh.TopologyVertices.TopologyVertexIndex(face[k]);
                double area = 0;
                for (int k = 0; k < nv; k++)
                {
                    Point3f a = mesh.TopologyVertices[tv[k]], b = mesh.TopologyVertices[tv[(k + 1) % nv]];
                    area += a.X * b.Y - b.X * a.Y;
                }
                area = Math.Abs(area) / 2.0;
                for (int k = 0; k < nv; k++) vArea[tv[k]] += area / nv;

                bool wet = true;
                for (int k = 0; k < nv && wet; k++) wet = pondId[tv[k]] >= 0 && pondId[tv[k]] == pondId[tv[0]];
                if (!wet) continue;
                var w = new int[nv];
                for (int k = 0; k < nv; k++)
                {
                    if (!wIndex.TryGetValue(tv[k], out int wi))
                    {
                        Point3f p = mesh.TopologyVertices[tv[k]];
                        wi = water.Vertices.Add(p.X, p.Y, route.Filled[tv[k]]);
                        wIndex[tv[k]] = wi;
                    }
                    w[k] = wi;
                }
                if (nv == 4) water.Faces.AddFace(w[0], w[1], w[2], w[3]); else water.Faces.AddFace(w[0], w[1], w[2]);
            }
            double pondVolume = 0, maxDepth = 0;
            for (int i = 0; i < vCount; i++)
                if (pondId[i] >= 0) { pondVolume += depth[i] * vArea[i]; maxDepth = Math.Max(maxDepth, depth[i]); }
            if (water.Faces.Count > 0) water.Normals.ComputeNormals();
            int catchments = route.Outlet.Distinct().Count(o => accumulation[o] >= threshold);

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

            stopwatch.Stop();
            Hud.Done(this, stopwatch.Elapsed.TotalMilliseconds, Hud.Metric("Streams", streams.Count),
                Hud.Metric("Ponds", pondCount), Hud.Metric("Pond vol.", pondVolume, "m³", 0));
            
            JObject payload = new JObject();
            payload["Title"] = "HYDRO DEM";
            payload["Type"] = "Discrete";
            payload["Colors"] = new JArray(new JObject { ["R"] = 0, ["G"] = 100, ["B"] = 255 });
            payload["Labels"] = new JArray("Streams");
            
            JArray metrics = new JArray();
            metrics.Add(new JObject { ["Name"] = "Stream Networks", ["Value"] = streams.Count.ToString() });
            metrics.Add(new JObject { ["Name"] = "Catchments", ["Value"] = catchments.ToString() });
            metrics.Add(new JObject { ["Name"] = "Ponds", ["Value"] = pondCount.ToString() });
            metrics.Add(new JObject { ["Name"] = "Pond volume", ["Value"] = pondVolume.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + " m³" });
            metrics.Add(new JObject { ["Name"] = "Max pond depth", ["Value"] = maxDepth.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " m" });
            payload["Metrics"] = metrics;
            
            DA.SetData(3, MethodNotes.Info("HydroDEM"));
            DA.SetData(4, payload.ToString(Newtonsoft.Json.Formatting.None));

            DA.SetDataList(0, streams);
            DA.SetDataList(1, accumulation.ToList());
            DA.SetDataList(2, topPoints);
            DA.SetDataList(5, depth);
            if (water.Faces.Count > 0) DA.SetData(6, water);
            DA.SetDataList(7, route.Outlet);
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("HydroDEM.png");

        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        private void AutoWireDefaultInputs(GH_Document document)
        {
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 1, 1000, 50, 330, 20);
            }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) =>
            {
                var doc = OnPingDocument();
                if (doc != null) AutoWireDefaultInputs(doc);
            });
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("E5A9D3B2-81C4-4B39-A1B2-C3D4E5F6A7B8"); }
        }
    }
}
