"""
TABLE TO DATATREE PARSER
================================================================================
Converts a pasted Excel table or CSV from a Panel into a clean DataTree.
Automatically casts numbers and creates isolated branches for rows or columns.

INPUTS:
    TableData (str)  [Item Access] : Multiline text panel (Paste from Excel).
    ByColumn  (bool) [Item Access] : True = Branches are Columns. False = Rows.

OUTPUTS:
    DataTree : The structured Grasshopper DataTree.
================================================================================
"""

import Grasshopper as gh
from Grasshopper.Kernel.Data import GH_Path
import System

ghenv.Component.Name = "Table to DataTree"
ghenv.Component.NickName = "ParseTable"
ghenv.Component.Description = "Converts multiline tabular text into a Grasshopper DataTree."

def smart_cast(val):
    """Attempts to convert text into floats or integers for downstream math."""
    val = val.strip()
    try:
        # Try to cast to float
        f = float(val)
        # If it's a perfect integer (e.g., 4.0), return as int
        if f.is_integer(): return int(f)
        return f
    except ValueError:
        # If it's just text, return the string
        return val

def parse_table():
    text = globals().get('TableData', None)
    by_column = globals().get('ByColumn', True)
    
    tree = gh.DataTree[System.Object]()
    if not text:
        return tree, "Awaiting Table Data"
        
    # Auto-detect Excel paste (Tabs) vs manual CSV (Commas)
    delimiter = "\t" if "\t" in text else ","
    
    # Split the raw text into rows
    lines = [line for line in text.split('\n') if line.strip()]
    grid = [[smart_cast(item) for item in line.split(delimiter)] for line in lines]
    
    if not grid:
        return tree, "No valid data found."

    if by_column:
        # Output Columns as Branches
        col_count = max(len(row) for row in grid)
        for c in range(col_count):
            path = GH_Path(c)
            for r in range(len(grid)):
                if c < len(grid[r]):
                    tree.Add(grid[r][c], path)
        msg = "Output: {} Columns (Branches)".format(col_count)
    else:
        # Output Rows as Branches
        for r in range(len(grid)):
            path = GH_Path(r)
            for c in range(len(grid[r])):
                tree.Add(grid[r][c], path)
        msg = "Output: {} Rows (Branches)".format(len(grid))
        
    return tree, msg

# Execute
out_tree, ui_msg = parse_table()

# Outputs
DataTree = out_tree
ghenv.Component.Message = ui_msg