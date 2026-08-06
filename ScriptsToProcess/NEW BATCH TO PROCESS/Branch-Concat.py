from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path

# Defaults if inputs are None
if sep is None:
    sep = ""
if prefix is None:
    prefix = ""
if suffix is None:
    suffix = ""

result = DataTree[str]()

# Iterate through branches
for i in range(tree.BranchCount):
    path = tree.Paths[i]
    branch = tree.Branches[i]

    # Convert all items to string
    str_items = [str(item) for item in branch]

    # Concatenate
    joined = prefix + sep.join(str_items) + suffix

    # Add to output tree (one item per branch)
    result.Add(joined, path)

a = result