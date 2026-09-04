with open("MeshHeightAnalysis.cs.backup", "r") as f:
    lines = f.readlines()

new_lines = []
skip = False
for i, line in enumerate(lines):
    if "pManager.AddPlaneParameter(\"RotationPlane\"" in line:
        continue
    if "pManager.AddIntegerParameter(\"SectionsX\"" in line:
        continue
    if "pManager.AddIntegerParameter(\"SectionsY\"" in line:
        continue
    if "pManager.AddBooleanParameter(\"LayoutFlat\"" in line:
        continue
    if "pManager.AddCurveParameter(\"SectionOutlinesX\"" in line:
        continue
    if "pManager.AddCurveParameter(\"SectionOutlinesY\"" in line:
        continue
    if "pManager.AddCurveParameter(\"FlatSectionsX\"" in line:
        continue
    if "pManager.AddCurveParameter(\"FlatSectionsY\"" in line:
        continue
    if "pManager.AddTextParameter(\"LabelText3D\"" in line:
        continue
    if "pManager.AddPointParameter(\"LabelPoints3D\"" in line:
        continue
    if "pManager.AddTextParameter(\"LabelTextFlat\"" in line:
        continue
    if "pManager.AddPointParameter(\"LabelPointsFlat\"" in line:
        continue
    if "pManager.AddTextParameter(\"SectionMetadata\"" in line:
        continue
    
    # In SolveInstance, skip reading inputs 7, 8, 9, 10
    if "Plane secPlane = Plane.WorldXY;" in line:
        skip = True
    if skip and "var localPeaks = new GH_Structure<GH_Point>();" in line:
        skip = False
    if skip:
        continue
        
    # skip the variables
    if "GH_Structure<GH_Curve> sectionOutlinesX" in line or "var sectionOutlinesX" in line: continue
    if "GH_Structure<GH_Curve> sectionOutlinesY" in line or "var sectionOutlinesY" in line: continue
    if "GH_Structure<GH_Curve> flatSectionsX" in line or "var flatSectionsX" in line: continue
    if "GH_Structure<GH_Curve> flatSectionsY" in line or "var flatSectionsY" in line: continue
    if "GH_Structure<GH_String> labelText3D" in line or "var labelText3D" in line: continue
    if "GH_Structure<GH_Point> labelPoints3D" in line or "var labelPoints3D" in line: continue
    if "GH_Structure<GH_String> labelTextFlat" in line or "var labelTextFlat" in line: continue
    if "GH_Structure<GH_Point> labelPointsFlat" in line or "var labelPointsFlat" in line: continue
    if "GH_Structure<GH_String> sectionMetadata" in line or "var sectionMetadata" in line: continue
    if "int totalSectionsX = 0;" in line: continue
    if "int totalSectionsY = 0;" in line: continue

    # bounding box
    if "if (secCountX > 0 || secCountY > 0)" in line:
        skip = True
    if skip and "if (avoidBounds && isNakedEdge[vIdx]) continue;" in line:
        skip = False
        
    # The actual section cutting blocks
    if "if (secCountX > 0 && (bMaxY - bMinY) > 1e-5)" in line:
        skip = True
    if skip and "DA.SetData(0, instructions);" in line:
        skip = False
        
    if skip:
        continue
        
    if "DA.SetDataTree(10, sectionOutlinesX);" in line: continue
    if "DA.SetDataTree(11, sectionOutlinesY);" in line: continue
    if "DA.SetDataTree(12, flatSectionsX);" in line: continue
    if "DA.SetDataTree(13, flatSectionsY);" in line: continue
    if "DA.SetDataTree(14, labelText3D);" in line: continue
    if "DA.SetDataTree(15, labelPoints3D);" in line: continue
    if "DA.SetDataTree(16, labelTextFlat);" in line: continue
    if "DA.SetDataTree(17, labelPointsFlat);" in line: continue
    if "DA.SetDataTree(18, sectionMetadata);" in line: continue

    if "DA.SetData(19, legendObj.ToString());" in line:
        new_lines.append(line.replace("19", "10"))
        continue

    # HUD message
    if "string layoutStatus = layoutFlat ? \"ON (Bi-Directional Unroll)\" : \"OFF\";" in line: continue
    if "Message = $\"TERRAIN ANALYZER" in line:
        new_lines.append('            Message = "TERRAIN ANALYZER\\n";\n')
        new_lines.append('            Message += $"Time: {t_start.ElapsedMilliseconds:F2} ms\\n";\n')
        new_lines.append('            Message += "---\\n";\n')
        new_lines.append('            Message += $"Area: {Math.Round(totalTerrainArea, 2)}\\n";\n')
        new_lines.append('            Message += $"Relief (\\u0394Z): {terrainRelief}\\n";\n')
        new_lines.append('            Message += $"Avg Elev: {meanElevation}\\n";\n')
        new_lines.append('            Message += $"Max Height: {Math.Round(globalTerrainZMax, 2)}\\n";\n')
        new_lines.append('            Message += $"Min Height: {Math.Round(globalTerrainZMin, 2)}\\n";\n')
        new_lines.append('            Message += $"Peaks: {totalPeaksFound} | Valleys: {totalValleysFound}";\n')
        continue

    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 10," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 11," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 12," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 13," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 14," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 15," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 16," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 17," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 8," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 9," in line: continue
    if "Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 10," in line: continue
    
    new_lines.append(line)

# Let's fix instructions text
for i, line in enumerate(new_lines):
    if "string instructions = \"Analyzes mesh extremes" in line:
        new_lines[i] = '            string instructions = "Analyzes mesh extremes and generates topo heatmaps.";\n'

with open("Components/MeshHeightAnalysis.cs", "w") as f:
    f.writelines(new_lines)

