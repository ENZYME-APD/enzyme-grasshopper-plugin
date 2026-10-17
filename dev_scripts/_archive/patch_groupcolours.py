with open("Components/GroupColours.cs", "r") as f:
    text = f.read()

added_doc = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[0].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 0, "GroupA\\nGroupB", 120, -40, 80, 40);
            if (this.Params.Input[1].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 1, Color.FromArgb(255, 100, 150, 255), 120, 0);
            if (this.Params.Input[2].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireColorSwatch(this, document, 2, Color.FromArgb(255, 214, 206, 206), 120, 40);
        }

        public override Guid"""

text = text.replace('        public override Guid', added_doc)

with open("Components/GroupColours.cs", "w") as f:
    f.write(text)
