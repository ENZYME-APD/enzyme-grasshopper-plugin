import re

with open('Components/MeshHeightAnalysis.cs', 'r') as f:
    content = f.read()

# Replace Inputs
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 10.0, 5, 330, -140);',
    'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 20.0, 5, 330, -140);'
)
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 2.0, 0, 330, 60);',
    'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 10.0, 5, 330, 60);'
)
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 2.0, 0, 330, 100);',
    'Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 10.0, 4, 330, 100);'
)

# Replace Outputs
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "point", 220, -285);',
    'Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 1, System.Drawing.Color.Blue, 10.0, 350, -285);'
)
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "point", 220, -240);',
    'Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 3, System.Drawing.Color.Blue, 5.0, 350, -240);'
)
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "point", 220, -195);',
    'Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 5, System.Drawing.Color.Red, 10.0, 350, -195);'
)
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 7, "point", 220, -150);',
    'Enzyme.Utils.AutoWireHelper.WirePointDisplay(this, document, 7, System.Drawing.Color.Red, 5.0, 350, -150);'
)
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 9, System.Drawing.Color.FromArgb(230, 230, 230), 220, -105);',
    'Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 9, "mesh", 220, -105);'
)

# Wait, the user image shows `GlobalMaxPoint` is ABOVE `LocalPeaks` in the display group?
# Oh wait, the image wiring order:
# GlobalMaxPoint (output 3) -> Top-most Point Display
# LocalPeaks (output 1) -> Second Point Display
# So the Point Display for output 3 should have Y=-285? No, the component outputs have fixed Y coords. 
# Output 1 is Y=-285 (higher up physically on the component). 
# Output 3 is Y=-240. 
# So Output 1 should have Y=-285. Output 3 should have Y=-240. This matches my offsets. 

with open('Components/MeshHeightAnalysis.cs', 'w') as f:
    f.write(content)

