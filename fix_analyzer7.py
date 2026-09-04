import re
with open("Components/MeshHeightAnalysis.cs", "r") as f:
    orig = f.read()

# I will just insert the braces before `string instructions = "Analyzes mesh extremes and generates topo heatmaps.";`
orig = orig.replace(
'''            string instructions = "Analyzes mesh extremes and generates topo heatmaps.";''',
'''                }
            }

            string instructions = "Analyzes mesh extremes and generates topo heatmaps.";'''
)

with open("Components/MeshHeightAnalysis.cs", "w") as f:
    f.write(orig)
