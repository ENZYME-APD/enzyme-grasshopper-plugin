with open("MeshHeightAnalysis.cs.backup", "r") as f:
    orig = f.read()

# 1. Inputs
orig = orig.replace(
'''        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "M", "The meshes to analyze.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("SearchRings", "R", "Topological radius in rings.", GH_ParamAccess.item, 5);
            pManager.AddNumberParameter("ProminenceLimit", "P", "Minimum Z-delta to be considered a peak/valley.", GH_ParamAccess.item, 0.5);
            pManager.AddColourParameter("CustomColors", "C", "Custom colormap list.", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("CullGlobals", "CG", "Toggle to remove the absolute highest/lowest points.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("AvoidBoundaries", "AB", "Toggle to ignore naked edge vertices.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("EnableHeatmap", "EH", "Toggle to compute and output the vertex heatmap mesh.", GH_ParamAccess.item, true);
            pManager.AddPlaneParameter("RotationPlane", "RP", "Orientation plane for the bounding box sectioning.", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddIntegerParameter("SectionsX", "SX", "Number of sections running parallel to the X-axis.", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("SectionsY", "SY", "Number of sections running parallel to the Y-axis.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("LayoutFlat", "LF", "Toggle to generate 2D XY print layouts next to the mesh.", GH_ParamAccess.item, false);
        }''',
'''        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("TargetMeshes", "M", "The meshes to analyze.", GH_ParamAccess.tree);
            pManager.AddIntegerParameter("SearchRings", "R", "Topological radius in rings.", GH_ParamAccess.item, 5);
            pManager.AddNumberParameter("ProminenceLimit", "P", "Minimum Z-delta to be considered a peak/valley.", GH_ParamAccess.item, 0.5);
            pManager.AddColourParameter("CustomColors", "C", "Custom colormap list.", GH_ParamAccess.list);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("CullGlobals", "CG", "Toggle to remove the absolute highest/lowest points.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("AvoidBoundaries", "AB", "Toggle to ignore naked edge vertices.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("EnableHeatmap", "EH", "Toggle to compute and output the vertex heatmap mesh.", GH_ParamAccess.item, true);
        }'''
)

# 2. Outputs
orig = orig.replace(
'''        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions", "I", "Component documentation and usage manual.", GH_ParamAccess.item);
            pManager.AddPointParameter("LocalPeaks", "LP", "Output points for local highs.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("PeakElevations", "PE", "Z-values for local highs.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMaxPoint", "GMP", "Absolute highest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMaxElevation", "GME", "Absolute highest Z-value.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LocalValleys", "LV", "Output points for local lows.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("ValleyElevations", "VE", "Z-values for local lows.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMinPoint", "GMI", "Absolute lowest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMinElevation", "GMIE", "Absolute lowest Z-value.", GH_ParamAccess.tree);
            pManager.AddMeshParameter("HeatmapMeshes", "HM", "The vertex-colored duplicate mesh.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("SectionOutlinesX", "SOX", "3D Polylines running parallel to the X-axis.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("SectionOutlinesY", "SOY", "3D Polylines running parallel to the Y-axis.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsX", "FSX", "2D X-Sections stacked downwards (-Y direction).", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsY", "FSY", "2D Y-Sections stacked leftwards (-X direction).", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelText3D", "LT3D", "Text strings for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPoints3D", "LP3D", "Points for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelTextFlat", "LTF", "Text strings for the flattened section layout.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPointsFlat", "LPF", "Points for the flattened section layout.", GH_ParamAccess.tree);
            pManager.AddTextParameter("SectionMetadata", "SM", "Dictionary keys containing spatial transform & ID data.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "JSON Legend Data", GH_ParamAccess.item);
        }''',
'''        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Instructions", "I", "Component documentation and usage manual.", GH_ParamAccess.item);
            pManager.AddPointParameter("LocalPeaks", "LP", "Output points for local highs.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("PeakElevations", "PE", "Z-values for local highs.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMaxPoint", "GMP", "Absolute highest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMaxElevation", "GME", "Absolute highest Z-value.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LocalValleys", "LV", "Output points for local lows.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("ValleyElevations", "VE", "Z-values for local lows.", GH_ParamAccess.tree);
            pManager.AddPointParameter("GlobalMinPoint", "GMI", "Absolute lowest point on the mesh.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("GlobalMinElevation", "GMIE", "Absolute lowest Z-value.", GH_ParamAccess.tree);
            pManager.AddMeshParameter("HeatmapMeshes", "HM", "The vertex-colored duplicate mesh.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Color Legend", "Color Legend", "JSON Legend Data", GH_ParamAccess.item);
        }'''
)

