import re

with open("Components/GradientGenerator.cs", "r") as f:
    content = f.read()

icon_code = """        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Enzyme.IconLoader.Load("Gradient Generator.png");
            }
        }

        public override Guid ComponentGuid"""

content = content.replace("        public override Guid ComponentGuid", icon_code)

with open("Components/GradientGenerator.cs", "w") as f:
    f.write(content)
