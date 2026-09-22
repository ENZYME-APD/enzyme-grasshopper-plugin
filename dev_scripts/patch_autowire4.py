import re

with open('Utils/AutoWireHelper.cs', 'r') as f:
    content = f.read()

if "WireInputParam" not in content:
    new_method = """
        public static void WireInputParam(GH_Component comp, GH_Document doc, int paramIndex, string paramType, int offsetX, int offsetY)
        {
            if (paramIndex >= comp.Params.Input.Count) return;
            if (comp.Params.Input[paramIndex].SourceCount > 0) return;

            Grasshopper.Kernel.IGH_Param param = null;
            paramType = paramType.ToLower();
            if (paramType == "curve") param = new Grasshopper.Kernel.Parameters.Param_Curve();
            else if (paramType == "point") param = new Grasshopper.Kernel.Parameters.Param_Point();
            else if (paramType == "mesh") param = new Grasshopper.Kernel.Parameters.Param_Mesh();
            else if (paramType == "line") param = new Grasshopper.Kernel.Parameters.Param_Line();

            if (param != null)
            {
                param.CreateAttributes();
                System.Drawing.PointF compPivot = comp.Attributes.Pivot;
                param.Attributes.Pivot = new System.Drawing.PointF(compPivot.X - offsetX, compPivot.Y + offsetY);
                doc.AddObject(param, false);
                comp.Params.Input[paramIndex].AddSource(param);
            }
        }
"""
    content = re.sub(r'(\s*}\s*}\s*)$', new_method + r'\1', content)
    with open('Utils/AutoWireHelper.cs', 'w') as f:
        f.write(content)
