import re

with open('Components/AnalysisDashboard.cs', 'r') as f:
    content = f.read()

source_lock_func = """
        private bool IsSourceLocked(Grasshopper.Kernel.IGH_Param param)
        {
            if (param == null) return false;
            
            var topLevel = param.Attributes?.GetTopLevel?.DocObject as Grasshopper.Kernel.IGH_ActiveObject;
            if (topLevel != null && topLevel.Locked) return true;

            if (param.Sources != null && param.Sources.Count > 0)
            {
                foreach (var src in param.Sources)
                {
                    if (IsSourceLocked(src)) return true;
                }
            }
            return false;
        }

        protected override void SolveInstance(IGH_DataAccess DA)"""

content = content.replace("        protected override void SolveInstance(IGH_DataAccess DA)", source_lock_func)

solve_logic = """        protected override void SolveInstance(IGH_DataAccess DA)
        {
            _run = false;
            DA.GetData(0, ref _run);

            if (!DA.GetData(1, ref _jsonPayload)) _jsonPayload = "";

            bool upstreamDisabled = false;
            if (this.Params.Input[1].Sources.Count > 0)
            {
                foreach (var src in this.Params.Input[1].Sources)
                {
                    if (IsSourceLocked(src)) 
                    {
                        upstreamDisabled = true;
                        break;
                    }
                }
            }

            if (upstreamDisabled)
            {
                _jsonPayload = "";
            }

            _size = 12.0; DA.GetData(2, ref _size);"""

content = re.sub(r'protected override void SolveInstance\(IGH_DataAccess DA\)\s*\{\s*_run = false;\s*DA\.GetData\(0, ref _run\);\s*if \(\!DA\.GetData\(1, ref _jsonPayload\)\) _jsonPayload = "";\s*_size = 12\.0; DA\.GetData\(2, ref _size\);', solve_logic, content)

with open('Components/AnalysisDashboard.cs', 'w') as f:
    f.write(content)

