import sys

code = """
using System;
using System.Reflection;
using Rhino.Display;

public class Test {
    public static void Main() {
        var props = typeof(ViewCapture).GetProperties();
        foreach (var p in props) {
            Console.WriteLine(p.Name + " - " + p.PropertyType.Name);
        }
        Console.WriteLine("--- Settings ---");
        var props2 = typeof(ViewCaptureSettings).GetProperties();
        foreach (var p in props2) {
            Console.WriteLine(p.Name + " - " + p.PropertyType.Name);
        }
    }
}
"""
with open('test_capture.cs', 'w') as f:
    f.write(code)
