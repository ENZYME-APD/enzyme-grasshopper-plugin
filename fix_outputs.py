import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

reg_out = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "T", "Modified terrain mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("Road Table", "R", "Asphalt surface mesh", GH_ParamAccess.list);
            pManager.AddMeshParameter("Cut Volume", "C", "Excavated earth volume", GH_ParamAccess.list);
            pManager.AddMeshParameter("Fill Volume", "F", "Added earth volume", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lanes", "L", "Lane centerlines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Railings", "B", "Road boundaries and shoulders", GH_ParamAccess.list);
            pManager.AddCurveParameter("Pillars", "P", "Bridge pillar lines", GH_ParamAccess.list);
        }"""

content = re.sub(r'protected override void RegisterOutputParams.*?}', reg_out, content, flags=re.DOTALL)

set_out = """            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, volumes); // Cut volume placeholder
            DA.SetDataList(3, volumes); // Fill volume placeholder
            DA.SetDataList(4, laneCurves);
            DA.SetDataList(5, railingCurves);
            DA.SetDataList(6, pillars);"""

content = re.sub(r'DA\.SetData\(0, modTerrain\);.*?DA\.SetDataList\(7, volumes\); // Cut/fill placeholder', set_out, content, flags=re.DOTALL)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
