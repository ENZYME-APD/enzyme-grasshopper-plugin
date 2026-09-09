using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Enzyme.Components
{
    public class KeylinePattern : GH_Component
    {
        public KeylinePattern()
          : base("Keyline Engine", "Keyline",
              "Generates parametric plowing lines or swale networks by offsetting guide curves along a terrain mesh.",
              "Enzyme", "LEAP")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "M", "The base topography mesh", GH_ParamAccess.item);
            pManager.AddCurveParameter("Guide Curves", "C", "The reference contours or keylines", GH_ParamAccess.list);
            pManager.AddNumberParameter("Spacing", "D", "Horizontal distance between plowing lines", GH_ParamAccess.item, 3.0);
            pManager.AddIntegerParameter("Count", "N", "Number of parallel lines to generate per side", GH_ParamAccess.item, 5);
            
            pManager[2].Optional = true;
            pManager[3].Optional = true;
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.5, 20.0, 3.0, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 0, 10, 5, 330, 60);
            }
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Keylines", "K", "Generated 3D swale/plow curves projected on terrain", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Component information and methodology", GH_ParamAccess.item);
            pManager.AddTextParameter("Dashboard Data", "Dashboard Data", "Unified JSON payload containing legend and key analysis metrics for the Analysis Dashboard", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh terrain = null;
            if (!DA.GetData(0, ref terrain) || terrain == null) return;

            List<Curve> guides = new List<Curve>();
            if (!DA.GetDataList(1, guides) || guides.Count == 0) return;

            double spacing = 3.0;
            DA.GetData(2, ref spacing);

            int count = 5;
            DA.GetData(3, ref count);

            List<Curve> keylines = new List<Curve>();
            Mesh[] meshes = new Mesh[] { terrain };

            foreach (Curve guide in guides)
            {
                if (guide == null) continue;

                // Add the original guide projected to the mesh
                var projGuide = Curve.ProjectToMesh(guide, meshes, Vector3d.ZAxis, 0.01);
                if (projGuide != null) keylines.AddRange(projGuide);

                // Generate offsets
                for (int i = 1; i <= count; i++)
                {
                    double dist = spacing * i;
                    
                    // Offset in both directions in the XY plane
                    Curve[] offsetPos = guide.Offset(Plane.WorldXY, dist, 0.01, CurveOffsetCornerStyle.Sharp);
                    Curve[] offsetNeg = guide.Offset(Plane.WorldXY, -dist, 0.01, CurveOffsetCornerStyle.Sharp);

                    if (offsetPos != null)
                    {
                        foreach (Curve c in offsetPos)
                        {
                            var projected = Curve.ProjectToMesh(c, meshes, Vector3d.ZAxis, 0.01);
                            if (projected != null) keylines.AddRange(projected);
                        }
                    }

                    if (offsetNeg != null)
                    {
                        foreach (Curve c in offsetNeg)
                        {
                            var projected = Curve.ProjectToMesh(c, meshes, Vector3d.ZAxis, 0.01);
                            if (projected != null) keylines.AddRange(projected);
                        }
                    }
                }
            }

            Message = $"Keyline Pattern\n---\nSpacing: {spacing}m\nCount: {count}\nGenerated: {keylines.Count}";
            
            double totalLen = 0;
            foreach (var crv in keylines) if (crv != null) totalLen += crv.GetLength();
            
            JObject payload = new JObject();
            payload["Title"] = "KEYLINE PATTERN";
            payload["Type"] = "Discrete";
            payload["Colors"] = new JArray(new JObject { ["R"] = 50, ["G"] = 200, ["B"] = 50 });
            payload["Labels"] = new JArray("Swales / Plow Lines");
            
            JArray metrics = new JArray();
            metrics.Add(new JObject { ["Name"] = "Keylines Generated", ["Value"] = keylines.Count.ToString() });
            metrics.Add(new JObject { ["Name"] = "Total Length", ["Value"] = $"{totalLen:F1} m" });
            payload["Metrics"] = metrics;
            
            DA.SetData(1, "KEYLINE PATTERN\n"
                + "\n"
                + "METHODOLOGY:\n"
                + "Generates agricultural earthworks (swales, plow lines) via contour-parallel offsetting originating from the Master Keyline. "
                + "Because hills naturally spread apart while valleys converge, offsetting downward from the keyline naturally creates a slightly off-contour geometry with a constant fall toward the ridges.\n\n"
                + "INTERPRETATION & IMPORTANCE:\n"
                + "Passively rehydrates landscapes without pumping by naturally intercepting surface runoff and distributing it from wet valleys out to dry ridges, maximizing soil moisture infiltration.");
            DA.SetData(2, payload.ToString(Newtonsoft.Json.Formatting.None));

            DA.SetDataList(0, keylines);
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("Keyline.png");

        public override Guid ComponentGuid
        {
            get { return new Guid("F6B3D4C1-92A5-4E38-B2C3-D4E5F6A7B8C9"); }
        }
    }
}
