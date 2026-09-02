import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

# 1. Add Rotation Parameter to RegisterInputParams
param_insert = """            pManager.AddTextParameter("Bake Name", "BN", "Bake group/layer name", GH_ParamAccess.item, "");
            pManager.AddIntegerParameter("Rotate 90", "R90", "Rotate image by multiples of 90 degrees (1=90, 2=180, 3=270)", GH_ParamAccess.item, 0);"""

content = content.replace('pManager.AddTextParameter("Bake Name", "BN", "Bake group/layer name", GH_ParamAccess.item, "");', param_insert)

optional_insert = """            pManager[10].Optional = true;
            pManager[11].Optional = true;"""
content = content.replace('pManager[10].Optional = true;', optional_insert)

# 2. Add _cachedRotation field
field_insert = """        private Bitmap _cachedBitmap = null;
        private string _cachedImagePath = "";
        private int _cachedRotation = 0;"""
content = content.replace('        private Bitmap _cachedBitmap = null;\n        private string _cachedImagePath = "";', field_insert)

# 3. Modify SolveInstance image loading
old_logic = """            string imgPath = "";
            DA.GetData(0, ref imgPath);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new Bitmap(imgPath);
                        _cachedImagePath = imgPath;
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load image: " + ex.Message);
                        return;
                    }
                }
            }"""

new_logic = """            string imgPath = "";
            DA.GetData(0, ref imgPath);
            
            int rotSteps = 0;
            DA.GetData(11, ref rotSteps);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || rotSteps != _cachedRotation || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new Bitmap(imgPath);
                        _cachedImagePath = imgPath;
                        _cachedRotation = rotSteps;
                        
                        int r = ((rotSteps % 4) + 4) % 4; 
                        if (r == 1) _cachedBitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        if (r == 2) _cachedBitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        if (r == 3) _cachedBitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load image: " + ex.Message);
                        return;
                    }
                }
            }"""

content = content.replace(old_logic, new_logic)

with open('Components/PixelatedSurface.cs', 'w') as f:
    f.write(content)
