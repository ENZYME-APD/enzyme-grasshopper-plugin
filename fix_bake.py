import re

with open("Components/TerrainSections.cs", "r") as f:
    ts = f.read()

# Locate the baking block
start_str = "                    string parent_name = \"TerrainSections\";"
end_str = "                }"
# We'll just slice it manually
start_idx = ts.find(start_str)
end_idx = ts.find(end_str, start_idx) # wait, "}" might be the end of if (doc != null)
# Let's find "DA.SetDataTree(0, sectionOutlinesX);" and replace up to there
end_idx = ts.find("            DA.SetDataTree(0, sectionOutlinesX);")

old_bake_block = ts[start_idx:end_idx]

new_bake_block = '''                    string parent_name = "Site Sections";
                    int parent_idx = -1;
                    foreach (var layer in doc.Layers) { if (layer.Name == parent_name && layer.ParentLayerId == System.Guid.Empty) { parent_idx = layer.Index; break; } }
                    if (parent_idx < 0)
                    {
                        var parent_layer = new Rhino.DocObjects.Layer();
                        parent_layer.Name = parent_name;
                        parent_idx = doc.Layers.Add(parent_layer);
                    }
                    var parentId = doc.Layers[parent_idx].Id;
                    
                    string layer3d_name = "Terrain Sections";
                    int layer3d_idx = -1;
                    foreach (var layer in doc.Layers) { if (layer.Name == layer3d_name && layer.ParentLayerId == parentId) { layer3d_idx = layer.Index; break; } }
                    if (layer3d_idx < 0)
                    {
                        var layer3d = new Rhino.DocObjects.Layer();
                        layer3d.Name = layer3d_name;
                        layer3d.ParentLayerId = parentId;
                        layer3d_idx = doc.Layers.Add(layer3d);
                    }
                    
                    string layer2d_name = "Unrolled Sections";
                    int layer2d_idx = -1;
                    foreach (var layer in doc.Layers) { if (layer.Name == layer2d_name && layer.ParentLayerId == parentId) { layer2d_idx = layer.Index; break; } }
                    if (layer2d_idx < 0)
                    {
                        var layer2d = new Rhino.DocObjects.Layer();
                        layer2d.Name = layer2d_name;
                        layer2d.ParentLayerId = parentId;
                        layer2d_idx = doc.Layers.Add(layer2d);
                    }
                    
                    var groupIndex = doc.Groups.Add(bakeName);

                    // Bake 3D sections
                    for (int i = 0; i < sectionOutlinesX.Branches.Count; i++)
                    {
                        foreach (var ghCrv in sectionOutlinesX.Branches[i])
                        {
                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = layer3d_idx;
                            if (!string.IsNullOrEmpty(bakeName)) attr.SetUserString("ElefrontBakeName", bakeName);
                            attr.AddToGroup(groupIndex);
                            doc.Objects.AddCurve(ghCrv.Value, attr);
                        }
                    }
                    for (int i = 0; i < sectionOutlinesY.Branches.Count; i++)
                    {
                        foreach (var ghCrv in sectionOutlinesY.Branches[i])
                        {
                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = layer3d_idx;
                            if (!string.IsNullOrEmpty(bakeName)) attr.SetUserString("ElefrontBakeName", bakeName);
                            attr.AddToGroup(groupIndex);
                            doc.Objects.AddCurve(ghCrv.Value, attr);
                        }
                    }
                    
                    // Bake 2D sections
                    for (int i = 0; i < flatSectionsX.Branches.Count; i++)
                    {
                        foreach (var ghCrv in flatSectionsX.Branches[i])
                        {
                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = layer2d_idx;
                            if (!string.IsNullOrEmpty(bakeName)) attr.SetUserString("ElefrontBakeName", bakeName);
                            attr.AddToGroup(groupIndex);
                            doc.Objects.AddCurve(ghCrv.Value, attr);
                        }
                    }
                    for (int i = 0; i < flatSectionsY.Branches.Count; i++)
                    {
                        foreach (var ghCrv in flatSectionsY.Branches[i])
                        {
                            var attr = new Rhino.DocObjects.ObjectAttributes();
                            attr.LayerIndex = layer2d_idx;
                            if (!string.IsNullOrEmpty(bakeName)) attr.SetUserString("ElefrontBakeName", bakeName);
                            attr.AddToGroup(groupIndex);
                            doc.Objects.AddCurve(ghCrv.Value, attr);
                        }
                    }
                }
            }

'''

ts = ts.replace(old_bake_block, new_bake_block)

with open("Components/TerrainSections.cs", "w") as f:
    f.write(ts)
