import re

with open('Components/LegendGeometry.cs', 'r') as f:
    content = f.read()

# 1. Insert cache variables and BeforeSolveInstance
class_start = content.find('public override Guid ComponentGuid')
if class_start == -1:
    print("Could not find class start")
    exit(1)

cache_vars = """
        private System.Collections.Generic.List<Rhino.Geometry.Mesh> m_displayMeshes = new System.Collections.Generic.List<Rhino.Geometry.Mesh>();
        private System.Collections.Generic.List<System.Drawing.Color> m_displayColors = new System.Collections.Generic.List<System.Drawing.Color>();
        private System.Collections.Generic.List<string> m_displayTexts = new System.Collections.Generic.List<string>();
        private System.Collections.Generic.List<Rhino.Geometry.Point3d> m_displayPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
        private Rhino.Geometry.BoundingBox m_displayBox = Rhino.Geometry.BoundingBox.Empty;
        private double m_lastScale = 1.0;

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            m_displayMeshes.Clear();
            m_displayColors.Clear();
            m_displayTexts.Clear();
            m_displayPoints.Clear();
            m_displayBox = Rhino.Geometry.BoundingBox.Empty;
        }

        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            base.DrawViewportMeshes(args);
            if (this.Hidden || this.Locked) return;

            for (int i = 0; i < m_displayMeshes.Count; i++)
            {
                var mat = new Rhino.Display.DisplayMaterial(m_displayColors[i]);
                args.Display.DrawMeshShaded(m_displayMeshes[i], mat);
            }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            base.DrawViewportWires(args);
            if (this.Hidden || this.Locked) return;
            
            foreach (var mesh in m_displayMeshes)
            {
                args.Display.DrawMeshWires(mesh, System.Drawing.Color.Black, 1);
            }

            double textHeight = 0.2 * m_lastScale;
            for (int i = 0; i < m_displayTexts.Count; i++)
            {
                Rhino.Geometry.Plane pln = new Rhino.Geometry.Plane(m_displayPoints[i], Rhino.Geometry.Vector3d.ZAxis);
                args.Display.Draw3dText(m_displayTexts[i], System.Drawing.Color.Black, pln, textHeight, "Arial");
            }
        }

        public override Rhino.Geometry.BoundingBox ClippingBox
        {
            get
            {
                var box = base.ClippingBox;
                box.Union(m_displayBox);
                return box;
            }
        }
"""

content = content[:class_start] + cache_vars + "\n" + content[class_start:]

# 2. Modify SolveInstance to populate cache
solve_end_str = "DA.SetDataList(0, result.Rectangles);"
solve_end_idx = content.find(solve_end_str)

if solve_end_idx == -1:
    print("Could not find SetDataList in SolveInstance")
    exit(1)

cache_population = """
            m_lastScale = scale;
            for (int i = 0; i < result.Rectangles.Count; i++)
            {
                if (result.Rectangles[i].TryGetPolyline(out Rhino.Geometry.Polyline pl) && pl.Count >= 4)
                {
                    var mesh = new Rhino.Geometry.Mesh();
                    mesh.Vertices.Add(pl[0]);
                    mesh.Vertices.Add(pl[1]);
                    mesh.Vertices.Add(pl[2]);
                    mesh.Vertices.Add(pl[3]);
                    mesh.Faces.AddFace(0, 1, 2, 3);
                    m_displayMeshes.Add(mesh);
                    m_displayBox.Union(mesh.GetBoundingBox(false));
                }
            }
            m_displayColors.AddRange(result.Colors);

            for (int i = 0; i < result.Labels.Count; i++)
            {
                m_displayTexts.Add(result.Labels[i]);
                m_displayPoints.Add(result.LabelPositions[i]);
                m_displayBox.Union(result.LabelPositions[i]);
            }

            """

content = content[:solve_end_idx] + cache_population + content[solve_end_idx:]

with open('Components/LegendGeometry.cs', 'w') as f:
    f.write(content)

