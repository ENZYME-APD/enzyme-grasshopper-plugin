"""
RECIPE JSON BUILDER
================================================================================
Parametrically compiles lists of programs, heights, and floor counts into the 
standardized JSON array required by the Stage 1 Adapters.

INPUTS (All set to List Access):
    Program     (Str)   - e.g., "Retail", "Office", "Hotel"
    FloorHeight (Float) - e.g., 4.5, 4.0, 3.3
    NumFloors   (Int)   - e.g., 3, 10, 5

OUTPUTS:
    RecipeJSON  (Str)   - The formatted JSON payload.
================================================================================
"""
import json

ghenv.Component.Name = "Recipe JSON Builder"
ghenv.Component.NickName = "RecipeBuilder"

recipe = []

# Ensure all inputs have data before proceeding
if Program and FloorHeight and NumFloors:
    
    # Use the length of the shortest list to prevent index out-of-bounds errors
    limit = min(len(Program), len(FloorHeight), len(NumFloors))
    
    for i in range(limit):
        recipe.append({
            "program": str(Program[i]).strip(),
            "height": float(FloorHeight[i]),
            "floors": int(NumFloors[i])
        })

# Serialize into a clean JSON string
RecipeJSON = json.dumps(recipe, indent=2)

# Update UI with helpful statistics
if recipe:
    total_floors = sum([item["floors"] for item in recipe])
    total_height = sum([item["floors"] * item["height"] for item in recipe])
    
    ghenv.Component.Message = "RECIPE\n---\nBlocks: {}\nFloors: {}\nHeight: {:.1f}m".format(
        len(recipe), total_floors, total_height)
else:
    ghenv.Component.Message = "Awaiting Data"