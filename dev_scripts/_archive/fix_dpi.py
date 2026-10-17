with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

orig = '            DA.GetData("DPI", ref dpi);'
new_code = '''            int dpiIdx = this.Params.IndexOfInputParam("DPI");
            if (dpiIdx != -1) DA.GetData(dpiIdx, ref dpi);'''

content = content.replace(orig, new_code)

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
