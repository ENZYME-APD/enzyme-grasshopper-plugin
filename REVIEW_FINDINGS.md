# Component review findings

Found while writing the method notes (2026-11-22) and worked through in the review-fixes pass the same day (AI-assisted; geometry changes still need a Rhino canvas check).

Status: 200 fixed, 34 open (need an owner decision or a canvas test), 17 not a bug. Open items are summarised in `PROJECT_CONTEXT.md` section 9.

Legend: `[x]` fixed · `[ ]` open, with the recommendation · `[x] ~~…~~` not a bug.

## 3D Road Slope Analysis (`Components/RoadSlope3DAnalyzer.cs`)

- [x] Vertical segment slope 0 and Compliant; ratio-mode flat segment infinity in Slope Values and HUD averages. **Fixed:** As in RoadProfileUnroller: vertical = 90 deg / +inf % / 1:0 and Non-Compliant; HUD statistics over finite values; Slope Values description documents both cases. Segment Size <= 0 now errors; Mode value list only added on a fresh drop.
- [x] Ratio mode legend labels reversed. **Fixed:** Same label fix as RoadProfileUnroller ('1:X, X >= threshold' / '1:X, X < threshold').
- [x] ~~Info text says finalised grading analysis / ensures compliance with standards~~ **Not a bug:** Info output already uses MethodNotes.Info("RoadSlope3DAnalyzer"); removed the unused stale infoText string from the component.

## Adapter: Archicad Slabs (Tapir) (`Components/AD_ARCHICAD.cs`)

- [x] Null FloorHeights are skipped while null curves keep their slot, shifting later heights onto the wrong floor. **Fixed:** Null heights now keep a NaN placeholder in the flattened list; a floor whose height is null (or curve is null) is skipped, so every other floor keeps its own height.
- [x] ~~Healing also applies to overlaps (negative gap), raising the base and shortening the floor~~ **Not a bug:** Documented behaviour (method notes: gap within SnapTolerance either above or below is snapped). Owner to confirm intent; if overlaps should not be healed, change the test to 0.001 < gap <= SnapTolerance.
- [x] Duplicated Optional loop; FloorContours/FloorHeights optional but component returns silently without them. **Fixed:** Added Hud.Waiting messages when FloorContours or FloorHeights are missing or empty. The redundant Optional loop is harmless and left as is (no style refactor; inputs must stay optional for the waiting message).

## Adapter: The Lego Builder (`Components/AD_LEGO.cs`)

- [x] Description is a debug string and input descriptions repeat the names. **Fixed:** Rewrote the component description and the five input descriptions (meaning, defaults, units).
- [x] JSON built by string interpolation without escaping names. **Fixed:** Building, block, tower and program strings are now written with Newtonsoft JsonConvert.ToString (quoted and escaped). Output for ordinary names is identical.
- [x] Floor count (int)(totalHeight/fh) has no tolerance (11.9999 m gives 2 floors + 3.9999 m). **Fixed:** Floor count = floor((height + 0.001) / FloorHeight); remainder = max(0, height - floors x FloorHeight), rounded to 1 mm.
- [ ] boundary_segments keep absolute world Z (same question as AD_CAKE). **Open:** Same owner question as AD_CAKE: confirm the MP Engine ignores segment Z before changing coordinates.

## Adapter: The Sculptor (`Components/AD_SCULPT.cs`)

- [x] Malformed RecipeJSON swallowed by empty catch, silently replaced by one 4 m Mixed_Use floor. **Fixed:** The catch now clears any partly parsed recipe and adds a runtime warning with the parse error, then falls back to one 4 m Mixed_Use floor as before.
- [x] Block names use Math.Round(relative_z) with no decimals, giving duplicate names. **Fixed:** Names are Floor_<relative level rounded to 2 decimals, invariant culture, trailing zeros dropped>_<TowerID>. A repeated name within a building gets the suffix _2, _3 and so on. Whole-metre levels keep their old names. Nothing parses the names downstream: MasterplanEngine only reads them as labels.
- [x] SerializeExactCurve drops a segment that passes IsArc but fails TryGetArc. **Fixed:** A segment that passes IsArc but fails TryGetArc now goes to the Polyline branch. Any segment that still cannot be written is counted, and the component warns that those boundaries are open.
- [x] Massing is Optional but the solve returns with no output when it is empty. **Fixed:** When Massing is missing or empty the HUD now shows Awaiting Massing (Hud.Waiting) instead of returning with no message. The port stays Optional so the wiring does not change.

## Adapter: The Sliced Cake (`Components/AD_CAKE.cs`)

- [ ] boundary_segments keep absolute world Z while base_z is relative to the building base. **Open:** Needs owner confirmation of how the MP Engine reads segment Z; changing coordinates would change payloads users rely on. Recommendation: confirm the engine ignores segment Z, or write segments relative to the healed base.
- [x] Block names Floor_<rounded relative Z>_<TowerID> collide for floors less than 1 m apart. **Fixed:** Names are now made unique per building: the existing name is kept and a duplicate gets a suffix _2, _3... Non-colliding names are unchanged.
- [x] Healing also snaps overlaps, description only mentions gaps. **Fixed:** Behaviour kept (method notes already say above or below); SnapTolerance description corrected to say gaps or overlaps and what is snapped.
- [x] Floors with no section at the slice plane are dropped silently. **Fixed:** Added a runtime warning listing the count and item indices (first 20) of skipped Breps.

## Advanced Export Views (`Components/AdvancedExportViews.cs`)

- [x] Current GH State output goes empty while a capture is pending and after the last state. **Fixed:** The state being captured is re-emitted while pending; the last exported state is kept on the output after the export finishes and while idle.
- [ ] Fixed 500 ms delay not tied to solution completion. **Open:** Needs a Rhino canvas test. Recommendation: wait for the document SolutionEnd event (plus a short delay) instead of a fixed 500 ms, or expose the delay as an appended input.
- [x] Views fed by Create Named Views with Run off is empty, so ALL named views are exported. **Fixed:** Root cause fixed in Create Named Views (names listed regardless of Run). Here a warning is shown when Views is connected but empty (export-all behaviour kept, as documented).

## Analysis Dashboard (`Components/AnalysisDashboard.cs`)

- [x] OnDrawForeground returns early when there are no colours, so a payload with only metrics is never drawn. **Fixed:** The early return now happens only when there are no Colors and no Metrics. A payload with only metrics draws the title and metrics.

## Analysis Heatmap (`Components/AnalysisHeatmap.cs`)

- [x] InfoText describes Cells as exact values, but per-vertex input shows the vertex mean. **Fixed:** Corrected the Cells line of the (unused) InfoText constant; the Info output already comes from MethodNotes, which is correct.
- [ ] InfoText ends with Method after Meerkat (T.-T. Tran, MIT): attribution to confirm. **Open:** Owner to confirm whether code or method came from Meerkat; then add a proper reference to the method notes or remove the line. The InfoText constant is not output (Info uses MethodNotes).

## Area Converter (`Components/AreaConv.cs`)

- [x] HUD reports Done before the conversion loop runs. **Fixed:** Moved the stopwatch stop and Hud.Done after the conversion loop and SetDataTree.

## Assign Layer Materials (`Components/LayerMat.cs`)

- [x] Instructions_Out (output 0) plays the role of an Info port with fixed text. **Fixed:** Instructions_Out now carries the usage text followed by the method notes (the port keeps its name; it plays the Info role).

## Auto List Item (`Components/AutoListItem.cs`)

- [x] With several branches the slider maximum is reset per branch and ends at the last branch length. **Fixed:** The longest list length is collected over all branches during the solution (reset in BeforeSolveInstance) and the slider update is scheduled once in AfterSolveInstance with that maximum.

## Auto-Grade Road (`Components/AutoGrade.cs`)

- [x] Smoothing near the pinned ends can push the end steps above Max Slope; comment claims a guarantee. **Fixed:** After smoothing, a forward then backward slope clamp is run on the interior points only (ends stay pinned), so every step of the point profile is <= Max Slope. Checked numerically on 3000 random profiles: max step/Max Slope = 1.000 after the clamp (up to 72 before). Misleading comment corrected.
- [ ] Degree-3 3D Road can overshoot between points; reported max grade is measured on the point profile. **Open:** Already stated in the method notes (simplifications). Changing the output curve type (e.g. to a polyline) or measuring grade on the curve changes results; owner decision. Recommendation: report the grade sampled along the output curve, or offer a polyline output.
- [x] Resolution <= 0 not guarded. **Fixed:** Resolution <= 0 / NaN / infinite now stops with a clear error; negative or NaN Max Slope also gives an error.

## Auto-Phase Assigner by Z (`Components/AutoPhase.cs`)

