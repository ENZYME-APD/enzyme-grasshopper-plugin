import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

merge_method = """
        public static void WireMergeWithSliders(GH_Component comp, GH_Document doc, int paramIndex, double[] defaults, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            var mergeProxy = Grasshopper.Instances.ComponentServer.FindObjectByName("Merge", true, true);
            if (mergeProxy == null) return;
            var merge = mergeProxy.CreateInstance() as IGH_Component;
            if (merge == null) return;

            System.Drawing.PointF compPivot = comp.Attributes.Pivot;
            merge.CreateAttributes();
            merge.Attributes.Pivot = new System.Drawing.PointF(compPivot.X - offsetX, compPivot.Y + offsetY);
            doc.AddObject(merge, false);

            var varParam = merge as Grasshopper.Kernel.IGH_VariableParameterComponent;
            
            // Merge defaults to 2 inputs. Ensure we have enough.
            while (merge.Params.Input.Count < defaults.Length)
            {
                if (varParam != null)
                {
                    var newParam = varParam.CreateParameter(Grasshopper.Kernel.GH_ParameterSide.Input, merge.Params.Input.Count);
                    merge.Params.RegisterInputParam(newParam);
                    varParam.VariableParameterMaintenance();
                }
            }

            for (int i = 0; i < defaults.Length; i++)
            {
                Grasshopper.Kernel.Special.GH_NumberSlider slider = new Grasshopper.Kernel.Special.GH_NumberSlider();
                slider.CreateAttributes();
                slider.Slider.Minimum = 0m;
                slider.Slider.Maximum = 10m;
                slider.Slider.Value = (decimal)defaults[i];
                slider.Attributes.Pivot = new System.Drawing.PointF(merge.Attributes.Pivot.X - 150, merge.Attributes.Pivot.Y - (defaults.Length * 20 / 2) + i * 20);
                doc.AddObject(slider, false);
                merge.Params.Input[i].AddSource(slider);
            }

            comp.Params.Input[paramIndex].AddSource(merge.Params.Output[0]);
        }
"""

class_end_idx = content.rfind("}")
class_end_idx = content.rfind("}", 0, class_end_idx)

content = content[:class_end_idx] + merge_method + content[class_end_idx:]

with open('Utils/AutoWireHelper.cs', 'w') as f:
    f.write(content)
