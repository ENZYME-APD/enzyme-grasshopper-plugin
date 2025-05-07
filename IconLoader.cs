using System;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;

namespace Enzyme
{
    public static class IconLoader
    {
        public static Bitmap Load(string iconName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"Enzyme.Resources.{iconName}";

                // Debug: Lists all resources
                //string[] resources = assembly.GetManifestResourceNames();
                //Trace.WriteLine($"Available resources: {string.Join(", ", resources)}");

                Trace.WriteLine($"Using resource name: {resourceName}");

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Trace.WriteLine($"Resource stream is null for: {resourceName}");
                        return null;
                    }

                    return new Bitmap(stream);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error loading icon: {ex.Message}");
                return null;
            }
        }
    }
}
