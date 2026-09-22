import re

with open('Components/PluginInfo.cs', 'r') as f:
    content = f.read()

autowire = """        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Output)
                if (param.Recipients.Count > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ox = 250;
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 0, "string", ox, -20);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "string", ox, 20);
            }
        }

        protected override void RegisterInputParams"""

content = content.replace("        protected override void RegisterInputParams", autowire)

with open('Components/PluginInfo.cs', 'w') as f:
    f.write(content)
