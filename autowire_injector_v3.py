import os
import re

TARGET_CATEGORIES = ["Masterplan", "Terrain"]
COMPONENTS_DIR = "Components"

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Check if category matches
    match = re.search(r'base\s*\(\s*".*?"\s*,\s*".*?"\s*,\s*".*?"\s*,\s*".*?"\s*,\s*"([^"]+)"\s*\)', content)
    if not match: return False
    if match.group(1) not in TARGET_CATEGORIES: return False

    print(f"Processing: {filepath}")

    # Remove the existing AddedToDocument block if it exists
    content = re.sub(r'\s*public override void AddedToDocument.*?protected override void RegisterInputParams', 
                     '\n\n        protected override void RegisterInputParams', 
                     content, flags=re.DOTALL)

    # Find RegisterInputParams
    input_match = re.search(r'protected override void RegisterInputParams[^{]*\{([^}]+)\}', content)
    if not input_match: return False
    inputs_block = input_match.group(1)
    
    # Extract params
    param_idx = 0
    auto_wire_lines = []
    y_offset = -150
    
    for line in inputs_block.split(';'):
        line = line.strip()
        if not line.startswith('pManager.Add'):
            if 'pManager[' in line and '.Optional' in line:
                pass
            continue
            
        desc_match = re.search(r'pManager\.Add[A-Za-z]+Parameter\s*\(\s*".*?"\s*,\s*".*?"\s*,\s*"(.*?)"', line)
        desc = desc_match.group(1) if desc_match else ""
        
        name_match = re.search(r'pManager\.Add[A-Za-z]+Parameter\s*\(\s*"([^"]+)"', line)
        name = name_match.group(1) if name_match else ""

        # Value list parsing (String)
        str_options = re.findall(r"'([^']+)'", desc)
        # Value list parsing (Int)
        int_options = re.findall(r"(\d+)\s*=\s*([^,]+?)(?:,|$)", desc)
        
        if str_options and "AddTextParameter" in line:
            keys = ", ".join(f'"{opt}"' for opt in str_options)
            vals = ", ".join(f'"\\"{opt}\\""' for opt in str_options) 
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, {param_idx}, new string[]{{{keys}}}, new string[]{{{vals}}}, ix, {y_offset});')
            y_offset += 30
            param_idx += 1
            continue
            
        if int_options and "AddIntegerParameter" in line:
            keys = ", ".join(f'"{opt[1].strip()}"' for opt in int_options)
            vals = ", ".join(f'"{opt[0]}"' for opt in int_options)
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, {param_idx}, new string[]{{{keys}}}, new string[]{{{vals}}}, ix, {y_offset});')
            y_offset += 30
            param_idx += 1
            continue

        if "AddTextParameter" in line and "Pattern" in name:
            # Extract default value if provided
            def_match = re.search(r'AddTextParameter\s*\(.*?,.*?GH_ParamAccess\.(?:item|list)\s*,\s*"([^"]*)"\s*\)', line)
            def_val = def_match.group(1) if def_match else "1"
            # TerrainGeneratorPro is handled manually (it has PatternSizeXY/Z which are AddNumberParameter)
            # So this is safe for Facade Modules
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, {param_idx}, "{def_val}", ix, {y_offset}, 80, 25);')
            y_offset += 30
            param_idx += 1
            continue

        # If it's ElevationLabel, it has empty text panels etc. We did that manually. Let's skip it from auto-overwrite.
        if "ElevationLabel.cs" in filepath or "TerrainGeneratorPro.cs" in filepath or "WaterFlow.cs" in filepath or "GlobalFloodEngine.cs" in filepath or "RoadSlopeAnalyzer.cs" in filepath or "FilletRules.cs" in filepath or "InitKeys.cs" in filepath:
            # Revert the removal if it's a file we manually perfected
            pass # We'll just not process them in the main loop below

        num_match = re.search(r'AddNumberParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*([-0-9\.]+)\s*\)', line)
        if num_match:
            val = num_match.group(1)
            v_float = float(val)
            max_val = max(10.0, v_float * 2) if v_float > 0 else 10.0
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, {param_idx}, 0.0, {max_val}, {val}, ix, {y_offset});')
            y_offset += 30
            
        bool_match = re.search(r'AddBooleanParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*(true|false)\s*\)', line, re.IGNORECASE)
        if bool_match:
            val = bool_match.group(1).lower()
            if re.search(r'(?i)run|execute|trigger|init|update', name):
                auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireButton(this, document, {param_idx}, ix, {y_offset});')
            else:
                auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, {param_idx}, {val}, ix, {y_offset});')
            y_offset += 30
            
        int_match = re.search(r'AddIntegerParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*([-0-9]+)\s*\)', line)
        if int_match and not int_options:
            val = int_match.group(1)
            v_int = int(val)
            max_val = max(10, v_int * 2) if v_int > 0 else 10
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, {param_idx}, 0.0, {max_val}, {val}, ix, {y_offset});')
            y_offset += 30

        col_match = re.search(r'AddColourParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*Color\.([A-Za-z]+)\s*\)', line)
        if col_match:
            col_name = col_match.group(1)
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, {param_idx}, System.Drawing.Color.{col_name}, ix, {y_offset});')
            y_offset += 30

        param_idx += 1

    # Outputs
    out_match = re.search(r'protected override void RegisterOutputParams[^{]*\{([^}]+)\}', content)
    if out_match:
        out_block = out_match.group(1)
        out_idx = 0
        out_y_offset = -100
        for line in out_block.split(';'):
            line = line.strip()
            if not line.startswith('pManager.Add'): continue
            
            if "AddMeshParameter" in line or "AddBrepParameter" in line or "AddSurfaceParameter" in line:
                auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, {out_idx}, System.Drawing.Color.FromArgb(230, 230, 230), ox, {out_y_offset});')
            elif "AddCurveParameter" in line:
                auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, {out_idx}, "curve", ox, {out_y_offset});')
            elif "AddPointParameter" in line:
                auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, {out_idx}, "point", ox, {out_y_offset});')
            elif "AddLineParameter" in line:
                auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, {out_idx}, "line", ox, {out_y_offset});')
            
            out_y_offset += 40
            out_idx += 1

    if not auto_wire_lines:
        return False

    added_to_doc = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ix = 220, ox = 250;
""" + "\n".join(auto_wire_lines) + """
            }
        }
"""
    content = re.sub(r'(protected override void RegisterInputParams)', added_to_doc + r'\n        \1', content)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
        
    return True

MANUAL_FILES = ["ElevationLabel.cs", "TerrainGeneratorPro.cs", "WaterFlow.cs", "GlobalFloodEngine.cs", "RoadSlopeAnalyzer.cs", "FilletRules.cs", "InitKeys.cs"]

processed = 0
for filename in os.listdir(COMPONENTS_DIR):
    if filename.endswith(".cs") and filename not in MANUAL_FILES:
        if process_file(os.path.join(COMPONENTS_DIR, filename)):
            processed += 1

print(f"Done generating V3 auto-wiring. Processed {processed} files.")
