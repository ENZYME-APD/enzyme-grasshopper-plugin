using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class MeshColorReset : GH_Component
    {
        public MeshColorReset()
          : base("Reset Mesh Colors", "Mesh-C-Reset",
              "Strips all vertex colors from a mesh, allowing Custom Preview to override its color natively.",
              "Enzyme", "Terrain")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Meshes to clean", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Cleaned meshes without vertex colors", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Mesh> inputMeshes = new List<Mesh>();
            if (!DA.GetDataList(0, inputMeshes)) return;

            List<Mesh> outputMeshes = new List<Mesh>();

            foreach (var m in inputMeshes)
            {
                if (m == null) continue;
                Mesh cleaned = m.DuplicateMesh();
                cleaned.VertexColors.Clear();
                outputMeshes.Add(cleaned);
            }

            DA.SetDataList(0, outputMeshes);
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
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", 100, 0);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(200, 200, 200), 200, -30);
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("ResetMeshColors.png");
            }
        }

        public override Guid ComponentGuid => new Guid("7d3f5b2c-6a4a-4e2b-a1b9-3f8c5b9f7d2a");
        public override GH_Exposure Exposure => GH_Exposure.quarternary;
    }
}
