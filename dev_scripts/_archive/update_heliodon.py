with open("Components/Heliodon.cs", "r") as f:
    content = f.read()

if 'pManager.AddCurveParameter("Base Circle"' not in content:
    content = content.replace('pManager.AddPointParameter("Sun Points", "Points", "Visual hourly sun positions.", GH_ParamAccess.list);',
        'pManager.AddPointParameter("Sun Points", "Points", "Visual hourly sun positions.", GH_ParamAccess.list);\n            pManager.AddCurveParameter("Base Circle", "Circle", "A flat circle framing the heliodon radius.", GH_ParamAccess.item);')

if 'DA.SetData(3, new Rhino.Geometry.Circle' not in content:
    content = content.replace('DA.SetDataList(2, points);',
        'DA.SetDataList(2, points);\n            DA.SetData(3, new Rhino.Geometry.Circle(new Rhino.Geometry.Plane(center, Rhino.Geometry.Vector3d.ZAxis), radius).ToNurbsCurve());')

with open("Components/Heliodon.cs", "w") as f:
    f.write(content)
