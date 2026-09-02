import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

merge_method = """
        public static void WireMergeWithSwatches(GH_Component comp, GH_Document doc, int paramIndex, System.Drawing.Color[] colors, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            var mergeProxy = Grasshopper.Instances.ComponentServer.FindObjectByName("Merge", true, true);
            if (mergeProxy == null) return;
            var merge = mergeProxy.CreateInstance() as Grasshopper.Kernel.IGH_Component;
            if (merge == null) return;

            System.Drawing.PointF compPivot = comp.Attributes.Pivot;
            merge.CreateAttributes();
            merge.Attributes.Pivot = new System.Drawing.PointF(compPivot.X - offsetX, compPivot.Y + offsetY);
            doc.AddObject(merge, false);

            var varParam = merge as Grasshopper.Kernel.IGH_VariableParameterComponent;
            
            while (merge.Params.Input.Count < colors.Length)
            {
                if (varParam != null)
                {
                    var newParam = varParam.CreateParameter(Grasshopper.Kernel.GH_ParameterSide.Input, merge.Params.Input.Count);
                    merge.Params.RegisterInputParam(newParam);
                    varParam.VariableParameterMaintenance();
                }
            }

            for (int i = 0; i < colors.Length; i++)
            {
                Grasshopper.Kernel.Special.GH_ColourSwatch swatch = new Grasshopper.Kernel.Special.GH_ColourSwatch();
                swatch.CreateAttributes();
                swatch.SwatchColour = colors[i];
                swatch.Attributes.Pivot = new System.Drawing.PointF(merge.Attributes.Pivot.X - 120, merge.Attributes.Pivot.Y - (colors.Length * 24 / 2) + i * 24);
                doc.AddObject(swatch, false);
                merge.Params.Input[i].AddSource(swatch);
            }

            comp.Params.Input[paramIndex].AddSource(merge.Params.Output[0]);
        }
"""

class_end_idx = content.rfind("}")
class_end_idx = content.rfind("}", 0, class_end_idx)

content = content[:class_end_idx] + merge_method + content[class_end_idx:]

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)