- [x] Phase User Text written with CommitChanges and no undo record. **Fixed:** The whole run is wrapped in doc.BeginUndoRecord / EndUndoRecord and Phase is written via doc.Objects.ModifyAttributes on a duplicate of the attributes (undoable); falls back to the old CommitChanges path if ModifyAttributes fails. Needs a Rhino check that Ctrl+Z restores phases.
- [ ] All TowerIDs containing 'podium' form one podium sequence per building ordered by Z. **Open:** Design question for the owner. Recommendation: confirm; if separate podiums should be phased independently, group podium blocks by TowerID like towers.

## BIM Attribute Serializer (`Components/BimJson.cs`)

- [x] Failed TryParse of FloorHeight / Floors / Phase sets 0 instead of defaults. **Fixed:** On a parse failure the defaults 3.5 / 1 / 0 are restored and a warning counts the bad values.
- [x] FloorHeight parsed with current culture. **Fixed:** FloorHeight, Floors and Phase are parsed with NumberStyles + CultureInfo.InvariantCulture.
- [x] ~~Refresh input read but never used~~ **Not a bug:** Its purpose is only to trigger a re-solve (re-read of User Text); any input change does that, so no code is needed. Port kept (released).
- [x] Arc segment whose TryGetArc fails is silently dropped. **Fixed:** Such a segment now falls through to the polyline branch (approximated as Polyline) instead of being dropped.
- [x] PushToRhino auto-wired to a Toggle instead of a Button. **Fixed:** Auto-wiring now places a Button on PushToRhino (newly dropped components only; saved files unchanged).

## BIM Key Initializer (`Components/InitKeys.cs`)

- [x] Description says referenced curves, but any object type gets keys. **Fixed:** Component description now says referenced Rhino objects of any type.

## BranchSizeSplit (`Components/BranchSizeSplit.cs`)

- [x] MinItems/MaxItems are truncated numbers; auto-wired sliders 0-3 default 1.5. **Fixed:** Auto-wire now uses integer sliders (0-10, defaults 1 and 3); port descriptions say the values are whole numbers and decimals are truncated. Port type unchanged (number) to keep saved files working.

## Canvas State Manager (`Components/CanvasStateManager.cs`)

- [x] Import JSON input (index 5) is registered but never read. **Fixed:** SolveInstance now reads input 5; when its text changes, the JSON is deserialized and its states merged into the stored states (same as Paste from Clipboard); unreadable JSON gives a warning. Merging only on change avoids overwriting local saves every solve.

## Canvas Style (`Components/CanvasStyle.cs`)

- [x] GH_Skin change is global and persists; no revert; should be stated in the description. **Fixed:** Description now states the colours apply to the whole Grasshopper application, stay after the component or file is closed and are not restored.

## Canvas Usage Info (`Components/CanvasUsageInfo.cs`)

- [x] Text says 'Last saved by' but shows the user and time of the click. **Fixed:** Label changed to 'Report generated by'; date/time line is the report time.
- [x] Total Components includes the two panels the tool creates. **Fixed:** The panels named 'PlugIns Used' and 'All Components Used' are skipped when counting.

## Change Pulse (`Components/ChangePulse.cs`)

- [x] Item access with a single previous value: list input pulses on almost every solution. **Fixed:** Previous value stored per solve iteration (dictionary keyed by DA.Iteration), so item i is compared with item i of the previous solution.
- [x] Pulse fires on the first solution after opening a file (can trigger saves/exports). **Fixed:** The first value seen for an iteration only initialises the memory and outputs False; pulses fire on real changes, including data becoming empty and data arriving again after being empty.

## Color Palette (`Components/ColorPalette.cs`)

