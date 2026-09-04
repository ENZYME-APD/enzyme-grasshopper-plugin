import re

with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# 1. Update InputParams
old_in = '''            pManager.AddTextParameter("Prefix", "P", "Prefix for the output filenames (optional).", GH_ParamAccess.item, "");
            
            pManager.AddIntegerParameter("Width", "W", "Image width in pixels.", GH_ParamAccess.item, 1920);'''

new_in = '''            pManager.AddTextParameter("Prefix", "P", "Prefix for the output filenames (optional).", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Suffix", "Suf", "Suffix for the output filenames (optional).", GH_ParamAccess.item, "");
            pManager.AddTextParameter("Format", "Fmt", "Image format: png, jpg, bmp, tiff.", GH_ParamAccess.item, "png");
            
            pManager.AddIntegerParameter("Width", "W", "Image width in pixels.", GH_ParamAccess.item, 1920);'''
ts = ts.replace(old_in, new_in)

old_opt = '''            pManager[1].Optional = true; // Views can be empty
            pManager[12].Optional = true;
            pManager[13].Optional = true;'''

new_opt = '''            pManager[1].Optional = true; // Views can be empty
            pManager[14].Optional = true; // Display Style
            pManager[15].Optional = true; // Layer State'''
ts = ts.replace(old_opt, new_opt)

# 2. Extract Data
old_get = '''            string prefix = "";
            DA.GetData("Prefix", ref prefix);

            int width = 1920;'''

new_get = '''            string prefix = "";
            DA.GetData("Prefix", ref prefix);

            string suffix = "";
            DA.GetData("Suffix", ref suffix);

            string formatStr = "png";
            DA.GetData("Format", ref formatStr);

            int width = 1920;'''
ts = ts.replace(old_get, new_get)

# 3. Handle filename and format
old_save = '''                            if (bitmap != null)
                            {
                                string safeName = string.Join("_", nv.Name.Split(Path.GetInvalidFileNameChars()));
                                string filename = string.IsNullOrEmpty(prefix) ? $"{safeName}.png" : $"{prefix}_{safeName}.png";
                                string path = Path.Combine(directory, filename);
                                
                                bitmap.SetResolution(dpi, dpi);
                                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                                savedFiles.Add(path);
                                bitmap.Dispose();
                            }'''

new_save = '''                            if (bitmap != null)
                            {
                                string safeName = string.Join("_", nv.Name.Split(Path.GetInvalidFileNameChars()));
                                
                                string f = formatStr.ToLower().Trim();
                                System.Drawing.Imaging.ImageFormat imgFormat = System.Drawing.Imaging.ImageFormat.Png;
                                string ext = "png";
                                
                                if (f == "jpg" || f == "jpeg") { imgFormat = System.Drawing.Imaging.ImageFormat.Jpeg; ext = "jpg"; }
                                else if (f == "bmp") { imgFormat = System.Drawing.Imaging.ImageFormat.Bmp; ext = "bmp"; }
                                else if (f == "tif" || f == "tiff") { imgFormat = System.Drawing.Imaging.ImageFormat.Tiff; ext = "tif"; }
                                
                                string pre = string.IsNullOrEmpty(prefix) ? "" : prefix + "_";
                                string suf = string.IsNullOrEmpty(suffix) ? "" : "_" + suffix;
                                
                                string filename = $"{pre}{safeName}{suf}.{ext}";
                                string path = Path.Combine(directory, filename);
                                
                                bitmap.SetResolution(dpi, dpi);
                                bitmap.Save(path, imgFormat);
                                savedFiles.Add(path);
                                bitmap.Dispose();
                            }'''
ts = ts.replace(old_save, new_save)

# 4. Fix Value List Indices (12 -> 14, 13 -> 15)
old_vl_12 = '''this.Params.Input[12].AddSource(vl);'''
new_vl_14 = '''this.Params.Input[14].AddSource(vl);'''
ts = ts.replace(old_vl_12, new_vl_14)

old_vl_13 = '''this.Params.Input[13].AddSource(vl);'''
new_vl_15 = '''this.Params.Input[15].AddSource(vl);'''
ts = ts.replace(old_vl_13, new_vl_15)

with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
