import re
import os

def patch_file(filepath, search_str, insert_str):
    with open(filepath, 'r') as f:
        content = f.read()
    
    content = content.replace(search_str, insert_str + "\n" + search_str)
    with open(filepath, 'w') as f:
        f.write(content)

# HydroDEM
patch_file(
    'Components/HydroDEM.cs', 
    '            DA.SetDataList(0, streams);',
    '            Message = $"Hydro-DEM\\n---\\nThreshold: {threshold}\\nStreams: {streams.Count}";'
)

# KeypointFinder
patch_file(
    'Components/KeypointFinder.cs',
    '            DA.SetDataList(0, keypoints);',
    '            Message = $"Keypoint Finder\\n---\\nSmoothing: {window}\\nFound: {keypoints.Count}";'
)

# KeylinePattern
patch_file(
    'Components/KeylinePattern.cs',
    '            DA.SetDataList(0, keylines);',
    '            Message = $"Keyline Pattern\\n---\\nSpacing: {spacing}m\\nCount: {count}\\nGenerated: {keylines.Count}";'
)

# DataVisualizer
patch_file(
    'Components/DataVisualizer.cs',
    '            DA.SetData(0, masterMesh);',
    '            string typeName = type == 0 ? "Bar Chart" : (type == 1 ? "Flat Dot" : "Sphere");\n            Message = $"Data Visualizer\\n---\\nType: {typeName}\\nPoints: {pts.Count}";'
)
