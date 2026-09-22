import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

sampler_method = """
        public static void WireImageSampler(GH_Component comp, GH_Document doc, int paramIndex, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            var samplerProxy = Grasshopper.Instances.ComponentServer.FindObjectByName("Image Sampler", true, true);
            if (samplerProxy == null) return;
            var sampler = samplerProxy.CreateInstance() as Grasshopper.Kernel.IGH_Param;
            if (sampler == null) return;

            sampler.CreateAttributes();
            sampler.Attributes.Pivot = new System.Drawing.PointF(comp.Attributes.Pivot.X - offsetX, comp.Attributes.Pivot.Y + offsetY);
            doc.AddObject(sampler, false);
            comp.Params.Input[paramIndex].AddSource(sampler);
        }
"""

class_end_idx = content.rfind("}")
class_end_idx = content.rfind("}", 0, class_end_idx)

if "WireImageSampler" not in content:
    content = content[:class_end_idx] + sampler_method + content[class_end_idx:]
    with open('Utils/AutoWireHelper.cs', 'w') as f:
        f.write(content)