- [x] Count = 1 is treated like 0 (returns all stops) but the description does not say so. **Fixed:** The Count input description now reads: number of evenly spaced colours to return, 2 to 1024 (0 or 1 = the palette's own stops). Behaviour is unchanged.

## Conditional Fillet (`Components/CondFillet.cs`)

- [x] threshold_pct described as % but used as fraction capped at 0.49; auto-wired slider 0-3 default 1.5 always clamped. **Fixed:** Description now says fraction (0.25 = 25 %, capped at 0.49); auto-wired slider 0-0.49 default 0.25; negative values give an error; values above 0.49 give a remark. Values above 1 are not reinterpreted as percent (would change results).
- [x] If CreateFilletCurves fails, the corner stays sharp but radii still reports the chosen radius. **Fixed:** On failure the corner's radius in radii is set to 0 and a warning is raised.
- [x] When the join gives several pieces a PolyCurve is built by appending parts without checking continuity. **Fixed:** Parts are chained into a PolyCurve only if each starts within 0.01 of the previous end; otherwise a warning is raised, the input curve is returned unchanged and its radii are reported as 0 (previously the first joined fragment could be returned).

## Contact Enzyme (`Components/ContactEnzyme.cs`)

- [x] Email body uses the HUD-formatted CurrentMessage, not the raw Message. **Fixed:** Added RawMessage (the raw Message input) and use it as the mailto body; CurrentMessage is unchanged.

## Create Named Views (`Components/CreateNamedViews.cs`)

- [x] View Names empty whenever Run is False, so Advanced Export Views exports ALL named views. **Fixed:** When Run is False the component now lists the names it would create (Prefix + 4-digit index) without touching the document; description updated.
- [x] ~~Views regenerated on every solution while Run is True~~ **Not a bug:** Run-toggle semantics as documented; regenerating on upstream change is what keeps views in sync. Recommendation: use a Button on Run.

## CurtainWallSystem (`Components/CWS.cs`)

- [x] Description says detailed LOD 300 but members are zero-thickness fins and glass is one surface without profiles. **Fixed:** Component description rewritten: says members are zero-thickness fins and glass is one unsplit surface unless profile curves are supplied.
- [ ] FindCurvatureTransitions makes every NURBS span end and >10 % curvature change a corner mullion (many unwanted mullions on freeform curves). **Open:** Changes the mullion layout users get; needs an owner decision and a canvas test. Recommendation: keep only G1 kinks and straight/curved transitions (drop span ends and the 10 % curvature rule, or make it an option).
- [x] Closed base curves likely get a second corner mullion at the seam (only exact Equals guard). **Fixed:** Discontinuity points and the end point are only added if no existing corner point lies within model tolerance, so the seam gets one corner mullion.
- [x] Mullion tangent taken at a linearly mapped parameter while the point uses PointAtNormalizedLength. **Fixed:** Tangent is now taken at segment.NormalizedLengthParameter(normalizedT) (same arc-length position as the point), with the old linear map only as fallback.
- [x] RhinoDoc.ActiveDoc dereferenced without null check (headless / Rhino.Compute). **Fixed:** All 9 uses replaced by a Tol property: model absolute tolerance, or 0.001 when no document is active.
- [x] Non-positive Heights raise an Error but processing continues for other curves. **Fixed:** Now a Warning stating that the curve with a non-positive height is skipped (behaviour unchanged, message matches it).

## Curve Segment Dispatcher (`Components/SegDisp.cs`)

- [x] Free-form segments silently dropped. **Fixed:** Segments that are neither linear nor arcs are counted and a runtime warning reports how many were not output. Also guarded num_dec (clamped to 0..15 with a remark; a negative value used to throw).
- [x] Text dots output wrapped in GH_ObjectWrapper (do not preview or bake). **Fixed:** Text dots are now appended as GH_TextDot (Grasshopper's TextDot goo, previewable and bakeable) instead of GH_ObjectWrapper. Recommend a quick canvas check of preview/bake of the dims output.

## CWProfile (`Components/CWP.cs`)

- [x] HUD reports Done before input validation. **Fixed:** Validation errors now go through Hud.Error and return; Hud.Done is set only after the polyline is output.

## CWT (`Components/CWT.cs`)

- [x] Name, description and port descriptions are placeholders. **Fixed:** Component description and all port descriptions rewritten (what it does, units, defaults, tree matching). Name/NickName 'CWT' and port names kept (released). Recommendation for owner: consider a clearer display name such as 'Curtain Wall Transoms' (GUID unchanged, docs regenerate).
- [x] Outward direction tested against centroid; fails on concave outlines. **Fixed:** Outward side now comes from curve.ClosedCurveOrientation(plane): for counter-clockwise outlines Normal x Tangent points inwards and is reversed. The centroid test is kept only as a fallback when the orientation is undefined. Convex outlines give the same result as before. Needs a Rhino check on an L / U plan.
- [x] modSize <= 0 not validated. **Fixed:** Curves with modSize <= 0 / NaN / infinite are skipped with a warning; string values are parsed with invariant culture.
- [x] curveIndex incremented but never used. **Fixed:** Dead variable removed.

## Data Visualizer (`Components/DataVisualizer.cs`)

- [x] ~~Info text calls it LEAP DATA VISUALIZER (copied from another plugin)~~ **Not a bug:** Already resolved: all Info SetData calls now output MethodNotes.Info("DataVisualizer"); no LEAP text remains in the code.
- [x] Bar Thickness is used as hexagon circumradius, so bars are about 2 x W wide. **Fixed:** Geometry unchanged (users rely on it); the input description now states it is the circumradius of the hexagonal bar, so the bar is about twice this wide. Port name unchanged (released).
- [x] min and max taken over the whole Values list, including values beyond the number of points. **Fixed:** Min and max are now computed only over the first min(Points, Values) values, i.e. the values that are drawn.
- [x] Joined mesh has no normals computed. **Fixed:** masterMesh.Normals.ComputeNormals() is called before output.

## Divide Target Length by Segment (`Components/DivideTargetSegment.cs`)

- [x] DuplicateSegments does not split single NURBS curves with kinks. **Fixed:** After DuplicateSegments, every segment is also split at its interior G1 discontinuities (Curve.GetNextDiscontinuity with Continuity.G1_continuous; seam of closed curves not included), so kinked NURBS (e.g. degree 1) are divided per straight piece.

## Document Size to Pixels (`Components/PaperSizeToPixels.cs`)

- [x] Screen formats multiplied by DPI/72: 16:9 FHD at default 300 DPI gives 8000 x 4500. **Fixed:** Screen formats now output their native pixel size and ignore DPI (FHD = 1920 x 1080 at any DPI); DPI description updated; DPI <= 0 gives an error. Note for owner: anyone who used DPI on screen formats for supersampling now gets native size.

## Elevation Label (`Components/ElevationLabel.cs`)

- [x] Info output only set inside the bake-layer-created branch. **Fixed:** The previous docs commit already sets Info at the top of the solve. I removed the leftover second DA.SetData(3) in the bake branch.
- [x] ~~Info text claims sampling the terrain and height above sea level~~ **Not a bug:** Already resolved: the old static text was replaced by MethodNotes.Info("ElevationLabel") in the previous docs commit, which describes what the code does. No change needed.
- [x] Setting inputs are tree access but only item 0 of branch 0 is read. **Fixed:** Length, Gap, Style, TextPlane, Orientation and Anchor are now read per point: matching branch (else the last branch), matching item (else the last item), else the old default. Dimension styles are cached per name. A single value gives the same result as before. Bake, BakeLayer and BakeName stay global (first item).
- [x] Nested BakeLayer path A::B creates an invalid layer name; with Bake on and no BakeName every solution bakes duplicates. **Fixed:** New EnsureLayer creates each level of a :: path with ParentLayerId (an empty BakeLayer falls back to Elevations) and warns if the layer cannot be created. When BakeName is empty, a per-component id ElevLabel_<InstanceGuid> is used, so a re-bake replaces this component's previous bake instead of duplicating it. This changes behaviour for anyone who relied on stacking copies without a BakeName.

## Enzyme Heliodon (`Components/Heliodon.cs`)

- [x] Description says annual sun vectors but only the 21st of each month is sampled. **Fixed:** Component description corrected: hourly sun vectors on the 21st of each selected month (one representative day per month).
- [x] A Hours start that is not a whole or half hour gives no Sun Vectors. **Fixed:** The loop now starts at the start hour rounded up to the next half hour, so whole hours are always hit (8.25 starts at 8.5). The loop end has a 1e-9 tolerance.
- [x] Months read as Interval Min..Max, so 11 to 2 becomes February to November. **Fixed:** Months are read as T0 to T1. A start after the end wraps over the new year (11 to 2 = Nov, Dec, Jan, Feb). Normal increasing domains are unchanged. A deliberately reversed full domain such as 12 to 1 now gives Dec and Jan instead of all 12 months. The input description is updated.
- [x] No North input; rotated models get wrong sun directions. **Fixed (owner, 2026-11-22):** North input appended (degrees CCW from +Y, Ladybug convention).

## Export Named Views (`Components/ExportViews.cs`)

- [x] Auto-wire connects a True toggle to Transparent; JPG and BMP then may get a black background. **Fixed:** ViewCapture.TransparentBackground is now on only when Transparent is True and the format is not jpg, jpeg or bmp. PNG and TIFF, which have an alpha channel, keep transparency. The Transparent input description says so. The auto-wire default is unchanged.

## Facade JSON Parser (`Components/FacadeParser.cs`)

- [ ] Branch indices count only buildings/programs passing the filters, so paths shift with filters. **Open:** Fixing it changes the output paths of every filtered definition; owner decision. Recommendation: index buildings and programs by their position in the JSON regardless of filters (stable paths).
- [x] Null BoundsClosed appended when a floor has no closed bounds. **Fixed:** Nulls are kept on purpose so BoundsExt / BoundsClosed / Heights / Programs stay aligned by index; a warning now counts the floors without BoundsClosed.
- [x] ~~Which tool writes this JSON schema (differs from BIM_JSON)?~~ **Not a bug:** Question for the maintainer, not a code defect. Recommendation: document the producer of the facade JSON schema in the method notes.

## Facade Module Distributor (`Components/FacadeDist.cs`)

- [x] Downloads and executes Python from a public GitHub test repo without integrity check (security risk). **Resolved (owner, 2026-11-22):** node unpublished (hidden) and its download / run code disabled until the script ships inside the plugin.
- [ ] Method cannot be documented because the geometry is in the remote facade_modules.py. **Open:** Needs the script in this repo (owner). Recommendation: add facade_modules.py to the repo/plugin and document its method in MethodNotes.
- [x] All exceptions reported as Compile error, hiding the real Python error. **Fixed:** Read and execution errors now show 'Script read error: ...' / 'Python error: ...' with the exception message and a runtime error balloon; a failed download with a cached file gives a warning, a failed first download shows 'Sync error: ...'.
- [x] Description mentions hot-reloading, but the file is only re-checked when the component solves. **Fixed:** Description rewritten: loads and runs facade_modules.py; a changed file is re-read the next time the component solves.
- [x] ~~WebClient is obsolete in .NET 6+ (Rhino 8)~~ **Not a bug:** The plugin targets net48, where WebClient is supported; on .NET 6+ it is only marked obsolete (warning SYSLIB0014) and still works. Replace with HttpClient if the target framework moves to .NET 7/8.

## Facade Module: Balcony (`Components/ModBalcony.cs`)

- [x] Walls made with Extrusion.Create, which follows the curve plane normal; clockwise outlines and single segments may not extrude to +Z. **Fixed:** Defensive orientation fix as the brief allows. All 9 Extrusion.Create calls (rail, sill, glass, header, corner, solid) now use a helper ExtrudeUp, Surface.CreateExtrusion(curve, (0, 0, height)).ToBrep(), so they always extrude along world +Z. For planar horizontal curves extruded up the geometry is the same. Outputs are still Breps. Needs a Rhino canvas check before release.
- [x] Only offsets[0] is used; when Offset returns several pieces the balcony is partial without a warning. **Fixed:** When Offset returns several pieces they are joined first. If they still do not join into one curve, a warning names the branch and floor and says the balcony is partial. The first piece is still used.
- [x] Description mentions intelligent corners, but corners are trimmed segment ends or short segments. **Fixed:** Component description corrected to say how corner pieces are made.

## Facade Module: Frames (`Components/ModFrames.cs`)

- [x] Extrusion.Create follows each curve's own plane normal: beam side faces, sills, glass, headers and fins may extrude downwards. **Fixed:** Defensive +Z fix: new helper ExtrudeUp tries Extrusion.Create(h), then Extrusion.Create(-h), keeping whichever goes from the curve up by h; otherwise falls back to Surface.CreateExtrusion along +Z (kinky faces split). Used for beam sides, open-beam end caps, sills, glass, headers and fins. Columns unchanged. Counter-clockwise results are unchanged; still worth a canvas check with clockwise outlines.
- [ ] ClosedCurveOrientation called on open curves; result probably Undefined so FlipDir alone decides. **Open:** Behaviour on open curves cannot be verified without Rhino and changing it may flip existing facades. Recommendation: decide explicitly (e.g. outward = right of curve direction, FlipDir reverses), test on canvas, then remove the orientation call.
- [x] Base beam added for index 0 of each branch, not the lowest storey. **Fixed:** The base beam now goes on the curve with the lowest bounding-box Z in each branch (index 0 if sorted, so sorted inputs are unchanged).
- [x] BeamWidth is the vertical beam height and BeamDepth the horizontal depth; description wording unclear. **Fixed:** Port names unchanged (released); BeamWidth description now 'Vertical height of the beams (m)', BeamDepth 'Horizontal depth of the beams: offset distance from the boundary (m)', and the component description says what it builds.

## Facade Module: Rhythmic Balconies (`Components/ModRhythmBalc.cs`)

- [x] ~~Closed-curve wrap-around merge only when there is a single middle piece~~ **Not a bug:** On a closed polyline the seam lies at a vertex, i.e. a corner; bays never merge across any corner (each segment is processed separately), so first/last bays not merging is consistent. Only a single smooth closed segment (circle) has a tangent seam, where wrap-around applies. Recommendation for owner if merging across tangent joins of polycurves is wanted.
- [x] Extrusion direction follows TryGetPlane normal, not world Z. **Fixed:** All 9 extrusions (corners, glass, headers, rails, partitions, solid walls) now use Surface.CreateExtrusion with +Z (helper ExtrudeZ). Needs a Rhino canvas check.
- [x] Description mentions fast math, but areas are bay length x Depth (wrong for curved bays). **Fixed:** Description now says the HUD slab area is a quick estimate (bay length x Depth), approximate for curved bays. Calculation unchanged.

## Facade Module: Spandrels (`Components/ModSpandrel.cs`)

- [x] Missing Heights silently uses 4.0 m for every floor. **Fixed:** Counts floor outlines without a matching Heights value and warns that 4.0 m was used.

## Facade Module: Storefront (`Components/ModRetail.cs`)

- [x] All port descriptions are placeholders (Input double, Output Brep). **Fixed:** All 14 input and 6 output descriptions rewritten with meaning, units and defaults (taken from the code and method notes).
- [ ] TransomHeight defaults to 0 when unwired: whole height becomes header, no glass, no mullions. **Open:** Choosing a default height is an owner decision. Added a Remark when TransomHeight is 0 or unset and documented the behaviour in the port description. Recommendation: default unwired TransomHeight to the full height (all glass) or to the auto-wire value 6.0 m clamped to the height.
- [x] Bands and fins use Extrusion.Create(curve, height), along the curve's own plane normal (not unique for lines, follows arc orientation). **Fixed:** Defensive orientation fix: new ExtrudeUp helper uses Surface.CreateExtrusion(curve, (0,0,height)), so kickplate, middle band, header and fins always go up world +Z. Outputs are still Breps.
- [x] Description promises intelligent canopies and structure; canopies are flat offsets and columns are lines. **Fixed:** Component description rewritten to say flat canopies and column lines at the mullions.

## Facade Module: Vertical Fins (`Components/ModFins.cs`)

- [x] Extrusion.Create follows the curve plane normal; clockwise outlines may extrude downwards. **Fixed:** All extrusions (glass, spandrel, fins) now use Surface.CreateExtrusion with an explicit +Z vector (helper ExtrudeZ). Needs a Rhino canvas check (clockwise outline, fins on straight lines).
- [ ] VertPattern indexed by item index within a branch; depends on upstream tree structure. **Open:** Documented behaviour; changing it changes results. Recommendation: state in method notes that each branch must hold one building's floors in order (or add an optional floor-index input at the end).

## Fast CFD (`Components/FastCFD.cs`)

- [x] Diffusion and friction use dt_sec = dt x CellSize / wind speed instead of dt (s), so Viscosity and Friction depend on CellSize and wind speed. **Fixed:** dt_sec = dt. Velocities are in cells per second, so the advection step dt is already in seconds. Viscosity and Friction now act per physical second, independent of grid and speed. RESULTS CHANGE: diffusion and friction per step become (pedestrian speed / CellSize) times stronger, about 1.6 times with the defaults (10 m/s at 10 m, about 6.2 m/s at 1.5 m, CellSize 4). Needs a Rhino canvas check and possibly a review of the default Friction 0.1 and Viscosity 0.1 before release. The Info dt now reports the true step.
- [ ] Terrain slope factor only seeds the initial field and is washed out; Info promises topographic speed-up. **Open:** Applying a speed-up during the time steps would be a new model term (as an inflow or source term) with no published coefficient. I only corrected Info item 4 to say it seeds the initial field only. Owner decision: either add a calibrated per-step term or keep it as an initial seed (the method notes already say so).
- [x] Comment cites Jackson and Hunt with 1 + 0.6 x slope; code uses 1 + 0.5 x slope; neither is Jackson-Hunt. **Fixed:** Comment corrected to: heuristic hill speed-up 1 + 0.5 x slope along the wind, clamped 0.7 to 1.4, not the Jackson and Hunt formula. The code is unchanged.
- [x] Convergence check compares u with u0 (post-advection field), not the previous step; never runs when minDomainFlushes > 250. **Fixed:** On check steps the field is copied to uPrev and vPrev at the start of the step, and the largest change is measured against that full previous step. The start condition is still iter >= minDomainFlushes. When the domain flush needs more than the 250-step cap the run is not flushed, so no early stop is correct; I kept that, and it is not a defect.
- [x] Friction list order (column by column from minimum X) is not documented. **Fixed:** The Friction input description now gives the order: columns from minimum X, cells in each column from minimum Y, last value repeats.
- [x] Lawson classes use gust speed while ComfortThreshold uses mean speed; the figures are not comparable. **Fixed:** Labels only, no change to the maths. Dashboard metric names are now 'Comfort (mean <= x m/s)' and 'Sitting (gust <4 m/s)'. The Info heading gives the gust heuristic, and the custom-threshold line says it uses mean speed and is not comparable with the gust classes. The ComfortThreshold description says the same. Whether both should use one speed is an owner decision.

## Fast Noise Analyzer (`Components/FastNoiseAnalyzer.cs`)

- [x] Barrier term 10 log10(3+20N) equals ISO 9613-2 with C2 = 40, not 20. **Fixed (owner chose ISO, 2026-11-22):** Dz = 10 log10(3 + 10 N), ISO 9613-2 C2 = 20.
- [ ] ComponentGuid 11112222-3333-4444-5555-666677778888 looks hand-typed. **Open:** Changing the GUID breaks saved files; owner decision. Checked: unique within this repo (only also in Archive/FastNoiseAnalyzer_v1_pre_review.cs, the old copy). Recommendation: keep unless a clash is reported.
- [x] ~~Run = false branch inside Solve is unreachable because of UsesRunGate~~ **Not a bug:** Harmless dead code (base class replays cached outputs); left as is (no style refactors).
- [ ] Colours scaled per run min/max; no fixed dB range. **Open:** Feature request needing new appended ports; owner decision. Recommendation: append an optional 'Range' interval input (dB) at the end.

## Fillet Rule Configurator (`Components/FilletRules.cs`)

- [x] Rules beyond the shortest list are dropped without warning; RuleTypes not checked. **Fixed:** Warns when RuleTypes, RuleMatches and RuleRadii differ in length. RuleTypes that match Tower, Program or Building ignoring case are written in the exact case, because BimJson compares case-sensitively. Unknown types are kept but warned about.

## Flow Accumulation Heatmap (`Components/FlowHeat.cs`)

- [x] VisualScale slider created with min 1.5, max 3.0, value 1.0 (outside range). **Fixed:** The auto-wired slider is now min 1.0, max 3.0, value 1.0, matching the port default. Before, Grasshopper clamped it to 1.5.
- [x] Non-polyline curves are ignored without a warning but counted in Paths Evaluated. **Fixed:** Skipped non-polyline curves are counted and reported in a runtime warning. Counting is unchanged.

## Global Flood Engine (`Components/GlobalFloodEngine.cs`)

- [ ] Water exchanged as depth per vertex without area weighting: volume not conserved on irregular meshes. **Open:** Changes the method and results; needs owner decision and a Rhino-free implementation with tests in Utils (out of scope for this batch). Recommendation: weight exchanges by vertex Voronoi/one-third area and move the solver to Utils with conservation tests.
- [x] Info text claims exact depths and retention pond volumes, none computed. **Fixed:** The claim was in a dead local string never output (the Info port already shows MethodNotes, which describe the method honestly); the dead string was removed.
- [x] ~~Early stop rarely triggers; runtime is normally the full Iterations~~ **Not a bug:** Results are correct; Iterations is the intended cap. Performance only. Recommendation if needed: stop when the largest moved depth in an iteration is below a small tolerance (e.g. 0.1 mm), which slightly changes results, so owner call.
- [x] Static Info string built by concatenation into local info and set with DA.SetData(3, info). **Fixed:** The unused local info string was removed; output 3 is set from MethodNotes.Info only.

## GroupByKey (`Components/GroupByKey.cs`)

- [x] Tree inputs: every branch writes paths 0,1,2... so groups of different branches merge. **Fixed:** When either input has more than one branch, output paths are {input branch path; group index} (DA.ParameterTargetPath). Single-branch input keeps the old {0},{1},... paths, so existing flat-list definitions are unchanged.
- [x] Null key makes the Dictionary throw. **Fixed:** Null keys are grouped under an internal sentinel and output as a null unique key.

## Hygrothermal Comfort (`Components/ThermalComfortAnalyzer.cs`)

- [x] Name and title spell Higrothermal. **Fixed:** Renamed to Hygrothermal Comfort (nickname HygroComfort); GUID unchanged so saved files keep working; docs page renamed.
- [x] Steadman AT expects 10 m wind; pedestrian-level speeds bias AT warm. **Fixed:** Stated in the VelocityValues description (no conversion: a height correction would be an invented coefficient without an agreed profile).
- [x] Count mismatch: nothing computed, HUD says Done with 0 best/0 worst. **Fixed:** Added a warning with both counts and HUD Awaiting matching VelocityValues; missing TerrainMesh now shows Awaiting TerrainMesh. Also the Dashboard Avg value is now formatted in invariant culture.
- [x] Ring search stops at first ring with a candidate, not always the true nearest. **Fixed:** After the first candidate, the search keeps widening while a closer point could sit in the next ring (ring R is at least (R-1) x cell size away), so the result is the exact nearest tag point.

## JSON Metadata Inspector (`Components/JsonInspect.cs`)

- [x] Non-integer floor_index (2.5 or text) throws and the whole inspection fails. **Fixed:** floor_index is read by TryAddLevel: integers, integral floats and integer text (invariant culture) are accepted; other values are skipped and counted in a warning instead of failing the whole inspection.

## JSON MP Engine V2 (`Components/MasterplanEngine.cs`)

- [x] Fallback programme colour seeded with string.GetHashCode(), unstable per process on .NET Core. **Fixed:** Seed is now a stable FNV-1a 32-bit hash of the trimmed program name (masked to a positive int). Note: unpaletted programs get different (but now stable) colours than before.
- [ ] Facades: a block's own base level is not in master_slabs for non-podium towers, so setback/one-floor-per-block edges are dropped from BoundsExt. **Open:** Needs a Rhino canvas check with a setback tower and an AD_ARCHICAD payload; changing it alters facade output. Recommendation: in the facade loop, test exterior edges against the union of master_slabs[relZ] and the outlines of all blocks occupying that floor (including the block itself), rather than changing master_slabs (which also drives slabs and roofs).
- [x] Open boundary curve gives null AreaMassProperties and area silently 0. **Fixed:** Area is computed once per block; if the boundary is not a closed planar curve a runtime warning names the block and says its area is counted as 0 (result unchanged otherwise).
- [x] Description (high-performance topological coordinator) does not say what it computes. **Fixed:** Component description now lists slabs per level, roof tops and setback roofs, railings, exterior facade edges per floor and gross areas per program and building, and the downstream parsers/dashboard.

## Keyline Engine (`Components/KeylinePattern.cs`)

- [x] ~~Info text claims off-contour lines with constant fall toward the ridges~~ **Not a bug:** The Info output already uses MethodNotes.Info("KeylinePattern"), which states plainly that lines are equal plan offsets with no controlled fall; the old claim is no longer in the component.
- [x] Count is per side: output holds 2 x Count + 1 curves per guide, guide included. **Fixed:** Keylines output description now states this. Also added a guard: Spacing <= 0 gives a clear runtime error (it produced duplicate or swapped offsets).

## Keypoint Engine (`Components/KeypointFinder.cs`)

- [x] Info/description overstates the method (second derivative / exact inflection). **Fixed:** Info already comes from the corrected method notes; the component description now says it takes the vertex with the largest steep-to-flat drop in smoothed slope.
- [x] Uphill-drawn streams give negative slopes and a flat-to-steep point. **Fixed:** A stream whose start is lower than its end is reversed before the analysis (streams flow downhill).
- [x] A keypoint is returned even when no slope decrease exists (maxChange <= 0). **Fixed:** Streams with no positive slope drop get no keypoint (and no keyline); a remark counts them.
- [ ] ComponentGuid AA112233-... looks hand-typed. **Open:** Changing it breaks saved files; owner decision. Checked unique within this repo. Recommendation: keep unless a clash is reported.

## Legend Geometry (`Components/LegendGeometry.cs`)

- [x] Legend Type Binary and Discrete not handled (title only). **Fixed:** Binary and Discrete payloads are drawn as Blocks (one swatch per colour) with one name per block whenever there is one label per colour (any count), and their first SubLabel (e.g. Compliance %) below. Blocks legends are unchanged.
- [x] Gradient legends use Labels[0] and Labels[1] as end labels even when more labels exist. **Fixed:** Right end label is now Labels[last].
- [x] ~~Info text says it bakes the legend and reads Slope, Height, Flow~~ **Not a bug:** Info output already comes from MethodNotes.Info("LegendGeometry"); the old text is gone.
- [x] InterpolateColor is unused dead code. **Fixed:** Removed the unused private method.

## Map to Gradient (`Components/MapToGradient.cs`)

- [x] Reversed Source Domain clamps every value to the first colour. **Fixed:** Values are mapped from T0 to T1 with the signed length, so a reversed domain flips the gradient. Auto domain ignores NaN / infinite values.
- [x] NaN value gives an invalid index and Color.FromArgb throws. **Fixed:** NaN values get the first colour with a warning (output stays aligned with the input list).

## Masses JSON Parser (`Components/MassParser.cs`)

- [x] Single-segment boundary returned without MakeClosed: open polyline gives area 0 / failed extrusion, block silently missing from Volumes and Areas. **Fixed:** Boundary closing now applies to single segments too: MakeClosed(0.01), and if the gap is larger a closing line is joined. A Warning now names any block whose boundary is missing or cannot be extruded, so mismatched trees are no longer silent.
- [x] Extrusion.Create follows the curve plane normal; clockwise boundary may extrude downwards. **Fixed:** Defensive orientation fix: after Extrusion.Create, if the extrusion goes below the boundary (by more than half the height) it is re-created with -height, so blocks always rise from true_z. Needs a canvas check with clockwise test data before release.
- [ ] Block paths use the original block index (gaps after filtering); building index counts only matching buildings. **Open:** Owner decision: paths are what downstream definitions index into. Recommend: make both indices stable (original building index and original block index) or both compact; changing either breaks existing wiring.

## Master JSON Config Builder (`Components/ConfigJson.cs`)

- [x] JSON_Palette description says OOP Engine; consumer is JSON MP Engine V2. **Fixed:** Output description now reads: Connect to the ColorPalette input of JSON MP Engine V2.

## Masterplan Clearance Engine (`Components/Clearance.cs`)

- [x] Delaunay topology vertex indices used directly as node indices. **Fixed:** Topology vertex indices are now mapped through TopologyVertices.MeshVertexIndices(tv)[0] before indexing validIndices, with a bounds guard.
- [x] No LabelPlane for a zero-length clearance line, so LabelPlanes can fall out of step with the other outputs. **Fixed:** A zero-length clearance (touching outlines) now gets a World XY plane at its mid-point, so every Lines, Distances and Categories item has a LabelPlane.
- [x] Description says topological or proximity clearances, but neighbours come from bounding-box centres. **Fixed:** Corrected the component description: shortest clearances between neighbouring outlines, with neighbour pairs from a Delaunay triangulation or a proximity search of the outline bounding-box centres. The GUID and ports are unchanged.

## Masterplan Dashboard (`Components/Dash.cs`)

- [x] Auto-wire puts a 0 to 2 slider on Target Global (area target). **Fixed:** Auto-wired slider range changed to 0 to 1,000,000; input description now says it is a total-area target and 0 = no target.
- [x] Transparency is applied as opacity (1 = opaque), opposite of its name. **Fixed:** Behaviour kept (changing it would alter existing definitions; port name cannot change); description now states 0 = fully transparent, 1 = opaque.
- [x] Numbers use the current culture (N2), separators change with locale. **Fixed:** FormatNum and the % values use InvariantCulture; Convert.ToDouble of payload / target values also uses InvariantCulture.

## Microclimate Comfort (`Components/MicroclimateComfort.cs`)

- [x] UTCI polynomial has 98 of 210 terms. **Fixed:** Full 210-term operational polynomial (Broede et al. 2012) ported from pythermalcomfort models/utci.py (MIT, attribution in code comment), converted by script from the Python source; vapour pressure now uses the same Hardy (1998) saturation formula as the UTCI program/pythermalcomfort instead of Magnus. Verified: the C# function reproduces all scalar SI reference cases of pythermalcomfort's test data (validation-data-comfort-models v1.1.0 ts_utci.json: 25/27/50/1 -> 25.19 (25.2), 19/24 -> 20.03 (20.0), 19/14 -> 16.77 (16.8), 27/22 v1 -> 25.43 (25.5, tol 0.1), v10 -> 20.01 (20.0), v16 -> 15.80 (15.8), 30/27 -> 29.53 (29.6), 9/9 -> 8.65 (8.7), 25/25 -> 24.61 (24.6)), plus test_utci_optimized [24.73, 26.57] and the extrapolation value -326.0792 (Python check of the converted expression).
- [x] No validity limits for Tmrt - Ta; wind clamped to 30 m/s. **Fixed:** Inputs are clamped to the polynomial's validity range: Ta -50..50 C, Tmrt-Ta -30..70 C, va 0.5..17 m/s, RH 0..100 %, Pa 0..5 kPa; a warning counts the points that were clamped (pythermalcomfort returns NaN there; clamping keeps colours/dashboard working, owner may prefer NaN).
- [x] Extreme heat (>46) and extreme cold (<-40) UTCI categories missing. **Fixed:** Added 'Extreme Heat Stress' (UTCI > 46) and 'Extreme Cold Stress' (UTCI < -40); other bands unchanged.
- [x] 'Precise' label hides that Tmrt is a fixed +15 C x Solar Factor guess. **Fixed:** Component description and Method port description now say UTCI uses Tmrt approximated as Ta + 15 C x Solar Factor, which dominates the uncertainty ('Precise' removed from descriptions; value-list item text and dashboard sub-label unchanged).

## Noise Emitter From Curves (`Components/NoiseEmitterFromCurves.cs`)

- [x] Calibration valid only if the analyser uses pure spherical spreading; state which analyser model it pairs with. **Fixed:** Info text (component part) now states it pairs with the Fast Noise Analyzer, that the analyser's air absorption (2.8 dB/km) only slightly lowers the level at RefDist, and that its ground effect (Ground supplied, G > 0) and barriers are not compensated, so L_ref is reproduced only for hard ground and free view. No formula changed.

## Pixel Gradient (`Components/ColorMeshTiles.cs`)

- [x] Bake runs on every solution while bake_trigger is true; with empty bake_name duplicates pile up. **Fixed:** Rising-edge detection: with an empty bake_name the component bakes only when bake_trigger turns from false to true (a Remark explains why it is not re-baking); with bake_name set it keeps re-baking each solution, which replaces the earlier bake (idempotent) as before.
- [x] Same colour twice in gradient_colors double-counts HUD tile counts. **Fixed:** HUD lists each distinct colour once, labelled with its tag (Tile n of its first position).
- [x] Description says BIM-ready idempotent engine / Mega-Meshes; HexFacade_ layer prefix for any grid. **Fixed:** Component description rewritten to say what it does and that re-bakes replace earlier ones only when bake_name is set. Layer prefix HexFacade_ NOT changed: renaming baked layers would break existing Rhino files and Elefront workflows; owner decision (recommend keeping, or a new optional layer-prefix input appended at the end).
- [x] Fan apex is the bounding-box centre; concave cells give overlapping/flipped triangles. **Fixed:** Before fanning, each fan triangle normal is checked against the polygon (Newell) normal; if any is flipped or degenerate the cell is meshed with Mesh.CreateFromClosedPolyline instead. Convex/star-shaped cells keep the original fan (identical output).

## Pixel Image (`Components/PixelatedSurface.cs`)

- [x] Tile counts count mesh faces, not cells. **Fixed:** Cells are counted per colour while building the meshes; tags, HUD and bake tags use cell counts.
- [x] Bake: tags listed per palette colour (incl. empty) are matched to meshes listed only for non-empty colours. **Fixed:** A tag name list parallel to the output meshes (Tile i from the palette index, or Accent Tile) is built and used for baking, so tags, layers and materials go to the right mesh. panel_tags output unchanged in shape.
- [x] Bake layer names include tile count (new layers on every change; colon in name). **Fixed:** Bake layer name is now HexFacade_<Tag>_<RRGGBB> (e.g. HexFacade_Tile1_141414): no count, no colon, distinct per colour. Material_Tag user string keeps 'Tile 1: <count>'. Layers baked by old versions keep their old names.
- [x] new Bitmap(path) locks the file and the cache ignores changes on disk. **Fixed:** The image is copied into memory and the file handle released (using block); the cache reloads when the path or the file's last-write time changes; the old bitmap is disposed.
- [x] Bake is a level trigger: Toggle left on with no Bake Name bakes another copy every solve. **Fixed:** Without a Bake Name, baking happens only when Bake turns from False to True. With a Bake Name the old replace-on-every-solve behaviour is kept (it is idempotent).

## Point Probe (`Components/PointProbe.cs`)

- [x] Mesh with as many vertices as faces: values always read as per-vertex. **Fixed:** A warning now flags the ambiguity (values read per vertex). Recommendation for owner: append an optional 'Per Face' boolean input at the end to resolve it explicitly.

## Polyline Healer (`Components/PHeal.cs`)

- [x] Tolerance is an extension length, not a tolerance/factor; no default, so nothing happens until wired. **Fixed:** Port name kept (released); description corrected to an extension length in model units, and a default of 1.5 added (same as the auto-wire slider). Negative values now give a clear error.
- [x] Description says it creates boolean regions; returns only the largest unioned outline. **Fixed:** Component description now says it returns the largest unioned outline and drops holes and islands. Returning all regions would change results: owner decision (recommend an appended output with all regions if needed).

## Procedural Road Generator (`Components/RoadGenerator.cs`)

- [x] Outputs Contours (7) and Main Contours (8) registered but never set. **Fixed:** Contours of the output terrain are now generated with Mesh.CreateContourCurves at every whole metre: multiples of 5 m go to Main Contours, the others to Contours; skipped with a warning if the height range exceeds 5000 units. Descriptions updated. Needs a canvas check.
- [x] Colorize described as colouring terrain and volumes, but only cut/fill meshes are coloured. **Fixed:** Colorize description corrected: colours the cut (red) and fill (blue) volume meshes; the rebuilt terrain is not coloured.
- [ ] Total cut and fill volumes only in the HUD; no numeric outputs. **Open:** Feature request rather than a defect; would need two appended outputs. Recommendation: append Cut Volume (m3) and Fill Volume (m3) number outputs at the end (after Info).
- [x] Pillar spacing uses length x i / station count, not the true arc length. **Fixed:** Station distance is now nCrv.GetLength over [T0, t], the true arc length to the station.
- [ ] Tangent un-flip also reverses genuine turns sharper than 90 deg between stations (hairpins with coarse Subdivide). **Open:** Needs a canvas test on hairpins and on joined curves before changing. Recommendation: remove the un-flip (a NURBS tangent does not flip on its own) or compare with the tangent at the mid-parameter between stations instead of the previous station.
- [ ] Fixed 150 m edge limit and 10 000 m ray offset assume metres; mm models lose most triangles. **Open:** The plugin standard is a metre model (PROJECT_CONTEXT 4.5) and other fixed values (0.1 m, 2 m) and defaults are metric too, so scaling only these would be inconsistent. Added a warning when the model units are not metres; also added an error for Subdivide <= 0. Recommendation: if non-metre models must be supported, scale all fixed lengths with RhinoMath.UnitScale.

## Railing JSON Parser (`Components/RailParser.cs`)

- [x] DeserializeCurve returns joined[0] only, disjoint pieces lost. **Fixed:** All joined pieces are returned (each closed if its gap is within 0.01) and appended to the railing's branch.
- [ ] Engine may write railing segments at absolute Z, curves would be lifted twice. **Open:** Needs owner confirmation of the MP Engine railing schema; no code change until confirmed.
- [x] ~~Flexible mode substring match: A also matches TOWER_A~~ **Not a bug:** Documented behaviour (method notes); changing it would alter filters users rely on. ExactMatch input description now explains both modes. Recommendation: use ExactMatch or wildcards for strict filters.

## Raindrop Flow Engine (`Components/WaterFlow.cs`)

- [x] GridSpacing <= 0 loops forever; slider starts at 0. **Fixed:** GridSpacing and StepSize must be > 0 (runtime error otherwise); seeding uses integer counts with a 1,000,000-seed limit; auto-wired sliders start at 0.1.
- [x] MaxSteps auto-wired to 0-10 slider (default 10). **Fixed:** Auto-wire now uses an integer slider 1 to 2000, default 500 (affects only newly auto-wired components).
- [ ] DropPoints branch index is the projected-hit index, not the seed index; overhangs give several hits. **Open:** Changing branch numbering alters results users may rely on and needs a Rhino test. Recommendation: project each seed with Intersection.MeshRay from above (topmost hit only) and use the seed index as the branch.

## RandomTreeSplit (`Components/RandomTreeSplit.cs`)

- [x] Auto-wired sliders set pA = pB = 1.5, so the component errors on placement. **Fixed:** Auto-wire now uses 0-1 sliders with pA = pB = 0.3 and an integer seed slider (0-100, default 1).
- [x] Port descriptions are just the names; component description is RandomTreeSplit. **Fixed:** Component and all input/output descriptions rewritten (shares, floor rule, pA + pB <= 1, seed truncation, outputs).

## Recipe JSON Builder (`Components/RecipeBuilder.cs`)

- [x] Program names not escaped in string-concatenated JSON. **Fixed:** The program value is written with Newtonsoft JsonConvert.ToString (quotes, backslashes and control characters escaped); layout otherwise unchanged.

## Report Metrics (`Components/ReportMetricsExport.cs`)

- [ ] Flags kept from an existing file (Merge) keep stale rating/value when this run updates the metric. **Open:** Fix belongs in Utils/ReportMetrics.Build (shared file, out of scope): re-rate inherited flags from their stored thresholds. Mitigation added in the component: a warning names each inherited flag whose metric this run updated.
- [ ] Flag lookup takes first metric with the key regardless of scope. **Open:** Fix belongs in Utils/ReportMetrics.Build (e.g. add a scope to FlagRule, default site). Mitigation added: a warning when a flag's metric key exists at more than one scope.

## Road Profile & Slope Unroller (`Components/RoadProfileUnroller.cs`)

- [x] Vertical segment (H = 0, V > 0) gets slope 0 and is Compliant in degree and percent modes. **Fixed:** Vertical segments now give 90 degrees, +infinity percent, or ratio 0 (1:0) and are always Non-Compliant. Same fix in RoadSlope3DAnalyzer.
- [x] Ratio mode flat segment gives infinity in Slope Values and makes HUD Avg/Min infinite. **Fixed:** Slope Values keeps the mathematically correct 1:infinity for a flat segment (explicit, documented in the output description), but HUD Max/Min/Avg are computed over finite values only. Same fix in RoadSlope3DAnalyzer.
- [x] Ratio mode legend labels are the reverse of the compliance rule. **Fixed:** Ratio-mode legend labels are now '1:X, X >= threshold' (compliant colour) and '1:X, X < threshold'; thresholds formatted with InvariantCulture in all modes.
- [x] AddedToDocument adds a Mode value list whenever input 3 has no source, also on reopening a file. **Fixed:** Read() override sets a flag; the value list is only added on a fresh drop (not when the component is read from a file or pasted). Same fix applied to RoadSlope3DAnalyzer. Also added a guard: Segment Size <= 0 gives a clear error.

## Road Slope Analyzer (`Components/RoadSlopeAnalyzer.cs`)

- [x] Pieces made by equal parameter steps, not equal length; actualSegmentSize unused. **Fixed:** Piece ends now use curve.NormalizedLengthParameter (equal-length pieces of length actualSegmentSize), with the old parameter map only as fallback. Added an error for Segment Size <= 0.
- [ ] Ratio mode: a flat piece gives dxy / 0 = infinity in Slope Values. **Open:** How to represent a flat piece in ratio mode (null item, very large value, or switch to %) is an owner decision (see PROJECT_CONTEXT P4). Partial: infinite values are now left out of the HUD Max/Min/Avg so the statistics stay finite.
- [ ] Outputs grouped only by compliant / non-compliant branch; link to curve and position is lost. **Open:** Changing the tree structure would break downstream definitions; owner decision. Recommendation: append outputs with per-curve ordered slopes (path {curve}) or a compliance flag list in input order.
- [x] Unknown Threshold Mode (e.g. 3) gives slope 0 and every piece compliant without warning. **Fixed:** Modes other than 0, 1, 2 now give a clear error.
- [x] Compliance Percentage description says compliant/non-compliant but value is the compliant share. **Fixed:** Description now: Percentage (0 to 100) of analysed segments that are compliant.
- [x] Vertical pieces read as 0 and compliant (PROJECT_CONTEXT P4). **Fixed:** Vertical pieces now read 90 degrees, infinite percent or 1:0 and are never compliant, same convention as 3D Road Slope; flat pieces in ratio mode are infinite (compliant).

## Slab JSON Parser (`Components/SlabParser.cs`)

- [x] When segments do not join into one curve, joined[0] is returned and the rest dropped without warning. **Fixed:** Counts boundaries that join into more than one piece and warns once per solve. The output is unchanged (still the first piece).

## Streamlines (`Components/Streamlines.cs`)

- [x] Info text says lines stop at buildings (only true for gridded data with missing cells). **Fixed:** The Info output already comes from MethodNotes.Info("Streamlines"); the old InfoText constant with the claim was unused dead code and has been removed.

## Surface Plane Finder (`Components/PlaneFinder.cs`)

- [x] Trimmed Brep face uses the untrimmed surface centroid. **Fixed:** For Brep input the origin is now the area centroid of the face itself (BrepFace.DuplicateFace); Surface input unchanged.
- [x] BrepFace.OrientationIsReversed ignored. **Fixed:** Normal is reversed when the face orientation is reversed, so it follows the Brep face.
- [x] Non-planar input only shows a HUD line, no warning. **Fixed:** Added a runtime warning; outputs stay empty.

## Survey Outlier Cleaner (`Components/SurveyOutlierCleaner.cs`)

- [x] Invalid points removed, so Deviation/Score/Flags/Colors misalign with the input list. **Fixed:** Per-point outputs are now indexed by input point: invalid points get null Deviation, Score and Colour and Flag False, with a warning. Output identical when all points are valid.
- [x] HUD shows Max off in m regardless of model units. **Fixed:** HUD unit now follows the document model units (m, mm, cm, km, ft, in, otherwise units).

## Table to DataTree (`Components/ParseTable.cs`)

- [x] double.TryParse uses current culture (Spanish: 4.5 read as 45). **Fixed:** SmartCast parses with NumberStyles.Float and CultureInfo.InvariantCulture: dot is always the decimal point, no thousands separators. Remaining limit: a comma-decimal cell from Spanish Excel (4,5) stays text in tab mode and is split in comma mode; detecting the separator would need an owner decision.
- [x] (int)f overflows for whole numbers beyond about 2.1 billion. **Fixed:** Whole numbers outside the int range are kept as numbers (GH_Number).

## Terrain Generator Pro (`Components/TerrainGeneratorPro.cs`)

- [x] Code defaults for PatternSizeXY / PatternHeightZ differ from auto-wired panels. **Fixed:** Input descriptions now name both: code defaults when empty (500, 150, 30 / 1, 0.3, 0.05) and the auto-wired panel values (150, 50, 20 / 1.0, 0.3, 0.1). Values unchanged.
- [ ] sin-based hash on absolute coordinates loses precision at UTM-size coordinates. **Open:** Changing the hash input would change every generated terrain away from the origin (seed reproducibility). Owner decision. Recommendation: hash coordinates relative to the boundary bounding-box minimum, ideally behind an appended option.
- [ ] With Solid on, mesh.Weld(pi) merges wall/base vertices with terrain edge, colours bleed. **Open:** Needs a Rhino canvas test (closedness / SolidOrientation after a crease-preserving weld). Recommendation: Weld with a crease angle (e.g. 0.5 rad) instead of pi and check the mesh stays closed.
- [x] MinHeight > MaxHeight not checked; HUD area labelled m2 regardless of units. **Fixed:** Min and Max are swapped with a warning when MinHeight > MaxHeight. HUD area uses invariant culture and the document unit (m², mm², ...).

## Terrain Grader (`Components/AdaptiveTerrainGrader.cs`)

- [x] Cut and fill measured against the re-triangulated ModMesh, so resampling error away from pads counts as earthworks. **Fixed:** The grading rule (pad plane inside a pad, slope-envelope clamp outside) is now a local function used both for the ModMesh samples and for the volumes: at each cell centre the original terrain height is compared with the grading rule applied at that same point, not with a ray-cast on the re-triangulated mesh. Away from pads the rule returns the original height, so no spurious volume. ColoredMesh classification still uses ModMesh (unchanged).
- [x] ~~No BoundaryCurves: Info, Contours and MainContours not set (early return)~~ **Not a bug:** Info is already set on iteration 0 at the top of Solve (MethodNotes.Info), before the early return; unset Contours/MainContours outputs are simply empty in Grasshopper, which is correct for the passthrough case.
- [x] ~~Contour levels and 0.01/0.05 m thresholds assume metres; output descriptions say 1m and 5m~~ **Not a bug:** Plugin standard (PROJECT_CONTEXT section 4.5) is a Rhino model in metres, and the method notes already state the metre assumption. No change; owner may later add unit scaling plugin-wide.
- [x] Contours only between original terrain bounding-box Z limits; pads above/below are not fully contoured. **Fixed:** Contour range is now the union of the original terrain and ModMesh bounding boxes.
- [x] ~~Info text mentions roads; code only handles closed pad curves~~ **Not a bug:** The Info output already comes from MethodNotes.Info("AdaptiveTerrainGrader"), which describes pads only; the old hard-coded text no longer exists in the component.

## Terrain Height (`Components/MeshHeightAnalysis.cs`)

- [x] Read() removes outputs after index 9 and re-adds a generic Color Legend, losing Info and Dashboard Data in saved files. **Fixed:** Read() now leaves the current layout (Dashboard Data at 10, Info at 11) untouched. Legacy files: section outputs after 9 are removed, an existing Color Legend / Dashboard Data output at 10 is kept (wires survive), a text Dashboard Data output is added if missing, and an Info output is appended. Needs a Rhino check opening an old and a current .gh file.
- [x] GetNakedEdgePointStatus (per mesh vertex) indexed with topology vertex indices. **Fixed:** Naked flags are mapped to topology vertices: a topology vertex is naked if any of its mesh vertices (MeshVertexIndices) is naked.
- [x] Heatmaps use per-mesh zMin/zMax but the legend uses the global range. **Fixed:** All heatmaps now use one range, the Z extent of the bounding box of all valid input meshes, matching the legend labels. Single-mesh results are unchanged; multi-mesh results change (colours now comparable across meshes).
- [x] Default legend lists blue, cyan, lime, yellow, red but ramp is cyan-yellow-red; single custom colour falls back to default ramp. **Fixed:** Default legend colours are now Cyan, Yellow, Red (matching the ramp). With one custom colour the heatmap uses that colour, matching the legend.
- [x] Description claims section unrolling and 3D/2D labels that were removed; Instructions output is one line. **Fixed:** Description now says it finds local and global peaks and valleys, colours a vertex heatmap by elevation and outputs Dashboard Data. Dead section/label structures and the one-line Instructions text left as is (no refactor; port is released).

## Terrain Mesher (`Components/TerrainMesher.cs`)

- [x] Info says breaklines are followed, but they are only sampled; triangles can cross a breakline between samples. **Fixed:** The Info output (MethodNotes) already states breaklines are enforced by dense sampling, not constrained edges. Corrected the Breaklines port description (it said 'must follow') to say the same and to use a small Spacing. A constrained triangulation is not added (would need new Utils code).

## Terrain Sections (`Components/TerrainSections.cs`)

- [x] Flat layout cursors reset per mesh, so several meshes' flat sections overlap. **Fixed:** Padding and the two layout cursors are initialised once before the mesh loop, so flat sections of all meshes continue the same stacks.
- [x] Output descriptions say X stack -Y and Y stack -X; code stacks +Y and +X. **Fixed:** FlatSectionsX / FlatSectionsY descriptions corrected to +Y / +X (code direction kept).
- [x] Each bake adds a new Rhino group with the same name; toggle re-bakes every solution. **Fixed:** Group is reused via doc.Groups.Find(BakeName) when BakeName is set; with an empty BakeName the component bakes once per false-to-true change of Bake (Remark otherwise). With BakeName set, re-bake still replaces earlier objects every solution.
- [x] Info set inside mesh loop (missing with no valid mesh); SectionMetadata only produced with LayoutFlat on. **Fixed:** Info was already set on iteration 0 (not a bug). SectionMetadata is now output for every section whether or not LayoutFlat is on, built with JObject (escaped) and the plane origin written with InvariantCulture (was current culture: ambiguous on Spanish Windows).

## Terrain Slope (`Components/SlopeTerrainPlus.cs`)

- [x] Total Area Over is a face-count share, not an area share. **Fixed:** Face areas are now computed (triangles, quads via the diagonal cross product); Total Area Over, Avg Mesh Ratio (per-mesh share) and the HUD Over % are area-weighted (falls back to face count only if total area is 0).
- [x] Gradient mode normalises per mesh; same colour = different slopes across meshes; gradient legend has no slope values. **Fixed:** Two passes: the slope range is taken over all input meshes, then all meshes are coloured against that common range. Gradient legend labels are now the slope at each colour stop in the ThresholdMode unit (deg, %, or 1:X), invariant culture. Colours are still relative to this solution's range (not absolute), so not comparable between runs.
- [x] Binary legend Type is Discrete, which Legend Geometry does not draw. **Fixed:** Fixed on the reader side: LegendGeometry.cs now draws Discrete (and Binary) legends as blocks with one label per block. Payload unchanged.
- [x] Info says faces are sorted; description 'ultra-fast ... safe UI automation' is not a method description. **Fixed:** Info already comes from MethodNotes (no sorting claim); component description rewritten to describe the method.

## Tile Pattern (`Components/TileGridGenerator.cs`)

- [x] A tile with all corners and centre inside is classed full even when the boundary cuts into it. **Fixed:** A tile is full only if all test points are inside and no boundary intersection was found; otherwise it goes through the boolean trim.
- [x] For surfaces/Breps only joined[0] of naked edges is used (may be a hole loop); holes ignored. **Fixed:** Brep bases now use the face's OuterLoop (naked-edge join only as fallback); a warning says holes are not cut out when the face has inner loops. Untrimmed Surface bases have no holes. Cutting holes out of tiles is not implemented.
- [x] X Dim / Y Dim not validated: negative values loop forever; tiny values create huge cell counts. **Fixed:** Cell Width (and Cell Height for rectangular types) <= 0 gives an error; an estimated cell count above 250,000 gives an error before any cells are generated.
- [x] Unrecognised Grid Type silently produces no tiles. **Fixed:** Grid Type is trimmed and lower-cased; an unknown type gives an error listing the valid types.
- [x] For curve bases the grid X axis is whatever TryGetPlane returns, not necessarily world X. **Fixed:** Documented in the Direction port description (no behaviour change). Recommendation for owner: default to world X projected on the plane when Direction is not given (changes existing layouts).

## Topology Split Edge Classifier (`Components/TopologySplitEdgeClassifier.cs`)

- [x] Overlap events ignored when splitting; partly shared collinear walls not split. **Fixed:** Overlap events now add both overlap ends (OverlapA on segment i, OverlapB on segment j) as split parameters, so the shared portion becomes its own sub-segment and the midpoint test classifies it as internal and the rest as external. Needs a Rhino check with two rooms sharing part of a wall.
- [x] Split parameters filtered by Tolerance in parameter space instead of distance. **Fixed:** New helper AddSplit keeps a parameter only if it is strictly inside the domain and its point is more than Tolerance (model distance) from both segment ends.
- [ ] MetadataOut mixes internal and external entries; not index-aligned with either output. **Open:** Reordering changes released output; a proper fix needs new outputs. Recommendation: append ExternalMetadata and InternalMetadata outputs at the end (aligned by index) and keep MetadataOut for compatibility.
- [x] Instructions_Out is leftover C# script text. **Fixed:** Port kept (released) but its text is now a one-line note pointing to the Info output, and its description says it is a legacy output.

## Turntable Camera (`Components/TurntableCamera.cs`)

- [x] If Directory is empty or cannot be created, Run does nothing and gives no warning. **Fixed:** With Run True, an empty Directory or a failed CreateDirectory (with its exception message) now gives a runtime warning.
- [x] RhinoDoc.ActiveDoc used without a null check. **Fixed:** Uses RhinoDoc.ActiveDoc?.Views.ActiveView and warns when there is no active document or view.

## Urban Wind Vector Engine HT (Beta) (`Components/WindEngineHTVer.cs`)

- [ ] Default streamline seeds step by uCount x 2 on a column-major grid: irregular pattern, not every other row. **Open:** Owner decision plus canvas test: the intended default seeding is ambiguous (a literal 'every other row' fix would seed only along the x-min edge, bad for easterly winds). Recommend seeding every other cell along the upwind edge(s) of the grid derived from the wind direction, keeping SeedBox as the override.
- [ ] Gap rule takes one closest point per building, so corridors inside one U-shaped/courtyard building are missed. **Open:** Model limitation, not a code slip; fixing needs a new search (several closest points per mesh / ray casts sideways) and a canvas test. Recommend documenting it in the method notes as a simplification now.
- [x] Corner bending: with several qualifying buildings the last one in the list overwrites the direction while boosts multiply. **Fixed:** The bend direction now comes from the nearest qualifying wall (order-independent); the boost multiplication across buildings is unchanged (changing it would alter speeds: owner decision).
- [x] Description says perfectly flat heatmap at custom elevation, but DrapeHeatmap defaults to true. **Fixed:** Component description rewritten: heuristic screening, heatmap draped by default or flat at a custom elevation.
- [ ] Info calls it physically-refined; should be a screening tool; Reading 2016 venturi reference to be checked. **Open:** Info output already uses MethodNotes (no 'physically-refined' claim) and the description now says heuristic screening. The Reading 2016 reference was not added: the maintainer must check the citation before it goes into MethodNotes.cs (shared file, outside this batch).

## Vector Arrow Generator (`Components/VArrow.cs`)

- [x] Double mode: lines between 1.1 and ~2 x HeadLength overlap heads and invert the shaft. **Fixed:** Minimum line length is now 2.2 x HeadLength in Double mode (1.1 x otherwise); skipped lines are reported in a Remark. Non-positive BodyWidth/HeadWidth/HeadLength give a clear error.

## Vertical Projection (`Components/VerticalProjection.cs`)

- [x] Above/below message uses the signed distance along the plane normal, not world Z. **Fixed:** Above or below is now decided by comparing pt.Z with the projected point's Z (the world Z projection direction). The on-plane test still uses the perpendicular distance.

## Wind Rose (`Components/WindRose.cs`)

- [x] Radius description says the busiest sector reaches it, but scale is rounded up to next ring. **Fixed:** Radius description corrected (outermost ring reaches Radius; longest petal usually shorter).
- [x] Direction output is the raw Selector while Speed/Frequency belong to the whole sector. **Fixed:** Behaviour kept (owner decision whether to snap Direction to the sector centre); Direction description now says so and a remark appears when the Selector is not a sector centre, naming the sector centre the statistics belong to. Recommendation: decide whether Direction should output the sector centre.

