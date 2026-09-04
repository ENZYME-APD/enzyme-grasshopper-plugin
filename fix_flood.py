import re

with open("Components/GlobalFloodEngine.cs", "r") as f:
    text = f.read()

# 1. Add output parameter
old_output = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("FloodMesh", "FM", "Flooded terrain heatmap mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("WaterDepths", "WD", "Water depths at each vertex in meters", GH_ParamAccess.list);
        }'''
new_output = '''        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("FloodMesh", "FM", "Flooded terrain heatmap mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("WaterDepths", "WD", "Water depths at each vertex in meters", GH_ParamAccess.list);
            pManager.AddPointParameter("AnalysisPoints", "Pts", "Points corresponding to the water depth values", GH_ParamAccess.list);
        }'''
text = text.replace(old_output, new_output)

# 2. Add analysisPoints in SolveInstance
old_loop = '''            List<double> finalDepths = new List<double>(numVertices);
            int floodedVertexCount = 0;
            double maxDepth = 0.0;

            for (int i = 0; i < numVertices; i++)
            {
                int topIdx = outMesh.TopologyVertices.TopologyVertexIndex(i);
                double depth = topWaterDepth[topIdx];
                finalDepths.Add(depth);
                if (depth > maxDepth) maxDepth = depth;
            }'''
new_loop = '''            List<double> finalDepths = new List<double>(numVertices);
            List<Point3d> analysisPoints = new List<Point3d>(numVertices);
            int floodedVertexCount = 0;
            double maxDepth = 0.0;

            for (int i = 0; i < numVertices; i++)
            {
                int topIdx = outMesh.TopologyVertices.TopologyVertexIndex(i);
                double depth = topWaterDepth[topIdx];
                finalDepths.Add(depth);
                
                // Add the actual point
                var pt = outMesh.Vertices[i];
                analysisPoints.Add(new Point3d(pt.X, pt.Y, pt.Z));
                
                if (depth > maxDepth) maxDepth = depth;
            }'''
text = text.replace(old_loop, new_loop)

# 3. Output the points
old_set = '''            DA.SetData(0, outMesh);
            DA.SetDataList(1, finalDepths);'''
new_set = '''            DA.SetData(0, outMesh);
            DA.SetDataList(1, finalDepths);
            DA.SetDataList(2, analysisPoints);'''
text = text.replace(old_set, new_set)

with open("Components/GlobalFloodEngine.cs", "w") as f:
    f.write(text)
