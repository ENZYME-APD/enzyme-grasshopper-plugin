import os
import re
import math

COMPONENTS_DIR = "Components"

def get_slider_max(val):
    try:
        v = float(val)
        if v <= 1.0: return 2.0
        if v <= 1.5: return 3.0
        if v <= 2.5: return 5.0
        return max(10.0, math.ceil(v * 2))
    except:
        return 10.0

def parse_outputs(content):
    out_match = re.search(r'protected override void RegisterOutputParams[^{]*\{([^}]+)\}', content)
    if not out_match: return []
    out_block = out_match.group(1)
    
    outputs = []
    for line in out_block.split(';'):
        line = line.strip()
        if not line.startswith('pManager.Add'): continue
        
        name_match = re.search(r'pManager\.Add[A-Za-z]+Parameter\s*\(\s*"([^"]+)"', line)
        name = name_match.group(1) if name_match else ""
        
        if "AddMeshParameter" in line or "AddBrepParameter" in line or "AddSurfaceParameter" in line:
            outputs.append(("preview", name))
        elif "AddCurveParameter" in line:
            outputs.append(("curve", name))
        elif "AddPointParameter" in line:
            outputs.append(("point", name))
        elif "AddLineParameter" in line:
            outputs.append(("line", name))
        elif "AddTextParameter" in line:
            outputs.append(("panel", name))
        else:
            outputs.append(("unknown", name))
    return outputs

def parse_inputs(content):
    input_match = re.search(r'protected override void RegisterInputParams[^{]*\{([^}]+)\}', content)
    if not input_match: return []
    inputs_block = input_match.group(1)
    
    inputs = []
    for line in inputs_block.split(';'):
        line = line.strip()
        if not line.startswith('pManager.Add'):
            continue
            
        desc_match = re.search(r'pManager\.Add[A-Za-z]+Parameter\s*\(\s*".*?"\s*,\s*".*?"\s*,\s*"(.*?)"', line)
        desc = desc_match.group(1) if desc_match else ""
        name_match = re.search(r'pManager\.Add[A-Za-z]+Parameter\s*\(\s*"([^"]+)"', line)
        name = name_match.group(1) if name_match else ""

        str_options = re.findall(r"'([^']+)'", desc)
        int_options = re.findall(r"(\d+)\s*=\s*([^,]+?)(?:,|$)", desc)
        
        if str_options and "AddTextParameter" in line:
            keys = ", ".join(f'"{opt}"' for opt in str_options)
            vals = ", ".join(f'"\\"{opt}\\""' for opt in str_options) 
            inputs.append(("valuelist", name, keys, vals))
            continue
            
        if int_options and "AddIntegerParameter" in line:
            keys = ", ".join(f'"{opt[1].strip()}"' for opt in int_options)
            vals = ", ".join(f'"{opt[0]}"' for opt in int_options)
            inputs.append(("valuelist", name, keys, vals))
            continue

        if "AddTextParameter" in line and ("Pattern" in name or "LabelText" in name):
            def_match = re.search(r'AddTextParameter\s*\(.*?,.*?GH_ParamAccess\.(?:item|list)\s*,\s*"([^"]*)"\s*\)', line)
            def_val = def_match.group(1) if def_match else "1"
            inputs.append(("panel", name, def_val))
            continue

        # Check for number parameters (with or without default value)
        if "AddNumberParameter" in line:
            # check if it's item access
            if "GH_ParamAccess.item" in line:
                num_match = re.search(r'AddNumberParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*([-0-9\.]+)\s*\)', line)
                val = num_match.group(1) if num_match else "1.5" # Default to 1.5 if no default is provided
                inputs.append(("slider_num", name, val))
                continue
            
        if "AddBooleanParameter" in line:
            if "GH_ParamAccess.item" in line:
                bool_match = re.search(r'AddBooleanParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*(true|false)\s*\)', line, re.IGNORECASE)
                val = bool_match.group(1).lower() if bool_match else "false"
                if re.search(r'(?i)run|execute|trigger|init|update', name):
                    inputs.append(("button", name))
                else:
                    inputs.append(("toggle", name, val))
                continue
            
        if "AddIntegerParameter" in line:
            if "GH_ParamAccess.item" in line:
                int_match = re.search(r'AddIntegerParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*([-0-9]+)\s*\)', line)
                val = int_match.group(1) if int_match else "1"
                inputs.append(("slider_int", name, val))
                continue

        col_match = re.search(r'AddColourParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*Color\.([A-Za-z]+)\s*\)', line)
        if col_match:
            inputs.append(("color", name, col_match.group(1)))
            continue

        inputs.append(("unknown", name))

    return inputs

