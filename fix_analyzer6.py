with open("MeshHeightAnalysis.cs.backup", "r") as f:
    orig = f.read()

start_str = "                    if (secCountX > 0 && (bMaxY - bMinY) > 1e-5)"
end_str = "            string instructions = \"Analyzes mesh extremes, unrolls sections bi-directionally, and generates 3D/2D metadata labels.\";"
idx1 = orig.find(start_str)
idx2 = orig.find(end_str)

if idx1 != -1 and idx2 != -1:
    block = orig[idx1:idx2]
    print(block[-100:]) # just to see the end of it
