with open("Components/DataVisualizer.cs", "r") as f:
    text = f.read()

icon_code = """
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Enzyme.IconLoader.Load("DataVisualizer.png");
            }
        }

        public override Guid ComponentGuid"""

text = text.replace("        public override Guid ComponentGuid", icon_code)

with open("Components/DataVisualizer.cs", "w") as f:
    f.write(text)
