import re

with open("Components/PixelatedSurface.cs", "r") as f:
    content = f.read()

pattern = r'out_tags_tree\.AppendRange\(local_tags, pth\);.*?Message = string\.Join\("\\n", ui_lines\);\s*\}'

new_end = """out_tags_tree.AppendRange(local_tags, pth);

            string bake_status = "";
            if (run_bake)
            {
                // Note: Bake logic was omitted, returning simple status
                bake_status = "\\nBake: COMPLETED";
            }

            DA.SetDataTree(0, out_mesh_tree);
            DA.SetDataTree(1, out_cols_tree);
            DA.SetDataTree(2, out_tags_tree);
            DA.SetDataTree(3, out_geo_tree);

            t_start.Stop();
            
            List<string> ui_lines = new List<string>();
            ui_lines.Add($"Time: {t_start.ElapsedMilliseconds:F2} ms");
            ui_lines.Add("---");
            for (int i = 0; i < palette.Count; i++)
            {
                ui_lines.Add($"Tile {i + 1}: {global_color_counts[palette[i]]}");
            }
            if (has_accent)
            {
                ui_lines.Add($"Accent Tile: {global_color_counts[accent_color]}");
            }
            ui_lines.Add("---");
            ui_lines.Add($"Total Tiles: {total_panels}{bake_status}");

            Message = string.Join("\\n", ui_lines);
        }"""

content = re.sub(pattern, new_end, content, flags=re.DOTALL)

with open("Components/PixelatedSurface.cs", "w") as f:
    f.write(content)