# 3. AddedToDocument
orig = orig.replace(
'''        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 0, "mesh", 200, -20);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 1, 1, 20, 5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 5.0, 0.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, true, 210, 140);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 5, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, true, 210, 220);
                
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 8, 0, 20, 5, 330, 300);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 9, 0, 20, 4, 330, 340);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10, true, 210, 380);
                
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "panel", 350, -320);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "point", 350, -280);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "panel", 350, -240);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 350, -200);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "panel", 350, -160);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "point", 350, -120);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 6, "panel", 350, -80);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 7, "point", 350, -40);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 8, "panel", 350, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 9, "mesh", 350, 40);
                
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 10, "curve", 350, 120);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 11, "curve", 350, 160);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 12, "curve", 350, 200);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 13, "curve", 350, 240);
                
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 14, "panel", 350, 280);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 15, "point", 350, 320);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 16, "panel", 350, 360);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 17, "point", 350, 400);
            }
        }''',
'''        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 0, "mesh", 200, -20);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 1, 1, 20, 5, 330, 20);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 5.0, 0.5, 330, 60);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, true, 210, 140);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 5, false, 210, 180);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 6, true, 210, 220);
                
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "panel", 350, -320);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "point", 350, -280);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "panel", 350, -240);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 350, -200);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "panel", 350, -160);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "point", 350, -120);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 6, "panel", 350, -80);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 7, "point", 350, -40);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 8, "panel", 350, 0);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 9, "mesh", 350, 40);
            }
        }'''
)

# 4. Remove section inputs logic in SolveInstance
orig = orig.replace(
'''            Plane secPlane = Plane.WorldXY;
            DA.GetData(7, ref secPlane);

            int secCountX = 0;
            DA.GetData(8, ref secCountX);

            int secCountY = 0;
            DA.GetData(9, ref secCountY);

            bool layoutFlat = false;
            DA.GetData(10, ref layoutFlat);''',
''
)

orig = orig.replace(
'''            var sectionOutlinesX = new GH_Structure<GH_Curve>();
            var sectionOutlinesY = new GH_Structure<GH_Curve>();
            var flatSectionsX = new GH_Structure<GH_Curve>();
            var flatSectionsY = new GH_Structure<GH_Curve>();

            var labelText3D = new GH_Structure<GH_String>();
            var labelPoints3D = new GH_Structure<GH_Point>();
            var labelTextFlat = new GH_Structure<GH_String>();
            var labelPointsFlat = new GH_Structure<GH_Point>();
            var sectionMetadata = new GH_Structure<GH_String>();

            int totalSectionsX = 0;
            int totalSectionsY = 0;''',
''
)

# 5. Remove bounding box logic
orig = orig.replace(
'''                        if (secCountX > 0 || secCountY > 0)
                        {
                            double u, v;
                            if (secPlane.ClosestParameter(pt, out u, out v))
                            {
                                if (u < bMinX) bMinX = u;
                                if (u > bMaxX) bMaxX = u;
                                if (v < bMinY) bMinY = v;
                                if (v > bMaxY) bMaxY = v;
                            }
                        }''',
''
)

# 6. Delete bMinX variables
orig = orig.replace(
'''                    double bMinX = double.MaxValue, bMaxX = double.MinValue;
                    double bMinY = double.MaxValue, bMaxY = double.MinValue;''',
''
)

# 7. Strip block
start_str = "                    if (secCountX > 0 && (bMaxY - bMinY) > 1e-5)"
end_str = "            string instructions = \"Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels.\";"
idx1 = orig.find(start_str)
idx2 = orig.find(end_str)
if idx1 != -1 and idx2 != -1:
    orig = orig[:idx1] + orig[idx2:]

