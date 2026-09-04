using Rhino;
using Rhino.DocObjects;
namespace Enzyme.Components {
    public class Test {
        public void M() {
            var doc = RhinoDoc.ActiveDoc;
            int count = doc.NamedLayerStates.Count;
            var names = doc.NamedLayerStates.Names;
        }
    }
}
