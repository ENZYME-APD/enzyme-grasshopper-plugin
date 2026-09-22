with open('Components/SunHoursAnalysis.cs', 'r') as f:
    content = f.read()

# 1. Add Input Parameter
inputs_orig = """            pManager.AddVectorParameter("Sun Vectors", "Vectors", "Solar vectors from the Heliodon.", GH_ParamAccess.list);
            
            pManager[0].Optional = true;"""

inputs_new = """            pManager.AddVectorParameter("Sun Vectors", "Vectors", "Solar vectors from the Heliodon.", GH_ParamAccess.list);
            pManager.AddColourParameter("Gradient", "Gradient", "Optional custom color gradient (list of colors from 0% to 100%).", GH_ParamAccess.list);
            
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[3].Optional = true;
            pManager[5].Optional = true;"""
content = content.replace(inputs_orig, inputs_new).replace("pManager[1].Optional = true;\n            pManager[3].Optional = true;", "")

# 2. Add Output Parameter
outputs_orig = """            pManager.AddMeshParameter("Analysis Mesh", "Mesh", "The auto-subdivided Massing mesh (for easy gradient coloring).", GH_ParamAccess.item);
        }"""
outputs_new = """            pManager.AddMeshParameter("Analysis Mesh", "Mesh", "The auto-subdivided Massing mesh (for easy gradient coloring).", GH_ParamAccess.item);
            pManager.AddColourParameter("Colors", "Colors", "Color mapped to each point based on exposure.", GH_ParamAccess.list);
        }"""
content = content.replace(outputs_orig, outputs_new)

# 3. Add InterpolateColor helper method
helper_method = """        private System.Drawing.Color InterpolateColor(List<System.Drawing.Color> gradient, double t)
        {
            if (gradient == null || gradient.Count == 0) return System.Drawing.Color.White;
            if (gradient.Count == 1) return gradient[0];
            
            t = Math.Max(0.0, Math.Min(1.0, t));
            double scaledT = t * (gradient.Count - 1);
            int idx1 = (int)Math.Floor(scaledT);
            int idx2 = (int)Math.Ceiling(scaledT);
            if (idx1 >= gradient.Count - 1) return gradient[gradient.Count - 1];
            if (idx1 == idx2) return gradient[idx1];
            
            double blend = scaledT - idx1;
            System.Drawing.Color c1 = gradient[idx1];
            System.Drawing.Color c2 = gradient[idx2];
            
            int r = (int)(c1.R + (c2.R - c1.R) * blend);
            int g = (int)(c1.G + (c2.G - c1.G) * blend);
            int b = (int)(c1.B + (c2.B - c1.B) * blend);
            
            return System.Drawing.Color.FromArgb(255, r, g, b);
        }

        protected override System.Drawing.Bitmap Icon"""
content = content.replace("        protected override System.Drawing.Bitmap Icon", helper_method)

# 4. Read Gradient in SolveInstance
solve_data_orig = """            DA.GetDataList(3, context); // Optional
            if (!DA.GetDataList(4, vectors)) return;

            if (vectors.Count == 0) return;"""
solve_data_new = """            DA.GetDataList(3, context); // Optional
            if (!DA.GetDataList(4, vectors)) return;
            
            List<System.Drawing.Color> customColors = new List<System.Drawing.Color>();
            DA.GetDataList(5, customColors);

            if (vectors.Count == 0) return;

            if (customColors.Count == 0)
            {
                customColors = new List<System.Drawing.Color> { 
                    System.Drawing.Color.FromArgb(255, 0, 0, 139), // DarkBlue
                    System.Drawing.Color.FromArgb(255, 0, 255, 255), // Cyan
                    System.Drawing.Color.FromArgb(255, 255, 255, 0), // Yellow
                    System.Drawing.Color.FromArgb(255, 255, 0, 0) // Red
                };
            }"""
content = content.replace(solve_data_orig, solve_data_new)

# 5. Apply colors in loop
loop_orig = """                sunHits[i] = hits;
                exposures[i] = (double)hits / totalRays;
            });

            sw.Stop();"""
loop_new = """                sunHits[i] = hits;
                exposures[i] = (double)hits / totalRays;
            });

            System.Drawing.Color[] outColors = new System.Drawing.Color[testPoints.Count];
            Parallel.For(0, testPoints.Count, i => {
                outColors[i] = InterpolateColor(customColors, exposures[i]);
            });

            sw.Stop();"""
content = content.replace(loop_orig, loop_new)

# 6. Output colors
out_orig = """            DA.SetDataList(2, testPoints);
            
            if (isWorkflow1 && displayMesh.IsValid)
            {
                DA.SetData(3, displayMesh);
            }

            Message ="""
out_new = """            DA.SetDataList(2, testPoints);
            
            if (isWorkflow1 && displayMesh.IsValid)
            {
                DA.SetData(3, displayMesh);
            }
            
            DA.SetDataList(4, outColors);

            Message ="""
content = content.replace(out_orig, out_new)

with open('Components/SunHoursAnalysis.cs', 'w') as f:
    f.write(content)
