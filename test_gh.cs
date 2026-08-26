using System;
using System.Reflection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;

public class Test
{
    public static void Main()
    {
        Console.WriteLine(typeof(GH_CustomPreviewComponent).FullName);
    }
}
