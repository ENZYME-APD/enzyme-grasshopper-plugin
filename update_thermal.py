import re

with open("Components/ThermalComfortAnalyzer.cs", "r") as f:
    text = f.read()

# 1. Inject the CreateAttributes method before AddedToDocument
if "public override void CreateAttributes()" not in text:
    create_attr = """        public override void CreateAttributes()
        {
            m_attributes = new Enzyme.Utils.ComponentHUD(this);
        }

"""
    text = text.replace("public override void AddedToDocument", create_attr + "        public override void AddedToDocument")

# 2. Replace PerformAutoWire method completely
new_autowire = """        private void PerformAutoWire(GH_Document document)
        {
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, document, 0, false, 362, -159);
            Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 1, "mesh", 252, -108);
            Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 2, "data", 251, -74);
            Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 3, "point", 252, -34);
            
            Enzyme.Utils.AutoWireHelper.WireSliderInt(this, document, 4, 0, 100, 50, 398, -11);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, -10.0, 45.0, 20.0, 416, 29);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, -10.0, 45.0, DEFAULT_IDEAL_TEMPERATURE, 439, 69);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 7, 0.0, 1.0, DEFAULT_COMFORT_TOLERANCE, 441, 109);
            
            Enzyme.Utils.AutoWireHelper.WireGeneratedColorPalette(this, document, 8, 313, 228);
            
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "point", 189, -72);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "point", 189, -36);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 2, "mesh", 188, 0);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "number", 187, 36);
        }"""

text = re.sub(r'private void PerformAutoWire\(GH_Document document\)\s*\{[^\}]+\}', new_autowire, text, flags=re.DOTALL)

with open("Components/ThermalComfortAnalyzer.cs", "w") as f:
    f.write(text)
