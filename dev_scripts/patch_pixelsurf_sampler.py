import re

with open('Components/PixelatedSurface.cs', 'r') as f:
    content = f.read()

# Replace the input parameter
content = content.replace(
    'pManager.AddTextParameter("Image Path", "Img", "Absolute path to the image file", GH_ParamAccess.item);',
    'pManager.AddGenericParameter("Image Sampler", "IS", "Connect a Grasshopper Image Sampler", GH_ParamAccess.item);'
)

# Replace the autowiring
content = content.replace(
    'Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "C:\\\\\\\\path\\\\\\\\to\\\\\\\\image.jpg", 300, -180, 150, 40);',
    'Enzyme.Utils.AutoWireHelper.WireImageSampler(this, document, 0, 200, -180);'
)

# Replace the image loading logic in SolveInstance
old_img_logic = """            string imgPath = "";
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
            }

            if (_cachedBitmap == null)
            {
                Message = "No Image";
                return;
            }"""

new_img_logic = """            if (Params.Input[0].SourceCount > 0)
            {
                var source = Params.Input[0].Sources[0];
                if (source != null && source.GetType().Name.Contains("ImageSampler"))
                {
                    System.Drawing.Bitmap foundImg = null;
                    foreach (var prop in source.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    {
                        if (prop.PropertyType == typeof(System.Drawing.Bitmap) || prop.PropertyType == typeof(System.Drawing.Image))
                        {
                            var val = prop.GetValue(source);
                            if (val != null) { foundImg = (System.Drawing.Bitmap)val; break; }
                        }
                    }
                    if (foundImg == null)
                    {
                        foreach (var field in source.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                        {
                            if (field.FieldType == typeof(System.Drawing.Bitmap) || field.FieldType == typeof(System.Drawing.Image))
                            {
                                var val = field.GetValue(source);
                                if (val != null) { foundImg = (System.Drawing.Bitmap)val; break; }
                            }
                        }
                    }
                    if (foundImg != null)
                    {
                        _cachedBitmap = foundImg;
                    }
                }
            }

            if (_cachedBitmap == null)
            {
                Message = "No Image Sampler attached, or Image not loaded.";
                return;
            }"""

content = content.replace(old_img_logic, new_img_logic)

with open('Components/PixelatedSurface.cs', 'w') as f:
    f.write(content)
