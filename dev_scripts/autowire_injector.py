import os
import re

TARGET_CATEGORIES = ["Masterplan", "Terrain"]
COMPONENTS_DIR = "Components"

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Skip if already has AddedToDocument
    if "public override void AddedToDocument" in content:
        return False

    # Check if category is Masterplan or Terrain
    # Look for: base("Name", "Nick", "Desc", "Enzyme", "Masterplan")
    match = re.search(r'base\s*\(\s*".*?"\s*,\s*".*?"\s*,\s*".*?"\s*,\s*".*?"\s*,\s*"([^"]+)"\s*\)', content)
    if not match:
        return False
    
    category = match.group(1)
    if category not in TARGET_CATEGORIES:
        return False

    print(f"Processing: {filepath}")

    # Find RegisterInputParams
    input_match = re.search(r'protected override void RegisterInputParams[^{]*\{([^}]+)\}', content)
    if not input_match:
        return False
    
    inputs_block = input_match.group(1)
    
    # Extract params
    param_idx = 0
    auto_wire_lines = []
    y_offset = -120
    
    for line in inputs_block.split(';'):
        line = line.strip()
        if not line.startswith('pManager.Add'):
            if 'pManager[' in line and '.Optional' in line:
                pass # it's just setting optional, doesn't increment index
            continue
            
        # Parse based on type
        # AddNumberParameter(name, nick, desc, access, default)
        num_match = re.search(r'AddNumberParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*([-0-9\.]+)\s*\)', line)
        if num_match:
            val = num_match.group(1)
            # determine domain based on val
            v_float = float(val)
            max_val = max(10.0, v_float * 2) if v_float > 0 else 10.0
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, {param_idx}, 0.0, {max_val}, {val}, ix, {y_offset});')
            y_offset += 30
            
        bool_match = re.search(r'AddBooleanParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*(true|false)\s*\)', line, re.IGNORECASE)
        if bool_match:
            val = bool_match.group(1).lower()
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, {param_idx}, {val}, ix, {y_offset});')
            y_offset += 30
            
        int_match = re.search(r'AddIntegerParameter\s*\(.*?GH_ParamAccess\.item\s*,\s*([-0-9]+)\s*\)', line)
        if int_match:
            val = int_match.group(1)
            v_int = int(val)
            max_val = max(10, v_int * 2) if v_int > 0 else 10
            # For integers, we use slider but accuracy is handled in AutoWireHelper (it's float currently, but acceptable)
            auto_wire_lines.append(f'                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, {param_idx}, 0.0, {max_val}, {val}, ix, {y_offset});')
            y_offset += 30
            
        param_idx += 1

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
                int ix = 200, ox = 250;
""" + "\n".join(auto_wire_lines) + """
            }
        }
"""
    # Inject before RegisterInputParams
    content = re.sub(r'(protected override void RegisterInputParams)', added_to_doc + r'\n        \1', content)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
        
    return True

processed = 0
for filename in os.listdir(COMPONENTS_DIR):
    if filename.endswith(".cs"):
        if process_file(os.path.join(COMPONENTS_DIR, filename)):
            processed += 1

print(f"Done. Processed {processed} files.")
