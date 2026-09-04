import re

with open("Components/TerrainGeneratorPro.cs", "r") as f:
    ts = f.read()

# Update Red Box (MaxHeight, MinHeight) to 1 decimal
ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 1, 0.0, 200, 100.0, 330, -400);',
                'Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 1, 0.0, 200.0, 100.0, 330, -400);')
ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 2.0, 0.0, 330, -360);',
                'Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, document, 2, -100.0, 100.0, 0.0, 330, -360);')

# Update Blue Box to Integer
ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 84, 42, 330, -320);',
                'Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 3, 0, 84, 42, 330, -320);')

# For ContourStep, since max was 2.0, a max of 2 as int is too small if they want to step by 1. Wait, contour steps can be 1, 2, 5, 10. Let's make max 10.
ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 2.0, 1.0, 330, -140);',
                'Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 6, 0, 10, 1, 330, -140);')

# MainStep max was 10.0, let's make it 50
ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 10.0, 5.0, 330, -100);',
                'Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 7, 0, 50, 5, 330, -100);')

ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 200, 100, 330, -60);',
                'Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 9, 0, 200, 100, 330, -60);')

ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 12, 0.0, 60, 30.0, 330, 60);',
                'Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 12, 0, 90, 30, 330, 60);')

# I will leave TreeMsk, TreeDns, TreeSeed, TreeZMin, TreeZMax as they were (WireSlider, which gives 2 decimals)
# Actually, wait, TreeSeed is an integer slider inherently, let's make it explicitly WireSliderInt
ts = ts.replace('Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 18, 0.0, 24690, 12345, 330, 300);',
                'Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 18, 0, 24690, 12345, 330, 300);')

with open("Components/TerrainGeneratorPro.cs", "w") as f:
    f.write(ts)
