import re

def update_icon(filepath, icon_name):
    with open(filepath, 'r') as f:
        content = f.read()

    icon_property = f"""
        protected override System.Drawing.Bitmap Icon
        {{
            get
            {{
                return Enzyme.Utils.IconLoader.Load("{icon_name}");
            }}
        }}

        public override Guid ComponentGuid"""
    
    content = content.replace("public override Guid ComponentGuid", icon_property.strip())
    
    with open(filepath, 'w') as f:
        f.write(content)

update_icon('Components/SortCurvesByAxis.cs', 'sort curve by axis.png')
update_icon('Components/PixelatedSurface.cs', 'Pixelated Surface.png')
