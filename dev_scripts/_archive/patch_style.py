with open("Components/CanvasStyle.cs", "r") as f:
    text = f.read()

text = text.replace('this.Message = "Love your style!";', 'this.Message = "Canvas Style\\nLove your style!";')

added_doc = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[0].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 0, Color.FromArgb(255, 255, 250, 90), 120, -40);
            if (this.Params.Input[1].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 1, Color.FromArgb(255, 212, 208, 200), 120, 0);
            if (this.Params.Input[2].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 2, Color.FromArgb(30, 0, 0, 0), 120, 40);
        }

        public override Guid"""

text = text.replace('        public override Guid', added_doc)

with open("Components/CanvasStyle.cs", "w") as f:
    f.write(text)
