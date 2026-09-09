using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;

namespace Enzyme
{
    public static class IconLoader
    {
        private static Dictionary<string, Bitmap> _iconCache = new Dictionary<string, Bitmap>();

        public static Bitmap Load(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;

            if (_iconCache.TryGetValue(iconName, out Bitmap cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"Enzyme.Resources.{iconName}";

                Trace.WriteLine($"Using resource name: {resourceName}");

                // Do not dispose the stream! GDI+ Bitmaps require the underlying stream to remain open
                // for the lifetime of the Bitmap, otherwise drawing them will fail.
                var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Trace.WriteLine($"Resource stream is null for: {resourceName}");
                    return null;
                }

                var bmp = new Bitmap(stream);
                _iconCache[iconName] = bmp;
                return bmp;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error loading icon: {ex.Message}");
                return null;
            }
        }
    }
}
