import random
from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path

# 1. Component Metadata
ghenv.Component.Name = "Tree Shuffler"
ghenv.Component.NickName = "T-Shuffle"
ghenv.Component.Description = "Shuffles items from a pool into an existing tree structure"

# 2. Check if inputs are connected to avoid 'NoneType' errors
if tree is not None and choices is not None:
    
    # Handle the seed (default to 42 if not provided)
    s = seed if 'seed' in globals() and seed is not None else 42
    rnd = random.Random(s)

    # 3. Prepare the pool
    # We ensure the pool is a 'flat' list to avoid the nested output you saw
    if hasattr(choices, "__iter__") and not isinstance(choices, (str, bytes)):
        pool = list(choices)
    else:
        pool = [choices]

    rnd.shuffle(pool)

    result = DataTree[object]()
    i = 0
    total_items = 0

    # 4. Process the Tree
    for b in range(tree.BranchCount):
        path = tree.Paths[b]
        branch = tree.Branches[b]

        for _ in branch:
            # Pick one item. The modulo (%) ensures that if tree (6) > pool (5), 
            # the 6th item grabs the 1st item of the pool again.
            pick = pool[i % len(pool)]
            result.Add(pick, path)
            i += 1
            total_items += 1

    # 5. Set the Component Message (visible on the canvas)
    ghenv.Component.Message = f"Branches: {tree.BranchCount}\nItems: {total_items}"
    
    a = result

else:
    # Clear message if no data is connected
    ghenv.Component.Message = "Waiting for data..."
    a = None