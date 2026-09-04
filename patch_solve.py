import re

with open("Components/TileGridGenerator.cs", "r") as f:
    content = f.read()

new_logic = """
            GeometryBase baseGeom = null;
            Point3d setoutPt = Point3d.Unset;
            double x_dim = 1.0, y_dim = 1.0;
            string gridType = "rectangular";

            if (!DA.GetData(0, ref baseGeom)) return;
            DA.GetData(1, ref setoutPt);
            DA.GetData(2, ref gridType);
            DA.GetData(3, ref x_dim);
            DA.GetData(4, ref y_dim);

            Curve boundary = null;
            Plane originPlane = Plane.WorldXY;

            if (baseGeom is Curve crv)
            {
                boundary = crv;
                if (!boundary.IsClosed)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base curve must be closed.");
                    return;
                }
                if (!boundary.TryGetPlane(out originPlane))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base curve must be planar.");
                    return;
                }
                
                var amp = Rhino.Geometry.AreaMassProperties.Compute(boundary);
                Point3d centroid = setoutPt.IsValid ? setoutPt : (amp != null ? amp.Centroid : boundary.PointAtStart);
                originPlane.Origin = originPlane.ClosestPoint(centroid);
            }
            else if (baseGeom is Surface srf)
            {
                if (!srf.IsPlanar())
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base surface must be planar.");
                    return;
                }
                var brep = Brep.CreateFromSurface(srf);
                Curve[] naked = brep.DuplicateNakedEdgeCurves(true, false);
                if (naked != null && naked.Length > 0)
                {
                    Curve[] joined = Curve.JoinCurves(naked);
                    if (joined != null && joined.Length > 0) boundary = joined[0];
                }
                
                double u = srf.Domain(0).Mid;
                double v = srf.Domain(1).Mid;
                Vector3d normal = srf.NormalAt(u, v);
                
                var amp = Rhino.Geometry.AreaMassProperties.Compute(brep);
                Point3d centroid = setoutPt.IsValid ? setoutPt : (amp != null ? amp.Centroid : srf.PointAt(u, v));
                Plane tempPlane = new Plane(centroid, normal);
                originPlane = tempPlane;
            }
            else if (baseGeom is Brep brep)
            {
                if (brep.Faces.Count != 1 || !brep.Faces[0].IsPlanar())
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base Brep must be a single planar face.");
                    return;
                }
                Curve[] naked = brep.DuplicateNakedEdgeCurves(true, false);
                if (naked != null && naked.Length > 0)
                {
                    Curve[] joined = Curve.JoinCurves(naked);
                    if (joined != null && joined.Length > 0) boundary = joined[0];
                }
                
                Surface srf = brep.Faces[0].UnderlyingSurface();
                double u = srf.Domain(0).Mid;
                double v = srf.Domain(1).Mid;
                Vector3d normal = srf.NormalAt(u, v);
                
                var amp = Rhino.Geometry.AreaMassProperties.Compute(brep);
                Point3d centroid = setoutPt.IsValid ? setoutPt : (amp != null ? amp.Centroid : srf.PointAt(u, v));
                Plane tempPlane = new Plane(centroid, normal);
                originPlane = tempPlane;
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Base Geometry must be a Curve, Surface, or Brep.");
                return;
            }

            if (boundary == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not extract a valid boundary.");
                return;
            }
"""

old_logic_pattern = r'Curve boundary = null;.*?AddRuntimeMessage\(GH_RuntimeMessageLevel\.Error, "Boundary must be a closed curve\."\);\s*return;\s*\}'
content = re.sub(old_logic_pattern, new_logic.strip(), content, flags=re.DOTALL)

with open("Components/TileGridGenerator.cs", "w") as f:
    f.write(content)

