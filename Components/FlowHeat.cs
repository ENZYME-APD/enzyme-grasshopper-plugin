using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Collections;

namespace Enzyme.Components
{
    public class FlowHeat : GH_Component
    {
        public FlowHeat()
          : base("Flow Accumulation Heatmap", "FlowHeat",
              "Generates a flow accumulation heatmap by evaluating water paths against mesh vertices.",
              "Enzyme", "Terrain")
        {
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 1.0, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -38);
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 2, 220, 26, 180, 22);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TerrainMesh", "TM", "The unified topological surface.", GH_ParamAccess.item);
            pManager.AddCurveParameter("FlowPaths", "FP", "The flow lines generated from the Raindrop Engine.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("VisualScale", "VS", "Multiplier to intensify the visual color mapping (Try 1.5 to 3.0).", GH_ParamAccess.item, 1.0);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("HeatmapMesh", "HM", "The colored terrain mesh displaying flow accumulation.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("VertexCounts", "VC", "Raw accumulation data mapped 1-to-1 with mesh vertices.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Node configuration guide.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            
            Mesh terrainMesh = null;
            if (!DA.GetData(0, ref terrainMesh)) return;

            GH_Structure<GH_Curve> flowPaths;
            if (!DA.GetDataTree(1, out flowPaths)) return;

            double visualScale = 1.0;
            DA.GetData(2, ref visualScale);

            Mesh coloredMesh = terrainMesh.DuplicateMesh();
            coloredMesh.VertexColors.Clear();

            Point3dList meshPts = new Point3dList(coloredMesh.Vertices.ToPoint3dArray());
            int[] vertexHits = new int[coloredMesh.Vertices.Count];

            int pathCount = 0;

            foreach (var branch in flowPaths.Branches)
            {
                foreach (var ghCurve in branch)
                {
                    if (ghCurve == null || ghCurve.Value == null) continue;
                    pathCount++;

                    Curve crv = ghCurve.Value;
                    if (crv.TryGetPolyline(out Polyline poly))
                    {
                        foreach (Point3d pt in poly)
                        {
                            int closestIdx = meshPts.ClosestIndex(pt);
                            vertexHits[closestIdx]++;
                        }
                    }
                }
            }

            int maxAccum = vertexHits.Length > 0 ? vertexHits.Max() : 1;
            if (maxAccum == 0) maxAccum = 1;

            GH_Structure<GH_Integer> vertexCounts = new GH_Structure<GH_Integer>();
            GH_Path pathIndex = new GH_Path(0);

            for (int i = 0; i < vertexHits.Length; i++)
            {
                int hits = vertexHits[i];
                double normalized = Math.Sqrt(hits / (double)maxAccum) * visualScale;
                double intensity = Math.Min(1.0, Math.Max(0.0, normalized));

                int r = (int)(220 * (1.0 - intensity) + 10 * intensity);
                int g = (int)(220 * (1.0 - intensity) + 50 * intensity);
                int b = (int)(220 * (1.0 - intensity) + 255 * intensity);

                coloredMesh.VertexColors.Add(Color.FromArgb(255, r, g, b));

                vertexCounts.Append(new GH_Integer(hits), pathIndex);
            }

            DA.SetData(0, coloredMesh);
            DA.SetDataTree(1, vertexCounts);
            
            string instructions = 
                "[INPUTS]\n" +
                "TerrainMesh  : Mesh (Item Access) - The unified topological surface.\n" +
                "FlowPaths    : Curve (Tree Access) - The flow lines generated from the Raindrop Engine.\n" +
                "VisualScale  : float (Item Access) - Multiplier to intensify the visual color mapping (Try 1.5 to 3.0).\n\n" +
                "[OUTPUTS]\n" +
                "HeatmapMesh      : Mesh (Item Access) - The colored terrain mesh displaying flow accumulation.\n" +
                "VertexCounts     : int (Tree Access) - Raw accumulation data mapped 1-to-1 with mesh vertices.\n" +
                "Instructions : string (Item Access) - Node configuration guide.";
            DA.SetData(2, instructions);

            watch.Stop();
            double durationMs = watch.Elapsed.TotalMilliseconds;

            this.Message = $"{this.NickName}\nTime: {durationMs:F1} ms\n---\nPaths Evaluated: {pathCount}\nPeak Accumulation: {maxAccum}\n● Visual Scale: {visualScale}";
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("FlowHeat.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("f6e8c75d-3d4f-4d2c-8a2e-4b6c3d5a7e9b"); }
        }
    }
}
