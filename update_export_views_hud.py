with open("Components/ExportViews.cs", "r") as f:
    ts = f.read()

# 1. Add Icon
old_icon = '''        public override Guid ComponentGuid
        {
            get { return new Guid("8E5B7C2A-4F9D-4638-9B2E-1D7F5A8C9B3D"); }
        }
    }
}'''

new_icon = '''        protected override System.Drawing.Bitmap Icon
        {
            get { return Enzyme.IconLoader.Load("ExportNamedViews.png"); }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("8E5B7C2A-4F9D-4638-9B2E-1D7F5A8C9B3D"); }
        }
    }
}'''

ts = ts.replace(old_icon, new_icon)


# 2. Add Stopwatch Start to SolveInstance
old_solve = '''        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;'''

new_solve = '''        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool run = false;'''
ts = ts.replace(old_solve, new_solve)

# 3. Stop stopwatch and update this.Message at the end of SolveInstance
old_end_solve = '''            DA.SetData(1, info);
        }

        public override void AppendAdditionalMenuItems'''

new_end_solve = '''            DA.SetData(1, info);

            sw.Stop();
            string viewText = viewsToExport != null && viewsToExport.Count > 0 ? (viewsToExport.Count == 1 ? viewsToExport[0].Name : $"{viewsToExport.Count} Views") : "ALL VIEWS";
            if (!run) viewText = "WAITING...";
            string lsText = string.IsNullOrEmpty(layerState) ? "ACTIVE" : layerState.ToUpper();
            string dsText = string.IsNullOrEmpty(displayStyle) ? "ACTIVE" : displayStyle.ToUpper();
            
            this.Message = $"EXPORT NAMED VIEWS\nTime: {sw.ElapsedMilliseconds} ms\n---\nVIEW: {viewText}\nLAYER: {lsText}\nSTYLE: {dsText}";
        }

        public override void AppendAdditionalMenuItems'''
ts = ts.replace(old_end_solve, new_end_solve)


with open("Components/ExportViews.cs", "w") as f:
    f.write(ts)
