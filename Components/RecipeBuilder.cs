using System;
using System.Collections.Generic;
using System.Text;
using Grasshopper.Kernel;

namespace Enzyme.Components
{
    public class RecipeBuilder : GH_Component
    {
        public RecipeBuilder()
          : base("Recipe JSON Builder", "RecipeBuilder",
              "Parametrically compiles lists of programs, heights, and floor counts into the standardized JSON array required by the Stage 1 Adapters.",
              Enzyme.Utils.TabInfo.TabName, "Masterplan (Beta)")
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
                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, 0, 220, -11, 180, 22);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Program", "P", "Program (e.g., Retail, Office, Hotel)", GH_ParamAccess.list);
            pManager.AddNumberParameter("FloorHeight", "H", "Floor height (e.g., 4.5, 4.0, 3.3)", GH_ParamAccess.list);
            pManager.AddIntegerParameter("NumFloors", "F", "Number of floors (e.g., 3, 10, 5)", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("RecipeJSON", "J", "The formatted JSON payload.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<string> programs = new List<string>();
            List<double> heights = new List<double>();
            List<int> floors = new List<int>();

            if (!DA.GetDataList(0, programs)) return;
            if (!DA.GetDataList(1, heights)) return;
            if (!DA.GetDataList(2, floors)) return;

            if (programs.Count == 0 || heights.Count == 0 || floors.Count == 0)
            {
                Message = "Awaiting Data";
                return;
            }

            int limit = Math.Min(programs.Count, Math.Min(heights.Count, floors.Count));

            int totalFloors = 0;
            double totalHeight = 0;

            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < limit; i++)
            {
                string prog = programs[i]?.Trim();
                double height = heights[i];
                int floor = floors[i];

                sb.AppendLine("  {");
                sb.AppendLine($"    \"program\": \"{prog}\",");
                // Use InvariantCulture for numbers to ensure dot as decimal separator
                sb.AppendLine($"    \"height\": {height.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
                sb.AppendLine($"    \"floors\": {floor}");
                sb.Append("  }");
                if (i < limit - 1) sb.AppendLine(",");
                else sb.AppendLine();

                totalFloors += floor;
                totalHeight += (floor * height);
            }
            sb.AppendLine("]");

            string json = sb.ToString();

            DA.SetData(0, json);

            // Use invariant culture for formatting the message to ensure consistent double to string conversion
            Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, "RECIPE\n---\nBlocks: {0}\nFloors: {1}\nHeight: {2:F1}m", limit, totalFloors, totalHeight);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("RecipeBuilder.png");
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("b48b9f1d-2f08-412e-a50e-cd5e73059eb2"); }
        }
    }
}
