using System.Reflection;
var asm = Assembly.LoadFile(System.IO.Path.GetFullPath("bin/Debug/net48/enzyme.gha"));
foreach (var res in asm.GetManifestResourceNames()) {
    System.Console.WriteLine(res);
}
