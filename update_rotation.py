import re

with open("Components/PixelatedSurface.cs", "r") as f:
    content = f.read()

# 1. Update Input Params
old_param = 'pManager.AddIntegerParameter("Rotate 90", "R90", "Rotate image by multiples of 90 degrees (1=90, 2=180, 3=270)", GH_ParamAccess.item, 0);'
new_param = 'pManager.AddNumberParameter("Rotation", "Rot", "Rotate image map in degrees", GH_ParamAccess.item, 0.0);'
content = content.replace(old_param, new_param)

# 2. Update Image Loading / Cache (Remove _cachedRotation)
old_cache_logic = """            int rotSteps = 0;
            DA.GetData(10, ref rotSteps);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || rotSteps != _cachedRotation || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new System.Drawing.Bitmap(imgPath);
                        _cachedImagePath = imgPath;
                        _cachedRotation = rotSteps;
                        
                        int r = ((rotSteps % 4) + 4) % 4; 
                        if (r == 1) _cachedBitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);
                        if (r == 2) _cachedBitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate180FlipNone);
                        if (r == 3) _cachedBitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipNone);
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load image: " + ex.Message);
                        return;
                    }
                }
            }"""
new_cache_logic = """            double rotDeg = 0.0;
            DA.GetData(10, ref rotDeg);

            if (!string.IsNullOrEmpty(imgPath))
            {
                if (imgPath != _cachedImagePath || _cachedBitmap == null)
                {
                    try
                    {
                        _cachedBitmap = new System.Drawing.Bitmap(imgPath);
                        _cachedImagePath = imgPath;
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load image: " + ex.Message);
                        return;
                    }
                }
            }"""
content = content.replace(old_cache_logic, new_cache_logic)

# 3. Update UV Mapping Logic inside the loop
old_uv_logic = """                int pxX = (int)Math.Max(0, Math.Min(_cachedBitmap.Width - 1, img_u * _cachedBitmap.Width));
                int pxY = (int)Math.Max(0, Math.Min(_cachedBitmap.Height - 1, (1.0 - img_v) * _cachedBitmap.Height));"""
new_uv_logic = """                if (rotDeg != 0.0)
                {
                    double rad = rotDeg * Math.PI / 180.0;
                    double cosA = Math.Cos(rad);
                    double sinA = Math.Sin(rad);

                    double cu = img_u - 0.5;
                    double cv = img_v - 0.5;

                    double ru = cu * cosA - cv * sinA;
                    double rv = cu * sinA + cv * cosA;

                    img_u = ru + 0.5;
                    img_v = rv + 0.5;
                }

                int pxX = (int)Math.Max(0, Math.Min(_cachedBitmap.Width - 1, img_u * _cachedBitmap.Width));
                int pxY = (int)Math.Max(0, Math.Min(_cachedBitmap.Height - 1, (1.0 - img_v) * _cachedBitmap.Height));"""
content = content.replace(old_uv_logic, new_uv_logic)

with open("Components/PixelatedSurface.cs", "w") as f:
    f.write(content)
