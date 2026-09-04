import re

with open("Components/TerrainSections.cs", "r") as f:
    orig = f.read()

# 1. Output order in RegisterOutputParams
orig = orig.replace('''            pManager.AddCurveParameter("FlatSectionsX", "FSX", "2D X-Sections stacked downwards (-Y direction).", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsY", "FSY", "2D Y-Sections stacked leftwards (-X direction).", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelText3D", "LT3D", "Text strings for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPoints3D", "LP3D", "Points for 3D section labels.", GH_ParamAccess.tree);''',
'''            pManager.AddTextParameter("LabelText3D", "LT3D", "Text strings for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddPointParameter("LabelPoints3D", "LP3D", "Points for 3D section labels.", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsX", "FSX", "2D X-Sections stacked downwards (-Y direction).", GH_ParamAccess.tree);
            pManager.AddCurveParameter("FlatSectionsY", "FSY", "2D Y-Sections stacked leftwards (-X direction).", GH_ParamAccess.tree);''')

# 2. Output order in DA.SetDataTree
orig = orig.replace('''            DA.SetDataTree(0, sectionOutlinesX);
            DA.SetDataTree(1, sectionOutlinesY);
            DA.SetDataTree(2, flatSectionsX);
            DA.SetDataTree(3, flatSectionsY);
            DA.SetDataTree(4, labelText3D);
            DA.SetDataTree(5, labelPoints3D);''',
'''            DA.SetDataTree(0, sectionOutlinesX);
            DA.SetDataTree(1, sectionOutlinesY);
            DA.SetDataTree(2, labelText3D);
            DA.SetDataTree(3, labelPoints3D);
            DA.SetDataTree(4, flatSectionsX);
            DA.SetDataTree(5, flatSectionsY);''')

# 3. Update Flat Layout offsets in SolveInstance
orig = orig.replace('''                        double cursorXSecs = globalBB.Max.Y + 20.0;
                        double cursorXYSecs = globalBB.Min.X - 20.0;''',
'''                        double padding = globalBB.IsValid ? globalBB.Diagonal.Length * 0.05 : 10.0;
                        double cursorYXSecs = globalBB.IsValid ? globalBB.Min.Y - padding : -padding;
                        double cursorXYSecs = globalBB.IsValid ? globalBB.Min.X - padding : -padding;''')

# And replace their usages
orig = orig.replace('''var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorXSecs - bbFlat.Max.Y, 0));''', 
'''var xformMove = Transform.Translation(new Vector3d(globalBB.Min.X - bbFlat.Min.X, cursorYXSecs - bbFlat.Max.Y, 0));''')

orig = orig.replace('''cursorXSecs -= ((bbFlat.Max.Y - bbFlat.Min.Y) + padding);''',
'''cursorYXSecs -= ((bbFlat.Max.Y - bbFlat.Min.Y) + padding);''')

# Let's fix globalBB generation - in the current code it's per mesh!
# Wait, let's see how globalBB is generated.
# `BoundingBox globalBB = mesh.GetBoundingBox(true);`
# Actually, the original code had globalBB of ALL meshes. Let's fix that.
# First, strip `BoundingBox globalBB = mesh.GetBoundingBox(true);`
orig = orig.replace('''                    if (sectionsX > 0 || sectionsY > 0)
                    {
                        BoundingBox globalBB = mesh.GetBoundingBox(true);''',
'''                    if (sectionsX > 0 || sectionsY > 0)
                    {''')

# And insert it before the loop
orig = orig.replace('''            int totalSectionsX = 0;
            int totalSectionsY = 0;

            for (int pathIdx = 0; pathIdx < targetMeshes.Paths.Count; pathIdx++)''',
'''            int totalSectionsX = 0;
            int totalSectionsY = 0;

            BoundingBox globalBB = BoundingBox.Empty;
            foreach (var path in targetMeshes.Paths)
            {
                foreach (var obj in targetMeshes.get_Branch(path))
                {
                    if (obj != null && obj.Value != null && obj.Value.IsValid)
                        globalBB.Union(obj.Value.GetBoundingBox(true));
                }
            }

            for (int pathIdx = 0; pathIdx < targetMeshes.Paths.Count; pathIdx++)''')

# 4. AddedToDocument - Autowiring fixes
# In AddedToDocument:
# We want Bake to be a Button (Input 5)
# And the curve outputs to have a curve parameter, then a preview.
# I will write a completely new AddedToDocument for TerrainSections.
new_added_to_doc = '''        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();
            
            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                Enzyme.Utils.AutoWireHelper.WireInputParam(this, document, 0, "mesh", 180, -120);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 2, 0, 50, 5, 330, -40);
                Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 3, 0, 50, 5, 330, 0);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 4, true, 210, 40);
                Enzyme.Utils.AutoWireHelper.WireButton(this, document, 5, 210, 80);
                Enzyme.Utils.AutoWireHelper.WireInputPanel(this, document, 6, "TerrainSections", 210, 120, 100, 30);
                
                // Spawn Curve Parameters and hook them to outputs 0 and 1, then hook Custom Preview Lineweights
                var outCrv0 = new Grasshopper.Kernel.Parameters.Param_Curve();
                outCrv0.CreateAttributes();
                outCrv0.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X + 250, this.Attributes.Pivot.Y - 80);
                document.AddObject(outCrv0, false);
                outCrv0.AddSource(this.Params.Output[0]);
                
                var outCrv1 = new Grasshopper.Kernel.Parameters.Param_Curve();
                outCrv1.CreateAttributes();
                outCrv1.Attributes.Pivot = new System.Drawing.PointF(this.Attributes.Pivot.X + 250, this.Attributes.Pivot.Y - 20);
                document.AddObject(outCrv1, false);
                outCrv1.AddSource(this.Params.Output[1]);
                
                // Now attach Human component to those curve parameters
                Enzyme.Utils.AutoWireHelper.WireHumanCurvePreviewToParam(outCrv0, document, System.Drawing.Color.Gray, 0.35, 200, 0);
                Enzyme.Utils.AutoWireHelper.WireHumanCurvePreviewToParam(outCrv1, document, System.Drawing.Color.Black, 0.35, 200, 40);

                // For FlatSections, wire Curve parameters
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "curve", 250, 100);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 5, "curve", 250, 140);
            }
        }'''

orig = re.sub(r'public override void AddedToDocument.*?\}\s*\}', new_added_to_doc, orig, flags=re.DOTALL, count=1)

with open("Components/TerrainSections.cs", "w") as f:
    f.write(orig)