def get_preview_color(name):
    name_low = name.lower()
    if 'glass' in name_low: return "System.Drawing.Color.FromArgb(150, 200, 255)"
    if 'solid' in name_low or 'panel' in name_low: return "System.Drawing.Color.FromArgb(250, 250, 250)"
    if 'rail' in name_low: return "System.Drawing.Color.FromArgb(150, 150, 150)"
    if 'header' in name_low: return "System.Drawing.Color.FromArgb(255, 165, 0)"
    if 'canopy' in name_low: return "System.Drawing.Color.FromArgb(200, 200, 200)"
    if 'mullion' in name_low: return "System.Drawing.Color.FromArgb(50, 50, 50)"
    if 'slab' in name_low: return "System.Drawing.Color.FromArgb(180, 180, 180)"
    if 'roof' in name_low: return "System.Drawing.Color.FromArgb(200, 100, 100)"
    return "System.Drawing.Color.FromArgb(230, 230, 230)"

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    if "PluginInfo.cs" in filepath or "AutoWireHelper.cs" in filepath:
        return False
        
    inputs = parse_inputs(content)
    outputs = parse_outputs(content)
    
    if not inputs and not outputs:
        return False
        
    content = re.sub(r'\s*public override void AddedToDocument.*?protected override void RegisterInputParams', 
                     '\n\n        protected override void RegisterInputParams', 
                     content, flags=re.DOTALL)

    auto_wire_lines = []
    
    valid_inputs = [inp for inp in inputs if inp[0] != "unknown"]
    total_in_h = len(valid_inputs) * 30
    in_y_start = -(total_in_h - 30) // 2 if valid_inputs else 0
    
    y_offset = in_y_start
    for i, inp in enumerate(inputs):
        t = inp[0]
        if t == "unknown": continue
        
        if t == "slider_num":
            max_val = get_slider_max(inp[2])
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, {i}, 0.0, {max_val}, {inp[2]}, 160, {y_offset});')
        elif t == "slider_int":
            max_val = get_slider_max(inp[2])
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, {i}, 0.0, {max_val}, {inp[2]}, 160, {y_offset});')
        elif t == "toggle":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, {i}, {inp[2]}, 80, {y_offset});')
        elif t == "button":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireButton(this, document, {i}, 60, {y_offset});')
        elif t == "valuelist":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireValueList(this, document, {i}, new string[]{{{inp[2]}}}, new string[]{{{inp[3]}}}, 150, {y_offset});')
        elif t == "panel":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, {i}, "{inp[2]}", 120, {y_offset}, 80, 25);')
        elif t == "color":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, {i}, System.Drawing.Color.{inp[2]}, 120, {y_offset});')
            
        y_offset += 30

    out_total_h = 0
    valid_outputs = [out for out in outputs if out[0] != "unknown"]
    for out in valid_outputs:
        out_total_h += 60 if out[0] == "preview" else 30
        
    out_y_offset = -(out_total_h - 30) // 2 if valid_outputs else 0
    
    for i, out in enumerate(outputs):
        t = out[0]
        name = out[1]
        if t == "unknown": continue
        
        if t == "preview":
            color = get_preview_color(name)
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, {i}, {color}, 150, {out_y_offset});')
            out_y_offset += 60
        elif t == "curve":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, {i}, "curve", 150, {out_y_offset});')
            out_y_offset += 30
        elif t == "point":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, {i}, "point", 150, {out_y_offset});')
            out_y_offset += 30
        elif t == "line":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, {i}, "line", 150, {out_y_offset});')
            out_y_offset += 30
        elif t == "panel":
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireOutputPanel(this, document, {i}, 70, {out_y_offset - 11}, 160, 22);')
            out_y_offset += 30

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
""" + "\n".join(auto_wire_lines) + """
            }
        }
"""
    content = re.sub(r'(protected override void RegisterInputParams)', added_to_doc + r'\n        \1', content)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    return True

processed = 0
for filename in os.listdir(COMPONENTS_DIR):
    if filename.endswith(".cs"):
        if process_file(os.path.join(COMPONENTS_DIR, filename)):
            processed += 1

print(f"Processed {processed} files for auto-wiring improvements.")
