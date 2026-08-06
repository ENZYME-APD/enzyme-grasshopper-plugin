"""
JB Grasshopper Gem - OOP Masterplan JSON Dashboard (V5 - Patched)
================================================================================
Reads a summarized JSON payload from the OOP Masterplan Engine and renders 
a responsive HUD. 
- FIXED: Background box scales correctly with multiple building titles.
- FIXED: Targets are now case-insensitive and display in both Global and Building modes.

INPUTS:
    run, JSON_Payload, GroupByBldg, title, suffix, TargetGlobal, TargetJSON
    size, anchor, font, transparency, ox, oy, fit_padding
================================================================================
"""

import json
import Rhino
import System.Drawing
import scriptcontext as sc

KEY = "JB_GEM_OOP_DASHBOARD_V5"

def format_num(n):
    try: return "{:,.2f}".format(float(n))
    except: return str(n)

# ==============================================================================
# VIEWPORT RENDER CALLBACK
# ==============================================================================
def draw_callback(sender, e):
    data = sc.sticky.get(KEY + "_DATA")
    if not data: return

    display_data = data['display_data'] 
    l_size, l_anchor = data['size'], data['anchor']
    font_face = data['font_face']
    alpha, ox, oy, padding = data['alpha'], data['off_x'], data['off_y'], data['padding']
    
    vp_w, vp_h = e.Viewport.Bounds.Width, e.Viewport.Bounds.Height
    line_h = l_size * 1.6
    title_gap = 20 
    
    max_label_chars = max([len(d['label']) for d in display_data]) if display_data else 10
    value_start_offset = max_label_chars * (l_size * 0.6)
    
    full_lines = [d['label'] + d['val'] for d in display_data]
    max_total_chars = max([len(s) for s in full_lines]) if full_lines else 10
    box_w = (max_total_chars * l_size * 0.55) + (padding * 3.0)
    
    # FIXED: Count EVERY title separator to calculate the exact box height
    title_count = sum(1 for d in display_data if "---" in d['label'] or "===" in d['label'])
    box_h = (len(display_data) * line_h) + (title_count * title_gap) + (padding * 2.5)

    if l_anchor == 1: x, y = vp_w - box_w - ox, oy 
    elif l_anchor == 2: x, y = ox, vp_h - box_h - oy 
    elif l_anchor == 3: x, y = vp_w - box_w - ox, vp_h - box_h - oy 
    else: x, y = ox, oy + 35 

    bg = System.Drawing.Color.FromArgb(int(alpha), 25, 25, 25)
    white = System.Drawing.Color.White
    warning_red = System.Drawing.Color.OrangeRed
    
    rect = System.Drawing.Rectangle(int(x), int(y), int(box_w), int(box_h))
    e.Display.Draw2dRectangle(rect, bg, 1, bg)
    
    current_y = y + padding
    for item in display_data:
        # Skip drawing text for empty spacers, just add height
        if not item['label'].strip() and not item['val'].strip():
            current_y += line_h
            continue
            
        e.Display.Draw2dText(item['label'], white, Rhino.Geometry.Point2d(x + padding, current_y), False, int(l_size), font_face)
        if "---" not in item['label'] and "===" not in item['label']:
            val_p = Rhino.Geometry.Point2d(x + padding + value_start_offset, current_y)
            color = warning_red if item['is_red'] else white
            e.Display.Draw2dText(item['val'], color, val_p, False, int(l_size), font_face)
        
        current_y += (line_h + title_gap) if ("---" in item['label'] or "===" in item['label']) else line_h

# ==============================================================================
# LIFECYCLE MANAGEMENT
# ==============================================================================
def shutdown():
    if KEY in sc.sticky:
        try: Rhino.Display.DisplayPipeline.DrawForeground -= sc.sticky[KEY]
        except: pass
        del sc.sticky[KEY]
    if KEY + "_DATA" in sc.sticky: del sc.sticky[KEY + "_DATA"]
    Rhino.RhinoDoc.ActiveDoc.Views.Redraw()

shutdown()

# ==============================================================================
# DATA PARSING & EXECUTION
# ==============================================================================
run_btn = globals().get('run', False)
json_in = globals().get('JSON_Payload', None)
group_by_bldg = globals().get('GroupByBldg', False)

