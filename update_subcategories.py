import re

categories = {
    'Components/BranchConcat.cs': '"Data"',
    'Components/BranchSizeSplit.cs': '"Data"',
    'Components/GroupByKey.cs': '"Data"',
    'Components/RandomTreeSplit.cs': '"Data"',
    'Components/TShuffle.cs': '"Data"',
    
    'Components/CondFillet.cs': '"Curve"',
    'Components/PHeal.cs': '"Curve"',
    'Components/SegDisp.cs': '"Curve"',
    'Components/SortCurvesByAxis.cs': '"Curve"',
    'Components/TopologySplitEdgeClassifier.cs': '"Curve"',
    
    'Components/Clearance.cs': '"MP Analysis"',
    'Components/VArrow.cs': '"MP Analysis"',
    
    # Utilities stay Utilities: AreaConv.cs, ActivateViewSettings.cs, ExportViews.cs, GradientGenerator.cs, PaperSizeToPixels.cs, LayerMat.cs
    
    'Components/PluginInfo.cs': '"Info"'
}

for filepath, new_subcat in categories.items():
    with open(filepath, 'r') as f:
        content = f.read()
    content = content.replace('"Utilities"', new_subcat)
    with open(filepath, 'w') as f:
        f.write(content)

print("Subcategories updated.")
