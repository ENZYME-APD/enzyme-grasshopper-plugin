using System;
using System.Reflection;

class Program
{
    static void Main()
    {
        Assembly asm = Assembly.LoadFile(System.IO.Path.GetFullPath("bin/Debug/net48/enzyme.gha"));
        foreach(var name in asm.GetManifestResourceNames())
        {
            Console.WriteLine(name);
        }
    }
}