# 8. Set Outputs
orig = orig.replace(
'''            string instructions = "Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels.";
            DA.SetData(0, instructions);
            DA.SetDataTree(1, localPeaks);
            DA.SetDataTree(2, peakElevations);
            DA.SetDataTree(3, globalMaxPoint);
            DA.SetDataTree(4, globalMaxElevation);
            DA.SetDataTree(5, localValleys);
            DA.SetDataTree(6, valleyElevations);
            DA.SetDataTree(7, globalMinPoint);
            DA.SetDataTree(8, globalMinElevation);
            DA.SetDataTree(9, heatmapMeshes);
            DA.SetDataTree(10, sectionOutlinesX);
            DA.SetDataTree(11, sectionOutlinesY);
            DA.SetDataTree(12, flatSectionsX);
            DA.SetDataTree(13, flatSectionsY);
            DA.SetDataTree(14, labelText3D);
            DA.SetDataTree(15, labelPoints3D);
            DA.SetDataTree(16, labelTextFlat);
            DA.SetDataTree(17, labelPointsFlat);
                        DA.SetDataTree(18, sectionMetadata);

            if (enableHeatmap && totalVerticesCount > 0)
            {
                var jColors = new JArray();
                var cList = customColorList.Count > 0 ? customColorList : new List<Color> { Color.Blue, Color.Cyan, Color.Lime, Color.Yellow, Color.Red };
                foreach (var c in cList) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var legendObj = new JObject
                {
                    ["Type"] = "Blocks",
                    ["Title"] = "Mesh Terrain Elevation",
                    ["Colors"] = jColors,
                    ["Labels"] = new JArray($"{globalTerrainZMin:F1}m", $"{globalTerrainZMax:F1}m"),
                    ["SubLabels"] = new JArray($"Relief: {(globalTerrainZMax - globalTerrainZMin):F1}m")
                };
                DA.SetData(19, legendObj.ToString());
            }''',
'''            string instructions = "Analyzes mesh extremes and generates topo heatmaps.";
            DA.SetData(0, instructions);
            DA.SetDataTree(1, localPeaks);
            DA.SetDataTree(2, peakElevations);
            DA.SetDataTree(3, globalMaxPoint);
            DA.SetDataTree(4, globalMaxElevation);
            DA.SetDataTree(5, localValleys);
            DA.SetDataTree(6, valleyElevations);
            DA.SetDataTree(7, globalMinPoint);
            DA.SetDataTree(8, globalMinElevation);
            DA.SetDataTree(9, heatmapMeshes);

            if (enableHeatmap && totalVerticesCount > 0)
            {
                var jColors = new JArray();
                var cList = customColorList.Count > 0 ? customColorList : new List<Color> { Color.Blue, Color.Cyan, Color.Lime, Color.Yellow, Color.Red };
                foreach (var c in cList) jColors.Add(new JObject { ["R"] = c.R, ["G"] = c.G, ["B"] = c.B });
                
                var legendObj = new JObject
                {
                    ["Type"] = "Blocks",
                    ["Title"] = "Mesh Terrain Elevation",
                    ["Colors"] = jColors,
                    ["Labels"] = new JArray($"{globalTerrainZMin:F1}m", $"{globalTerrainZMax:F1}m"),
                    ["SubLabels"] = new JArray($"Relief: {(globalTerrainZMax - globalTerrainZMin):F1}m")
                };
                DA.SetData(10, legendObj.ToString());
            }'''
)

# 9. HUD formatting
orig = orig.replace(
'''            double terrainRelief = totalVerticesCount > 0 ? Math.Round(globalTerrainZMax - globalTerrainZMin, 2) : 0.0;
            double meanElevation = totalVerticesCount > 0 ? Math.Round(totalZSum / totalVerticesCount, 2) : 0.0;
            string layoutStatus = layoutFlat ? "ON (Bi-Directional Unroll)" : "OFF";
            
            Message = $"TERRAIN ANALYZER\\n---\\nArea: {Math.Round(totalTerrainArea, 2)}\\nRelief (ΔZ): {terrainRelief}\\nAvg Elev: {meanElevation}\\n● Peaks: {totalPeaksFound} | ○ Valleys: {totalValleysFound}\\n≡ Sections X: {totalSectionsX} | Y: {totalSectionsY}\\n[] XY Layout: {layoutStatus}";
        }''',
'''            double terrainRelief = totalVerticesCount > 0 ? Math.Round(globalTerrainZMax - globalTerrainZMin, 2) : 0.0;
            double meanElevation = totalVerticesCount > 0 ? Math.Round(totalZSum / totalVerticesCount, 2) : 0.0;
            
            Message = "TERRAIN ANALYZER\\n";
            Message += $"Time: {t_start.ElapsedMilliseconds:F2} ms\\n";
            Message += "---\\n";
            Message += $"Area: {Math.Round(totalTerrainArea, 2)}\\n";
            Message += $"Relief (ΔZ): {terrainRelief}\\n";
            Message += $"Avg Elev: {meanElevation}\\n";
            Message += $"Max Height: {Math.Round(globalTerrainZMax, 2)}\\n";
            Message += $"Min Height: {Math.Round(globalTerrainZMin, 2)}\\n";
            Message += $"Peaks: {totalPeaksFound} | Valleys: {totalValleysFound}";
        }'''
)

with open("Components/MeshHeightAnalysis.cs", "w") as f:
    f.write(orig)
