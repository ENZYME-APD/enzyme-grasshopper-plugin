import ghpythonlib.treehelpers as th

# 1. Metadata and UI setup
ghenv.Component.Name = "Area Converter"
ghenv.Component.NickName = "AreaConv"
ghenv.Component.Description = "Converts between Square Meters and Square Feet while maintaining Data Trees."

# 2. Update UI Message based on Toggle
if Conv_Type:
    ghenv.Component.Message = "SQM > SQFT"
else:
    ghenv.Component.Message = "SQFT > SQM"

# 3. Guard Clause
if Area is not None and Area.DataCount > 0:
    FACTOR = 10.7639104

    def process_conversion(val, to_feet):
        # Explicitly handle None (Grasshopper Nulls)
        if val is None: 
            return None
        try:
            # Conversion logic
            return float(val) * FACTOR if to_feet else float(val) / FACTOR
        except (ValueError, TypeError):
            # Return original value if it's not a number (e.g., a string)
            return val

    def walk_tree(data, mode):
        # Check if the current item is a list (representing a branch)
        if isinstance(data, list):
            return [walk_tree(item, mode) for item in data]
        else:
            return process_conversion(data, mode)

    # Convert Tree to Nested Lists
    # retrieve_base=True ensures we get the full structure
    data_list = th.tree_to_list(Area)
    
    # Process the nested list structure
    results = walk_tree(data_list, Conv_Type)
    
    # Pack back into a Grasshopper Tree
    a = th.list_to_tree(results)

else:
    a = None
    if Area is not None and Area.DataCount == 0:
        ghenv.Component.Message = "Empty Tree"