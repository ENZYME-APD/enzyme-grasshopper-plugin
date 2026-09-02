using System;
using System.IO;
using System.Reflection;
using Grasshopper.Kernel.Special;

namespace Enzyme.Components {
    public class DumpSampler {
        public static void Dump() {
            var t = typeof(GH_ImageSampler);
            var lines = new System.Collections.Generic.List<string>();
            foreach(var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) lines.Add("P: " + p.Name);
            foreach(var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) lines.Add("F: " + f.Name);
            foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance)) lines.Add("M: " + m.Name);
            File.WriteAllLines("sampler_dump.txt", lines);
        }
    }
}
