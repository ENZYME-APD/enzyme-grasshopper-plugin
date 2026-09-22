with open("Components/CanvasUsageInfo.cs", "r") as f:
    text = f.read()

added_doc = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Params.Input[0].SourceCount == 0)
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 80, 0);
        }

        public override Guid"""

text = text.replace('        public override Guid', added_doc)

with open("Components/CanvasUsageInfo.cs", "w") as f:
    f.write(text)
