using Rhino;
using Rhino.DocObjects;

namespace Enzyme.Components {
    public class TestActivate {
        public void M() {
            var doc = RhinoDoc.ActiveDoc;
            var view = doc.Views.ActiveView;
            
            int index = doc.NamedViews.FindByName("test");
            if (index >= 0) {
                // does this compile?
                doc.NamedViews.Restore(index, view.ActiveViewport);
            }
        }
    }
}
