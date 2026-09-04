import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

# 1. Update RegisterInputParams
old_reg = """            pManager.AddNumberParameter("Grout Width", "Grout", "Width of the grout joint between tiles. Default 0.", GH_ParamAccess.item, 0.0);
        }"""
new_reg = """            pManager.AddNumberParameter("Grout Width", "Grout", "Width of the grout joint between tiles. Default 0.", GH_ParamAccess.item, 0.0);
            pManager.AddVectorParameter("Direction", "Dir", "Optional vector to align the grid's X-axis. Projects to the base plane.", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("Rotation", "Rot", "Optional rotation angle (in radians) applied after alignment.", GH_ParamAccess.item, 0.0);
        }"""
content = content.replace(old_reg, new_reg)

# 2. Update SolveInstance for new inputs and plane alignment
old_solve1 = """            double x_dim = 1.0, y_dim = 1.0, grout = 0.0;
            string gridType = "rectangular";

            if (!DA.GetData(0, ref baseGeom)) return;
            DA.GetData(1, ref setoutPt);
            DA.GetData(2, ref gridType);
            DA.GetData(3, ref x_dim);
            DA.GetData(4, ref y_dim);
            DA.GetData(5, ref grout);
            if (grout < 0) grout = 0;"""

new_solve1 = """            double x_dim = 1.0, y_dim = 1.0, grout = 0.0, rot = 0.0;
            string gridType = "rectangular";
            Rhino.Geometry.Vector3d dir = Rhino.Geometry.Vector3d.Unset;

            if (!DA.GetData(0, ref baseGeom)) return;
            DA.GetData(1, ref setoutPt);
            DA.GetData(2, ref gridType);
            DA.GetData(3, ref x_dim);
            DA.GetData(4, ref y_dim);
            DA.GetData(5, ref grout);
            DA.GetData(6, ref dir);
            DA.GetData(7, ref rot);
            if (grout < 0) grout = 0;"""
content = content.replace(old_solve1, new_solve1)

old_plane_logic = """            if (boundary == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not extract a valid boundary.");
                return;
            }"""
new_plane_logic = """            if (boundary == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not extract a valid boundary.");
                return;
            }

            if (dir.IsValid && !dir.IsZero)
            {
                Rhino.Geometry.Vector3d projDir = originPlane.ClosestPoint(originPlane.Origin + dir) - originPlane.Origin;
                if (projDir.Length > 1e-6)
                {
                    projDir.Unitize();
                    Rhino.Geometry.Vector3d yAxis = Rhino.Geometry.Vector3d.CrossProduct(originPlane.ZAxis, projDir);
                    originPlane = new Plane(originPlane.Origin, projDir, yAxis);
                }
            }

            if (rot != 0.0)
            {
                originPlane.Rotate(rot, originPlane.ZAxis);
            }"""
content = content.replace(old_plane_logic, new_plane_logic)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)

