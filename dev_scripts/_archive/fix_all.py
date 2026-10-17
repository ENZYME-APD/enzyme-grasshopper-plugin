with open('Components/TurntableCamera.cs', 'r') as f:
    content = f.read()

import re

# 1. Add fields if they don't exist
if "_lastExportDate" not in content:
    content = content.replace("public class TurntableCamera : GH_Component\n    {", 
"""public class TurntableCamera : GH_Component
    {
        private string _lastExportDate = "Never";
        private string _lastExportDuration = "-";
        private int _lastExportFrames = 0;""")

# 2. Fix the SolveInstance bottom half
# Find the start of the bottom half (timer.Stop();)
idx = content.find("timer.Stop();")
if idx != -1:
    top_half = content[:idx]
    
    bottom_half = """timer.Stop();

            if (run && savedFiles.Count > 0)
            {
                _lastExportDate = DateTime.Now.ToString("dd MMM yyyy HH:mm");
                _lastExportDuration = timer.ElapsedMilliseconds.ToString() + " ms";
                _lastExportFrames = savedFiles.Count;
            }

            DA.SetData(0, orbitPath);
            DA.SetDataList(1, camPoints);
            DA.SetData(2, target);
            DA.SetDataList(3, savedFiles);
            
            Message = $"{this.NickName}\\nTime: {_lastExportDuration}\\n---\\nLast: {_lastExportDate}\\nFrames: {_lastExportFrames}";

            DA.SetData(4, "TURNTABLE CAMERA ENGINE\\n"
                + "\\n"
                + "METHODOLOGY:\\n"
                + "Calculates an orbital path around the target point and locks the active Rhino viewport camera to each step. Uses Enzyme's Advanced Export Engine settings to dump high-resolution imagery per frame.\\n\\n"
                + "INTERPRETATION & IMPORTANCE:\\n"
                + "Perfect for architectural and product presentations, creating seamless rotating GIFs or video sequences of your Grasshopper geometry in its final display state.");
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                AutoWireDefaultInputs();
            }
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendItem(menu, "Auto-wire Default Inputs", (s, e) => AutoWireDefaultInputs());
        }

        private void AutoWireDefaultInputs()
        {
            GH_Document doc = OnPingDocument();
            if (doc == null) return;

            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 1, 0.1, 500.0, 100.0, 200, -100);
            Enzyme.Utils.AutoWireHelper.WireSlider1Dec(this, doc, 2, 0.0, 500.0, 50.0, 200, -80);
            Enzyme.Utils.AutoWireHelper.WireSliderInt(this, doc, 3, 1, 360, 36, 200, -60);
            Enzyme.Utils.AutoWireHelper.WirePanel(this, doc, 4, "C:\\\\Turntable", 200, -40, 150, 20);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 8, false, 200, 40);
            Enzyme.Utils.AutoWireHelper.WireBooleanToggle(this, doc, 9, false, 200, 60);
            Enzyme.Utils.AutoWireHelper.WireButton(this, doc, 10, 200, 80);
        }

        protected override System.Drawing.Bitmap Icon => Enzyme.IconLoader.Load("TurntableCamera.png"); // Uses fallback if missing

        public override Guid ComponentGuid => new Guid("4A9812DC-F41C-4B9A-A3E5-D9814C12D55B");
    }
}"""
    
    content = top_half + bottom_half

with open('Components/TurntableCamera.cs', 'w') as f:
    f.write(content)
