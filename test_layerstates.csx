using System;
using System.Linq;
using System.Reflection;
using Rhino;

var docType = typeof(RhinoDoc);
var nlsProp = docType.GetProperty("NamedLayerStates");
if (nlsProp != null) {
    Console.WriteLine("NamedLayerStates exists.");
    var nlsType = nlsProp.PropertyType;
    foreach(var m in nlsType.GetMethods()) {
        Console.WriteLine(m.Name);
    }
} else {
    Console.WriteLine("NamedLayerStates DOES NOT EXIST.");
}
