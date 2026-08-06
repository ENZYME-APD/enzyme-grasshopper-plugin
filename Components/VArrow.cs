using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class VArrow : GH_Component
    {
        public VArrow()
          : base("Vector Arrow Generator", "V-Arrow",
              "Generates high-fidelity 2D arrow outlines and meshes from input lines with custom mode logic.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Lines", "L", "Input lines", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Mode", "M", "0=End, 1=Start, 2=Double", GH_ParamAccess.item, 2);
            pManager.AddNumberParameter("BodyWidth", "BW", "Body Width", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("HeadWidth", "HW", "Head Width", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("HeadLength", "HL", "Head Length", GH_ParamAccess.item, 2.0);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Arrow2D", "A2D", "Arrow 2D Outlines", GH_ParamAccess.list);
            pManager.AddMeshParameter("ArrowMesh", "AM", "Arrow Meshes", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Stopwatch sw = Stopwatch.StartNew();

            List<Line> lines = new List<Line>();
            if (!DA.GetDataList(0, lines)) 
            {
                this.Message = "Awaiting Lines...";
                return;
            }
            if (lines.Count == 0)
            {
                this.Message = "Awaiting Lines...";
                return;
            }

            int m_val = 2;
            DA.GetData(1, ref m_val);

            double bw = 0.5;
            DA.GetData(2, ref bw);

            double hw = 1.5;
            DA.GetData(3, ref hw);

            double hl = 2.0;
            DA.GetData(4, ref hl);

            string current_mode_str = "Custom";
            if (m_val == 0) current_mode_str = "End Head";
            else if (m_val == 1) current_mode_str = "Start Head";
            else if (m_val == 2) current_mode_str = "Double Head";

            List<Curve> arrow2D = new List<Curve>();
            List<Mesh> arrowMesh = new List<Mesh>();

            foreach (var ln in lines)
            {
                if (!ln.IsValid) continue;

                Point3d p_start = ln.From;
                Point3d p_end = ln.To;
                Vector3d v_dir = p_end - p_start;
                double v_length = v_dir.Length;

                if (v_length < (hl * 1.1)) continue;

                v_dir.Unitize();
                Vector3d v_perp = new Vector3d(-v_dir.Y, v_dir.X, 0);

                List<Curve> parts = new List<Curve>();
                double half_bw = bw * 0.5;
                double half_hw = hw * 0.5;

                Point3d[] GetHeadPts(Point3d anchor, Vector3d direction, bool is_end)
                {
                    int rev = is_end ? 1 : -1;
                    Point3d tip = anchor;
                    Point3d base_center = anchor - (direction * hl * rev);
                    Point3d side_a = base_center + (v_perp * half_hw);
                    Point3d side_b = base_center - (v_perp * half_hw);
                    return new Point3d[] { tip, side_a, side_b, tip };
                }

                Point3d s_start = p_start;
                Point3d s_end = p_end;
                double overlap = hl * 0.1;

                if (m_val == 0 || m_val == 2) s_end -= v_dir * (hl - overlap);
                if (m_val == 1 || m_val == 2) s_start += v_dir * (hl - overlap);

                Point3d[] shaft_pts = new Point3d[]
                {
                    s_start + v_perp * half_bw,
                    s_end + v_perp * half_bw,
                    s_end - v_perp * half_bw,
                    s_start - v_perp * half_bw,
                    s_start + v_perp * half_bw
                };
                parts.Add(new Polyline(shaft_pts).ToPolylineCurve());

                if (m_val == 0 || m_val == 2)
                    parts.Add(new Polyline(GetHeadPts(p_end, v_dir, true)).ToPolylineCurve());
                if (m_val == 1 || m_val == 2)
                    parts.Add(new Polyline(GetHeadPts(p_start, v_dir, false)).ToPolylineCurve());

                Curve[] merged_crvs = Curve.CreateBooleanUnion(parts, 0.001);
                
                if (merged_crvs != null && merged_crvs.Length > 0)
                {
                    foreach (var c in merged_crvs)
                    {
                        arrow2D.Add(c);
                        var pm = Mesh.CreateFromPlanarBoundary(c, MeshingParameters.Default, 0.001);
                        if (pm != null)
                        {
                            arrowMesh.Add(pm);
                        }
                    }
                }
            }

            DA.SetDataList(0, arrow2D);
            DA.SetDataList(1, arrowMesh);

            sw.Stop();
            double msec = sw.Elapsed.TotalMilliseconds;
            this.Message = $"{current_mode_str}\nn: {arrowMesh.Count} | {msec:F1}ms";
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // Assuming IconLoader is available in the global scope or Enzyme namespace
                return IconLoader.Load("V-Arrow.png");
            }
        }

        public override Guid ComponentGuid => new Guid("46c65664-d62f-410a-83b6-12a23af6c6ab");
    }
}