if run_btn and json_in:
    try:
        data = json.loads(json_in)
        prog_areas = data.get("programs", {})
        total_area = data.get("total_area", 0.0)
        bldg_data = data.get("buildings", {})
        
        # Parse Targets safely (Case-Insensitive matching)
        clean_target_data = {}
        target_json = globals().get('TargetJSON', None)
        if target_json:
            try: 
                raw_targets = json.loads(target_json)
                clean_target_data = {str(k).strip().upper(): float(v) for k, v in raw_targets.items()}
            except: pass
            
        u = str(globals().get('suffix', ""))
        opac = int(max(0.0, min(1.0, float(globals().get('transparency', 0.8) or 0.8))) * 255)
        
        display_data = []
        hud_title = globals().get('title', "MASTERPLAN SUMMARY")
        if hud_title:
            display_data.append({'label': "=== " + str(hud_title).upper() + " ===", 'val': "", 'is_red': False})

        # --- MODE 1: PER BUILDING BREAKDOWN ---
        if group_by_bldg:
            for b_name, b_stats in bldg_data.items():
                display_data.append({'label': "--- " + str(b_name).upper() + " ---", 'val': "", 'is_red': False})
                for p_name, val_area in b_stats["programs"].items():
                    line_label = "  {}: ".format(str(p_name))
                    line_val = "{}{}".format(format_num(val_area), u)
                    display_data.append({'label': line_label, 'val': line_val, 'is_red': False})
                
                b_tot_val = "{}{}".format(format_num(b_stats["total_area"]), u)
                display_data.append({'label': "  SUBTOTAL: ", 'val': b_tot_val, 'is_red': False})
                display_data.append({'label': " ", 'val': " ", 'is_red': False})
            
            # Show Global Targets at the bottom of Building Mode if they exist
            if clean_target_data:
                display_data.append({'label': "--- TARGET TRACKING ---", 'val': "", 'is_red': False})
                for prog_name, val_area in prog_areas.items():
                    target = clean_target_data.get(str(prog_name).strip().upper(), 0.0)
                    if target > 0:
                        pct = (val_area / target) * 100
                        line_label = "  {}: ".format(str(prog_name))
                        line_val = "{}{}".format(format_num(val_area), u)
                        line_val += " / {}{} ({:.1f}%)".format(format_num(target), u, pct)
                        display_data.append({'label': line_label, 'val': line_val, 'is_red': val_area > target})
                display_data.append({'label': " ", 'val': " ", 'is_red': False})
                
            display_data.append({'label': "=" * 15, 'val': "", 'is_red': False})
            
        # --- MODE 2: GLOBAL PROGRAMS ONLY ---
        else:
            for prog_name, val_area in prog_areas.items():
                target = clean_target_data.get(str(prog_name).strip().upper(), 0.0)
                line_label = "{}: ".format(str(prog_name).upper())
                line_val = "{}{}".format(format_num(val_area), u)
                over_target = False
                
                if target > 0:
                    pct = (val_area / target) * 100
                    line_val += " / {}{} ({:.1f}%)".format(format_num(target), u, pct)
                    if val_area > target: over_target = True
                    
                display_data.append({'label': line_label, 'val': line_val, 'is_red': over_target})
            display_data.append({'label': "-" * 15, 'val': "", 'is_red': False})

        # --- GLOBAL MASTERPLAN TOTAL (Always shown at bottom) ---
        tot_is_red = False
        tot_str = "{}{}".format(format_num(total_area), u)
        
        t_glob = globals().get('TargetGlobal', 0)
        if t_glob and t_glob > 0:
            tot_pct = (total_area / t_glob) * 100
            tot_str += " [Target: {}{} ({:.1f}%)]".format(format_num(t_glob), u, tot_pct)
            if total_area > t_glob: tot_is_red = True

        display_data.append({'label': "TOTAL AREA: ", 'val': tot_str, 'is_red': tot_is_red})

        sc.sticky[KEY + "_DATA"] = {
            "display_data": display_data,
            "size": globals().get('size') or 12,
            "anchor": globals().get('anchor') or 0,
            "font_face": globals().get('font') or "Arial",
            "alpha": opac,
            "off_x": float(globals().get('ox') or 20),
            "off_y": float(globals().get('oy') or 20),
            "padding": float(globals().get('fit_padding') or 10)
        }
        
        sc.sticky[KEY] = draw_callback
        Rhino.Display.DisplayPipeline.DrawForeground += draw_callback
        
        mode_msg = "Per-Building Mode" if group_by_bldg else "Global Mode"
        ghenv.Component.Message = "HUD ACTIVE\n{}".format(mode_msg)
        Rhino.RhinoDoc.ActiveDoc.Views.Redraw()
        
    except Exception as e:
        ghenv.Component.Message = "JSON Error: " + str(e)
else:
    if not run_btn: ghenv.Component.Message = "STATE: OFF"
    else: ghenv.Component.Message = "WAITING FOR DATA"