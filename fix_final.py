import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# 1. Output ordering in RegisterOutputParams
old_out = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "T", "Modified terrain mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("Road Table", "R", "Asphalt surface mesh", GH_ParamAccess.list);
            pManager.AddMeshParameter("Cut Volume", "C", "Excavated earth volume", GH_ParamAccess.list);
            pManager.AddMeshParameter("Fill Volume", "F", "Added earth volume", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lanes", "L", "Lane centerlines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Railings", "B", "Road boundaries and shoulders", GH_ParamAccess.list);
            pManager.AddCurveParameter("Pillars", "P", "Bridge pillar lines", GH_ParamAccess.list);
        }"""
new_out = """        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Terrain", "T", "Modified terrain mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("Road Table", "R", "Asphalt surface mesh", GH_ParamAccess.list);
            pManager.AddCurveParameter("Lanes", "L", "Lane centerlines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Railings", "B", "Road boundaries and shoulders", GH_ParamAccess.list);
            pManager.AddCurveParameter("Pillars", "P", "Bridge pillar lines", GH_ParamAccess.list);
            pManager.AddMeshParameter("Cut Volume", "C", "Excavated earth volume", GH_ParamAccess.list);
            pManager.AddMeshParameter("Fill Volume", "F", "Added earth volume", GH_ParamAccess.list);
        }"""
content = content.replace(old_out, new_out)

# 2. Output ordering in SetDataList
old_set = """            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, cutVols);
            DA.SetDataList(3, fillVols);
            DA.SetDataList(4, laneCurves);
            DA.SetDataList(5, railingCurves);
            DA.SetDataList(6, pillars);"""
new_set = """            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, laneCurves);
            DA.SetDataList(3, railingCurves);
            DA.SetDataList(4, pillars);
            DA.SetDataList(5, cutVols);
            DA.SetDataList(6, fillVols);"""
content = content.replace(old_set, new_set)

# 3. Input Autowiring
old_auto = """            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 1, 2, 2, 330, -60);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 1, 6, 2, 330, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 1.0, 10.0, 3.5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 5.0, 1.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 1.0, 20.0, 5.0, 330, 100);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 5.0, 100.0, 20.0, 330, 140);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 10.0, 80.0, 45.0, 330, 180);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.5, 10.0, 2.0, 330, 220);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 10, 0.0, 20.0, 5.0, 330, 260);
                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 11, true, 330, 300);
            }"""
new_auto = """            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 1, 2, 2, 330, -60);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 1, 6, 2, 330, -20);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 4, 1.0, 10.0, 3.5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 5, 0.0, 5.0, 1.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 6, 1.0, 20.0, 5.0, 330, 100);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 7, 5.0, 100.0, 20.0, 330, 140);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 8, 10, 80, 45, 330, 180);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 9, 0.5, 10.0, 2.0, 330, 220);
                Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 10, 0.0, 20.0, 5.0, 330, 260);
                Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 11, true, 330, 300);

                // Autowire Outputs
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "mesh", -250, -60);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "mesh", -250, -20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "curve", -250, 20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", -250, 60);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", -250, 100);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "mesh", -250, 140);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 6, "mesh", -250, 180);
            }"""
content = content.replace(old_auto, new_auto)

# 4. HUD String
old_msg = 'Message = $"Road Generator\\\\n---\\\\nLanes: {totalLanes}\\\\nWidth: {totalHalfWidth*2}m\\\\nTime: {stopwatch.ElapsedMilliseconds} ms";'
new_msg = 'Message = $"Road Generator\\n---\\nLanes: {totalLanes}\\nWidth: {totalHalfWidth*2}m\\nTime: {stopwatch.ElapsedMilliseconds} ms";'
content = content.replace(old_msg, new_msg)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
