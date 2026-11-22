# Component review findings

Found while writing the method notes for every component (2026-11-22, AI-assisted code reading; not yet verified in Rhino).
Each finding is a candidate: confirm on a canvas or in the code before fixing. Tick it and add the PR when fixed, or strike it with the reason when it is not a problem.
Priorities are summarised in `PROJECT_CONTEXT.md` section 9.

250 findings in 91 components.

## 3D Road Slope Analysis (`Components/RoadSlope3DAnalyzer.cs`)

- [ ] RoadSlope3DAnalyzer.cs: a vertical segment (H = 0, V > 0) gets slope 0 and is Compliant in degree and percent modes; in ratio mode a flat segment returns infinity in Slope Values and in the HUD averages.
- [ ] RoadSlope3DAnalyzer.cs: in ratio mode the legend labels (<= and >) are the reverse of the compliance rule.
- [ ] RoadSlope3DAnalyzer.cs: Info text says it is used for finalised grading analysis and ensures compliance with standards; it checks only one threshold on average segment slope.

## Adapter: Archicad Slabs (Tapir) (`Components/AD_ARCHICAD.cs`)

- [ ] AD_ARCHICAD.cs: null curves are kept as placeholders (so indices stay aligned) but null FloorHeights are skipped, which shifts every later height onto the wrong floor. Treat both the same way.
- [ ] AD_ARCHICAD.cs: healing also applies to overlaps (negative gap), raising the base and shortening the floor; check this is intended.
- [ ] AD_ARCHICAD.cs: the duplicated Optional loop in RegisterInputParams is harmless but redundant; FloorContours and FloorHeights are marked optional although the component returns silently without them (no Waiting message).

## Adapter: The Lego Builder (`Components/AD_LEGO.cs`)

- [ ] AD_LEGO.cs: the description is a debug string (STAGE 1 ADAPTER ... VERBOSE DIAGNOSTICS & FIXED STACKING) and input descriptions just repeat the names; they do not say what the component does.
- [ ] AD_LEGO.cs: JSON is built by string interpolation without escaping, so a program, tower or building name containing a double quote or backslash produces invalid JSON. AD_CAKE uses JsonConvert; AD_LEGO should too.
- [ ] AD_LEGO.cs: floor count uses (int)(totalHeight / fh) with no tolerance. A block modelled at 11.9999 m with 4 m floors gives 2 floors and a 3.9999 m remainder; in Stretch mode this becomes 1 standard floor plus an 8 m top floor. Add a small epsilon before truncating.
- [ ] AD_LEGO.cs: boundary_segments keep absolute world Z while base_z is relative to the building base (same question as AD_CAKE).

## Adapter: The Sculptor (`Components/AD_SCULPT.cs`)

- [ ] AD_SCULPT.cs: a malformed RecipeJSON is swallowed by an empty catch and silently replaced by one 4 m Mixed_Use floor; with RepeatLast False this slices only the ground floor. Should warn.
- [ ] AD_SCULPT.cs: block names use Math.Round(relative_z) (banker's rounding, no decimals), so floors less than 1 m apart and several section curves on one floor of the same tower get identical names.
- [ ] AD_SCULPT.cs SerializeExactCurve: a segment that passes IsArc but fails TryGetArc is dropped silently, leaving an open boundary.
- [ ] AD_SCULPT.cs: Massing is registered as Optional but the solve returns with no output when it is empty.

## Adapter: The Sliced Cake (`Components/AD_CAKE.cs`)

- [ ] AD_CAKE.cs: boundary_segments keep absolute world coordinates (Z at the original floor bottom) while base_z is relative to the building base. Confirm the MP Engine ignores the Z of the segments; otherwise healed floors and buildings not at Z = 0 are offset.
- [ ] AD_CAKE.cs: block names are Floor_<rounded relative Z>_<TowerID>; two floors of the same tower less than 1 m apart get the same name.
- [ ] AD_CAKE.cs: healing uses the absolute gap, so overlaps (floor starting below the top of the one beneath) are also snapped and the floor is made taller or shorter; the description only mentions gaps.
- [ ] AD_CAKE.cs: floors with no section at the slice plane are dropped silently; a warning with the item index would help users find bad Breps.

## Advanced Export Views (`Components/AdvancedExportViews.cs`)

- [ ] AdvancedExportViews.cs: while a capture is pending, and after the last state finishes, the component returns without setting Current GH State, so that output goes empty between frames. If a Canvas State Manager reloads on any input change this can cause extra reloads; consider re-emitting the current state.
- [ ] AdvancedExportViews.cs: the 500 ms fixed delay is not tied to solution completion; heavy definitions may be captured before they finish updating.
- [ ] AdvancedExportViews.cs: if Views is fed from Create Named Views while its Run is False, the list is empty and this component exports ALL named views in the document.

## Analysis Dashboard (`Components/AnalysisDashboard.cs`)

- [ ] AnalysisDashboard.cs OnDrawForeground returns early when _colors.Count == 0, so a metrics-only payload never shows its metrics.

## Analysis Heatmap (`Components/AnalysisHeatmap.cs`)

- [ ] AnalysisHeatmap.cs InfoText: Cells is described as exact values, no interpolation, but with per-vertex input each face shows the mean of its vertex values.
- [ ] AnalysisHeatmap.cs InfoText ends with Method after Meerkat (T.-T. Tran, MIT); maintainer should confirm this attribution and add a proper reference or remove it.

## Area Converter (`Components/AreaConv.cs`)

- [ ] AreaConv.cs: the HUD reports Done before the conversion loop runs (cosmetic only, the timing excludes the work).

## Assign Layer Materials (`Components/LayerMat.cs`)

- [ ] LayerMat.cs: Instructions_Out (output 0) already plays the role of an Info port with a fixed text; consider renaming or replacing it with Info for consistency.

## Auto List Item (`Components/AutoListItem.cs`)

- [ ] AutoListItem.cs: when several branches with different lengths are processed, the connected slider maximum is reset on each branch and ends at the last branch length.

## Auto-Grade Road (`Components/AutoGrade.cs`)

- [ ] AutoGrade.cs smoothing: the code comment says the moving average is guaranteed to keep the max slope, but that only holds in the interior. Near the ends the window is truncated and the end level is re-pinned after each pass, so on a ramp already at Max Slope the step next to each end can become up to about (window+1)/2 times steeper (window = steps/20, so roads longer than about 40 steps). The HUD Max Grade shows it, but the output can exceed Max Slope.
- [ ] AutoGrade.cs: the interpolated 3D Road (degree 3) can overshoot between points, so its true max grade can differ from the reported value, which is measured on the point profile.
- [ ] AutoGrade.cs: Resolution <= 0 is not guarded (division by zero gives a huge or invalid step count).

## Auto-Phase Assigner by Z (`Components/AutoPhase.cs`)

- [ ] AutoPhase.cs: Phase User Text is written straight to the Rhino objects with CommitChanges and no undo record; a misclick on Run overwrites existing phases without a way back.
- [ ] AutoPhase.cs: tower ids such as Podium_A and podium-B are all treated as one podium sequence per building, ordered by Z only; confirm this is intended.

## BIM Attribute Serializer (`Components/BimJson.cs`)

- [ ] BimJson.cs: double.TryParse / int.TryParse for FloorHeight, Floors and Phase set the value to 0 when parsing fails, instead of keeping the defaults 3.5 / 1 / 0. A typo gives a zero-height block.
- [ ] BimJson.cs: FloorHeight is parsed with the current culture. On a Spanish or other comma-decimal Windows locale, 3.5 is read as 35 (dot = thousands separator). Use CultureInfo.InvariantCulture.
- [ ] BimJson.cs: the Refresh input is read but never used; it only works because any input change re-solves.
- [ ] BimJson.cs: an arc segment for which TryGetArc fails is silently dropped from boundary_segments.
- [ ] BimJson.cs: PushToRhino is auto-wired to a Toggle, not a Button as its description says; while True it moves objects on every solve (idempotent, but surprising).

## BIM Key Initializer (`Components/InitKeys.cs`)

- [ ] InitKeys.cs: description says it works on referenced curves, but any Rhino object type gets the keys.

## BranchSizeSplit (`Components/BranchSizeSplit.cs`)

- [ ] BranchSizeSplit.cs: MinItems and MaxItems are number inputs that are silently truncated; the auto-wired sliders (0 to 3, default 1.5) give fractional values that become 1. Integer inputs or integer sliders would be clearer.

## Canvas State Manager (`Components/CanvasStateManager.cs`)

- [ ] CanvasStateManager.cs: the Import JSON input (index 5) is registered but never read in SolveInstance, so merging states through that input does nothing; only the Paste States from Clipboard menu item imports.

## Canvas Style (`Components/CanvasStyle.cs`)

- [ ] CanvasStyle.cs: GH_Skin is global and the change persists after the component or file is closed; there is no way to revert to the user's previous colours. Worth stating in the description.

## Canvas Usage Info (`Components/CanvasUsageInfo.cs`)

- [ ] CanvasUsageInfo.cs: the text says Last saved by but shows the Windows user name and time of the click, not the last save.
- [ ] CanvasUsageInfo.cs: Total Components counts every non-group object, including the two panels this tool creates, so the number grows by 2 after the first run.

## Change Pulse (`Components/ChangePulse.cs`)

- [ ] ChangePulse.cs: Data has item access but the previous value is one field, so a list input compares each item with the previous item and pulses on almost every solution.
- [ ] ChangePulse.cs: a pulse also fires on the first solution after opening the file, which can trigger downstream saves or exports unintentionally.

## Color Palette (`Components/ColorPalette.cs`)

- [ ] ColorPalette.cs: Count = 1 is treated like 0 (returns all stops), which is not stated in the input description.

## Conditional Fillet (`Components/CondFillet.cs`)

- [ ] CondFillet.cs: threshold_pct is described as a % but used as a fraction and capped at 0.49; the auto-wired slider (0 to 3, default 1.5) is therefore always clamped to 0.49, and a user entering 30 for 30 % gets 49 %.
- [ ] CondFillet.cs: if CreateFilletCurves fails at a corner, the corner stays sharp but radii still reports the chosen radius.
- [ ] CondFillet.cs: when the joined result has several pieces, a PolyCurve is built by appending the parts in order without checking continuity.

## Contact Enzyme (`Components/ContactEnzyme.cs`)

- [ ] ContactEnzyme.cs: the email body uses CurrentMessage, which is the HUD text built by Hud.Build (nickname, timing line, separator and the message), not the raw Message input, so the email contains HUD formatting.

## Create Named Views (`Components/CreateNamedViews.cs`)

- [ ] CreateNamedViews.cs: View Names is empty whenever Run is False. Wired into Advanced Export Views, an empty Views list means export ALL named views, so turning Run off between runs silently changes what gets exported. Consider listing the names regardless of Run.
- [ ] CreateNamedViews.cs: views are regenerated on every solution while Run is True (a toggle left on rewrites them on each upstream change).

## CurtainWallSystem (`Components/CWS.cs`)

- [ ] CWS.cs: the description says detailed LOD 300, but without profile curves members are zero-thickness fin surfaces and glass is a single unsplit surface.
- [ ] CWS.cs FindCurvatureTransitions: every NURBS span end and every sample with more than 10 % curvature change becomes a corner mullion, so freeform curves get many unwanted corner mullions.
- [ ] CWS.cs: for closed base curves the end point equals the start point, so a second corner mullion is likely added at the seam (only an exact Equals check on the last discontinuity guards it).
- [ ] CWS.cs: mullion positions use PointAtNormalizedLength but the tangent is taken at a parameter mapped linearly from the same fraction; on non-uniformly parameterised curved segments the mullion orientation can be wrong.
- [ ] CWS.cs: RhinoDoc.ActiveDoc is dereferenced without a null check (ModelAbsoluteTolerance), which fails in headless or Rhino.Compute use.
- [ ] CWS.cs: Heights with a non-positive value raise an Error but processing continues for the other curves.

## Curve Segment Dispatcher (`Components/SegDisp.cs`)

- [ ] SegDisp.cs: free-form segments are silently dropped, so the description (explodes curves into Lines and Arcs) can lose geometry without warning; add a warning or a third output.
- [ ] SegDisp.cs: text dots are output wrapped in GH_ObjectWrapper inside a generic tree mixed with curves; check they preview and bake as expected.

## CWProfile (`Components/CWP.cs`)

- [ ] CWP.cs: the HUD reports Done before input validation, so an invalid A, B or R still shows Done next to the error.

## CWT (`Components/CWT.cs`)

- [ ] CWT.cs: name, nickname, description and all port descriptions are just the port names (CWT, curves, modSize...). Users cannot tell what it does; a proper description is needed.
- [ ] CWT.cs: outward direction is tested against the centroid. On concave plans (L, U, courtyard shapes) parts of the outline face the centroid, so fins there point inwards. Using the curve orientation (ClosedCurveOrientation) would be robust.
- [ ] CWT.cs: modSize <= 0 is not validated (length / 0 gives an undefined int cast that happens to fall back to 4 divisions).
- [ ] CWT.cs: curveIndex is incremented but never used.

## Data Visualizer (`Components/DataVisualizer.cs`)

- [ ] DataVisualizer.cs: the Info text calls it LEAP DATA VISUALIZER and refers to LEAP components and ecological metrics; this looks copied from another plugin.
- [ ] DataVisualizer.cs: Bar Thickness is used as the hexagon circumradius, so the bar is about 2 × W wide; the input name suggests full thickness.
- [ ] DataVisualizer.cs: min and max are taken over the whole Values list, including values beyond the number of points, which are then not drawn.
- [ ] DataVisualizer.cs: the joined mesh has no normals computed (no Normals.ComputeNormals), which can give flat or wrong shading.

## Divide Target Length by Segment (`Components/DivideTargetSegment.cs`)

- [ ] DivideTargetSegment.cs: DuplicateSegments only splits polycurves and polylines; a single NURBS curve with kinks (for example degree 1) is not split, so the description's claim that it respects kinks holds only for those types. Curve.GetNextDiscontinuity with C1 continuity would cover all curves.

## Document Size to Pixels (`Components/PaperSizeToPixels.cs`)

- [ ] PaperSizeToPixels.cs: screen formats are multiplied by DPI / 72, so with the default DPI of 300 a 16:9 FHD output becomes 8000 × 4500 instead of 1920 × 1080. Screen formats should probably ignore DPI or the default should not apply to them.

## Elevation Label (`Components/ElevationLabel.cs`)

- [ ] ElevationLabel.cs: the Info output (index 3) is only set inside the branch where Bake is True and the bake layer has just been created, so Info is empty in normal use. Move it out of that branch.
- [ ] ElevationLabel.cs Info text claims it extracts sample points across the terrain and shows height above sea level; it only labels the given points with their model Z.
- [ ] ElevationLabel.cs: all setting inputs are tree access but only item 0 of branch 0 is read, so per-branch or per-point settings are ignored.
- [ ] ElevationLabel.cs: a nested BakeLayer path such as A::B is created with Layer.Name = A::B, which Rhino rejects, so nothing is baked; and with Bake on a toggle and no BakeName every solution bakes duplicates.

## Enzyme Heliodon (`Components/Heliodon.cs`)

- [ ] Heliodon.cs: description says annual sun vectors, but only the 21st of each selected month is sampled.
- [ ] Heliodon.cs: if the Hours start is not a whole or half hour (for example 8.25) the 0.5 h steps never land on whole hours and no Sun Vectors are output.
- [ ] Heliodon.cs: Months is read as Interval Min to Max, so a winter range such as 11 to 2 becomes February to November; wrap-around is not possible.
- [ ] Heliodon.cs: no North input, unlike Fast CFD and the wind tools; sites with rotated models get wrong sun directions.

## Export Named Views (`Components/ExportViews.cs`)

- [ ] ExportViews.cs: auto-wire connects a True toggle to Transparent, which only works for PNG; JPG and BMP exports with that default may get a black background.

## Facade JSON Parser (`Components/FacadeParser.cs`)

- [ ] FacadeParser.cs: branch indices count only the buildings and programs that pass the filters, so paths change when filters change; downstream trees matched by path can shift.
- [ ] FacadeParser.cs: when a floor has no BoundsClosed, a null curve is appended to BoundsClosed, so the branch carries nulls.
- [ ] FacadeParser.cs: question for the maintainer: which tool writes this JSON schema? It differs from the BIM_JSON output (buildings / blocks), and the comments refer to a Python version.

## Facade Module Distributor (`Components/FacadeDist.cs`)

- [ ] FacadeDist.cs: downloads and executes Python code from a public GitHub test repository without any integrity check; anyone with write access to that repo can run arbitrary code on users' machines. Security risk for a distributed plugin.
- [ ] FacadeDist.cs: the method cannot be documented from this repository because the geometry is in the remote facade_modules.py, which is not in the repo.
- [ ] FacadeDist.cs: all exceptions from reading or running the script are reported as Compile error, hiding the real Python error.
- [ ] FacadeDist.cs: the description mentions hot-reloading, but the file is only re-checked when the component solves again; nothing watches the file.
- [ ] FacadeDist.cs: WebClient is obsolete in .NET 6 and later (Rhino 8).

## Facade Module: Balcony (`Components/ModBalcony.cs`)

- [ ] ModBalcony.cs: walls are made with Extrusion.Create on single segments and bay curves; for a planar curve RhinoCommon extrudes along the curve plane normal, so please confirm that line segments and clockwise outlines always extrude towards +Z.
- [ ] ModBalcony.cs: only offsets[0] is used; when Offset returns several pieces the balcony is partial without a warning.
- [ ] ModBalcony.cs: description mentions intelligent corners, but corners are simply segment ends trimmed by CornerOffset or short segments when SegLength is on.

## Facade Module: Frames (`Components/ModFrames.cs`)

- [ ] ModFrames.cs: Extrusion.Create extrudes along the normal of the curve's own plane. Column profiles are forced counter-clockwise for this reason, but the beam side faces (Extrusion.Create on bIn and bOut), sills, glass, headers and fins are not. Check that clockwise boundaries and straight segments always extrude upwards; if not, beams fail to join into solids and panels go below the floor.
- [ ] ModFrames.cs: for open curves ClosedCurveOrientation is called, which is meant for closed curves; the result is probably Undefined, so FlipDir alone decides the side.
- [ ] ModFrames.cs: the base beam is added for index 0 of each branch, not for the lowest storey; if the branch is not sorted by level the base beam lands on the wrong floor.
- [ ] ModFrames.cs: input names are confusing: BeamWidth is the vertical beam height and BeamDepth the horizontal depth (offset). The description's deep structural exoskeleton wording does not say this.

## Facade Module: Rhythmic Balconies (`Components/ModRhythmBalc.cs`)

- [ ] ModRhythmBalc.cs: wrap-around of the merge logic on closed curves only applies when there is a single middle piece (middleCrvs.Count == 1), i.e. a single-segment closed curve such as a circle. On closed polylines the first and last bays never merge.
- [ ] ModRhythmBalc.cs: same Extrusion.Create concern as Mod_Fins: extrusion direction follows the curve plane from TryGetPlane, not explicit world Z.
- [ ] ModRhythmBalc.cs: the description mentions fast math, but areas are approximations (bay length × Depth), wrong for curved bays.

## Facade Module: Spandrels (`Components/ModSpandrel.cs`)

- [ ] ModSpandrel.cs: Heights is optional, so a missing Heights tree silently uses 4.0 m for every floor; a warning would help.

## Facade Module: Storefront (`Components/ModRetail.cs`)

- [ ] ModRetail.cs: all input and output descriptions are placeholders (Input double, Input string, Output Brep), so users get no guidance.
- [ ] ModRetail.cs: TransomHeight defaults to 0 when unwired, which turns the whole height into header band with no glass and no mullions.
- [ ] ModRetail.cs: bands and fins use Extrusion.Create(curve, height, false), which extrudes along the curve's own plane normal. For straight bay lines and fin lines that plane is not unique, and for arcs the sign follows the arc orientation, so the extrusion may not point up world Z. Worth verifying on curved and clockwise bounds.
- [ ] ModRetail.cs: the description promises intelligent canopies and structure, but canopies are flat offsets and columns are lines at every mullion.

## Facade Module: Vertical Fins (`Components/ModFins.cs`)

- [ ] ModFins.cs: Extrusion.Create extrudes along the normal of the plane Rhino finds for each curve (TryGetPlane), not along world Z. For horizontal lines this is normally +Z, but for closed curves the normal can depend on orientation, and a clockwise outline may extrude downwards. Surface.CreateExtrusion with an explicit Z vector would make it robust; please confirm.
- [ ] ModFins.cs: VertPattern is indexed by the item index within a branch, so the floor numbering depends on how the Bounds tree is structured upstream.

## Fast CFD (`Components/FastCFD.cs`)

- [ ] FastCFD.cs: the advection time step dt is already in seconds (velocities are in cells per second), but diffusion and friction use dt_sec = dt × CellSize / wind speed, a different time scale. Viscosity and Friction effects therefore change with CellSize and wind speed, contrary to the Info claim of grid-independent physics.
- [ ] FastCFD.cs: terrainSlopeFactor is only applied to the initial field and never during the time steps, so the topographic speed-up promised in Info is largely washed out once the domain is flushed.
- [ ] FastCFD.cs: comment cites Jackson and Hunt with 1 + 0.6 × slope, the code uses 1 + 0.5 × slope; neither is the Jackson-Hunt speed-up formula.
- [ ] FastCFD.cs: the convergence check compares u with u0, which after the diffusion swap holds the post-advection field, not the previous step; it measures one sub-step change. When minDomainFlushes exceeds 250 the check never runs.
- [ ] FastCFD.cs: the Friction list is indexed (i − 1) × rows + (j − 1), column by column from the minimum X; this order is not documented, so per-cell friction lists are easy to misalign.
- [ ] FastCFD.cs: Lawson classes use the heuristic gust speed while ComfortThreshold uses the mean speed, so the two comfort figures in Info and the dashboard are not comparable.

## Fast Noise Analyzer (`Components/FastNoiseAnalyzer.cs`)

- [ ] FastNoiseAnalyzer.cs BarrierAttenuation: Dz = 10 log10(3 + 20 N) with N = 2δ/λ equals ISO 9613-2 eq. 14 with C2 = 40 (40 δ/λ). The usual ISO value is C2 = 20 (3 + 10 N), so screening is up to about 3 dB stronger than ISO before the 20/25 dB cap. Info calls it ISO / Maekawa; please confirm which is intended.
- [ ] FastNoiseAnalyzer.cs: ComponentGuid 11112222-3333-4444-5555-666677778888 looks hand-typed; a placeholder GUID risks clashing with another plug-in. Changing it now would break saved files, so at least check it is unique.
- [ ] FastNoiseAnalyzer.cs: the Run = false branch inside Solve (BuildInfo(null), Hud.Off) is never reached because UsesRunGate makes the base class skip Solve and replay cached outputs.
- [ ] FastNoiseAnalyzer.cs: colours are scaled to each run's min and max, so two options cannot be compared by colour; a fixed dB range input would help.

## Fillet Rule Configurator (`Components/FilletRules.cs`)

- [ ] FilletRules.cs: rules beyond the shortest of RuleTypes, RuleMatches and RuleRadii are dropped without a warning, and RuleTypes are not checked against Tower, Program and Building.

## Flow Accumulation Heatmap (`Components/FlowHeat.cs`)

- [ ] FlowHeat.cs AutoWireDefaultInputs: the VisualScale slider is created with min 1.5, max 3.0 and value 1.0, outside its own range.
- [ ] FlowHeat.cs: non-polyline curves (for example interpolated flow lines) are ignored without a warning, although they are counted in Paths Evaluated.

## Global Flood Engine (`Components/GlobalFloodEngine.cs`)

- [ ] GlobalFloodEngine.cs: water is stored as depth per vertex and half the level difference is exchanged without weighting by vertex area, so volume is not conserved on irregular meshes (e.g. Delaunay terrain).
- [ ] GlobalFloodEngine.cs: the Info text claims exact water depths and retention pond volumes, but no volume is computed or output, and depths depend on Iterations and mesh resolution.
- [ ] GlobalFloodEngine.cs: the early stop rarely triggers because any wet vertex above a lower neighbour keeps moving water; runtime is normally the full Iterations.
- [ ] GlobalFloodEngine.cs: the Info string is static but built by concatenation into the local variable info and set with DA.SetData(3, info).

## GroupByKey (`Components/GroupByKey.cs`)

- [ ] GroupByKey.cs: with a tree on the inputs the component runs once per branch, but every run writes branch paths 0, 1, 2... so groups from different input branches are merged into the same output branches.
- [ ] GroupByKey.cs: a null key would make the Dictionary throw (ArgumentNullException) if Grasshopper passes nulls in the list.

## Higrothermal Comfort (`Components/ThermalComfortAnalyzer.cs`)

- [ ] ThermalComfortAnalyzer.cs: name and title spell Higrothermal; the usual spelling is Hygrothermal.
- [ ] ThermalComfortAnalyzer.cs: the Steadman AT formula expects wind speed at 10 m height; pedestrian-level CFD speeds are lower, so AT is biased warm compared with the published scale. Worth stating or converting.
- [ ] ThermalComfortAnalyzer.cs: when VelocityValues and TagPoints counts differ, nothing is computed and the HUD reports Done with 0 best / 0 worst instead of a warning.
- [ ] ThermalComfortAnalyzer.cs: the ring search stops at the first ring with any candidate, which is not always the true nearest tag point (a closer one may sit in the next ring).

## JSON Metadata Inspector (`Components/JsonInspect.cs`)

- [ ] JsonInspect.cs: a non-integer floor_index (for example 2.5 or a text value) throws and the whole inspection fails with a JSON error.

## JSON MP Engine V2 (`Components/MasterplanEngine.cs`)

- [ ] MasterplanEngine.cs GetProgramColor: the fallback colour uses new Random(string.GetHashCode()). On .NET Core (Rhino 8 default) string hash codes change per process, so unpaletted programs change colour every session. Use a stable hash such as a simple FNV or MD5 of the name.
- [ ] MasterplanEngine.cs facades: in towers without podium in the id, a block's own base level is not added to master_slabs, so for its first floor the exterior test compares against the outline of the block below. Edges of a setback upper block, or of every floor of a one-floor-per-block payload such as AD_ARCHICAD's, that do not lie on the lower outline are dropped from BoundsExt. Check with a setback tower.
- [ ] MasterplanEngine.cs MassingBlock: floor areas use AreaMassProperties of the boundary curve; an open curve gives null and the area silently becomes 0.
- [ ] MasterplanEngine.cs: the description (high-performance topological coordinator) does not say what it computes; suggest describing slab, roof, railing, facade and area outputs.

## Keyline Engine (`Components/KeylinePattern.cs`)

- [ ] KeylinePattern.cs: the Info text says the offsets create off-contour lines with a constant fall toward the ridges. The code only offsets the guide by a fixed plan distance and drapes it; the resulting fall is whatever the terrain gives, and it is not computed or reported.
- [ ] KeylinePattern.cs: Count is per side, so the output holds 2 × Count + 1 curves per guide (more if offsets split); the guide itself is included.

## Keypoint Engine (`Components/KeypointFinder.cs`)

- [ ] KeypointFinder.cs Info: says it computes the second derivative to isolate the exact inflection point. The code takes the largest drop between consecutive smoothed segment slopes (a discrete maximum concave break), which is not a zero crossing of curvature. The Info text overstates the method.
- [ ] KeypointFinder.cs: if a stream curve runs uphill, slopes are negative and the tool returns a flat-to-steep point instead; direction is not checked or corrected.
- [ ] KeypointFinder.cs: a keypoint is returned even when no slope decrease exists (maxChange <= 0).
- [ ] KeypointFinder.cs: ComponentGuid AA112233-4455-6677-8899-AABBCCDDEEFF looks hand-typed; check it is unique.

## Legend Geometry (`Components/LegendGeometry.cs`)

- [ ] LegendGeometry.cs: legend Type Binary (Road Profile and 3D Road Slope) and Discrete (Terrain Slope binary mode, Keyline) are not handled, so those payloads draw only the title.
- [ ] LegendGeometry.cs: Gradient legends always use Labels[0] and Labels[1] as the end labels, even when more labels are supplied (the maximum would be Labels[last]).
- [ ] LegendGeometry.cs: Info text says it bakes the legend into the Rhino scene and reads Slope, Height, Flow; it only previews and outputs geometry, and reads any compliant payload.
- [ ] LegendGeometry.cs: InterpolateColor is unused dead code.

## Map to Gradient (`Components/MapToGradient.cs`)

- [ ] MapToGradient.cs: a reversed Source Domain (for example 10 to 0, to flip the gradient) gives a negative Length, so every t is clamped to 0 and all values get the first colour.
- [ ] MapToGradient.cs: a NaN value gives an invalid colour index and Color.FromArgb throws.

## Masses JSON Parser (`Components/MassParser.cs`)

- [ ] MassParser.cs: a boundary made of a single segment is returned without MakeClosed; an open polyline then gives area 0 and Extrusion.Create may fail, so Volumes and Areas silently miss that block while Heights, Colors and Labels still have it.
- [ ] MassParser.cs: Extrusion.Create extrudes along the curve plane normal; a clockwise boundary may extrude downwards from true_z. Worth checking orientation (or using a signed height) with clockwise test data.
- [ ] MassParser.cs: block paths use the original block index, so filtered output trees have gaps; building index only counts matching buildings.

## Master JSON Config Builder (`Components/ConfigJson.cs`)

- [ ] ConfigJson.cs: the JSON_Palette description says OOP Engine; the consumer is JSON MP Engine V2 (ColorPalette input).

## Masterplan Clearance Engine (`Components/Clearance.cs`)

- [ ] Clearance.cs: Delaunay edges are read with TopologyEdges.GetTopologyVertices and the topology vertex indices are used directly as indices into validIndices; topology vertex order is not guaranteed to match mesh vertex (node) order. Safer to map through TopologyVertices.MeshVertexIndices.
- [ ] Clearance.cs: no LabelPlane is added for a zero-length clearance line (touching outlines), so the LabelPlanes tree can fall out of step with Lines, Distances and Categories.
- [ ] Clearance.cs: description says topological or proximity clearances between tower outlines, but the neighbour search uses bounding-box centres only.

## Masterplan Dashboard (`Components/Dash.cs`)

- [ ] Dash.cs: auto-wire puts a 0 to 2 slider on Target Global, which is an area target (typically thousands of m²); the slider range is unusable.
- [ ] Dash.cs: Transparency is applied as background opacity (1 = opaque), the opposite of its name.
- [ ] Dash.cs: numbers use the current culture (N2), so decimal and thousands separators change with the user's locale.

## Microclimate Comfort (`Components/MicroclimateComfort.cs`)

- [ ] MicroclimateComfort.cs CalculateUTCI: the operational UTCI polynomial (Bröde et al. 2012) has 210 terms; the code has 98 and stops at the term 1.00601257e-6 × Ta² × va × Pa (va² in the source). All higher humidity terms (Pa², Pa³... and Tmrt-Ta × Pa cross terms) are missing, so results labelled Precise UTCI are wrong, most in hot humid conditions. The full coefficient set should be ported.
- [ ] MicroclimateComfort.cs: UTCI is only valid for Tmrt - Ta between -30 and +70 °C and va 0.5-17 m/s; the code clamps va to 30 m/s and does not clamp Tmrt - Ta.
- [ ] MicroclimateComfort.cs: the UTCI scale has extreme heat stress above 46 °C and extreme cold stress below -40 °C; these categories are missing.
- [ ] MicroclimateComfort.cs: the description calls method 1 Precise, but Tmrt is a fixed +15 °C × Solar Factor guess, which dominates the error; worth saying in the description.

## Noise Emitter From Curves (`Components/NoiseEmitterFromCurves.cs`)

- [ ] NoiseEmitterFromCurves.cs: the calibration is only valid if the downstream analyser uses pure spherical spreading (Lp1 − 20 log10 r) with energetic summation; if the analyser adds air or ground absorption the reference level is not reproduced. Worth stating which analyser model it pairs with.

## Pixel Gradient (`Components/ColorMeshTiles.cs`)

- [ ] ColorMeshTiles.cs: bake runs on every solution while bake_trigger is true; with a toggle instead of a button it re-bakes on each upstream change, and with an empty bake_name earlier bakes are never deleted, so duplicates pile up.
- [ ] ColorMeshTiles.cs: if gradient_colors contains the same colour twice, both list positions share one count and one tag, so the HUD tile counts double-count that colour.
- [ ] ColorMeshTiles.cs: description says BIM-ready idempotent engine and Mega-Meshes; idempotent only holds when bake_name is set. Layer names use a HexFacade_ prefix even for non-hexagonal grids.
- [ ] ColorMeshTiles.cs: the fan apex is the bounding-box centre, not the polygon centroid; concave cells can produce overlapping or flipped triangles.

## Pixel Image (`Components/PixelatedSurface.cs`)

- [ ] PixelatedSurface.cs: tile counts in panel_tags, the HUD and the bake layer names count mesh faces, not cells (each cell has one triangle per edge), so a hexagon cell counts 6. The code comment admits it. Count cells per colour instead.
- [ ] PixelatedSurface.cs bake: branch_tags[j] is matched to branch_meshes[j], but tags are listed per palette colour (including empty ones) while meshes are listed only for non-empty colour buckets, so tags, layers and materials go to the wrong mesh when any palette colour has no tiles.
- [ ] PixelatedSurface.cs bake: layer names include the tile count (HexFacade_Tile1:120), so every change in counts creates new layers; the colon may also be rejected in a layer name. Check.
- [ ] PixelatedSurface.cs: new Bitmap(path) keeps the image file locked and the cache does not reload it when the file changes on disk under the same path.
- [ ] PixelatedSurface.cs: Bake is a level trigger; with a Toggle left on and no Bake Name, every solve bakes another copy.

## Point Probe (`Components/PointProbe.cs`)

- [ ] PointProbe.cs: if a mesh has as many vertices as faces, a branch is always read as per-vertex; per-face values would then be misread silently.

## Polyline Healer (`Components/PHeal.cs`)

- [ ] PHeal.cs: the Tolerance input is an extension length, not a tolerance or factor as named; it has no default, so the component does nothing until it is wired (the auto-wire slider sets 1.5).
- [ ] PHeal.cs: description says it creates boolean regions; it returns only the largest unioned outline, so multi-region inputs lose all but one region.

## Procedural Road Generator (`Components/RoadGenerator.cs`)

- [ ] RoadGenerator.cs: outputs Contours (7) and Main Contours (8) are registered but never set.
- [ ] RoadGenerator.cs: Colorize is described as colouring the terrain and volumes, but only the cut and fill meshes are coloured; the rebuilt terrain loses any vertex colours.
- [ ] RoadGenerator.cs: total cut and fill volumes are only shown in the HUD message; there are no numeric outputs.
- [ ] RoadGenerator.cs: pillar spacing uses length × i / station count (not the true arc length of the station), so spacing is slightly off.
- [ ] RoadGenerator.cs: the tangent un-flip (reverse if the dot product with the previous tangent is negative) also reverses genuine turns sharper than 90° between stations, e.g. hairpins with a coarse Subdivide.
- [ ] RoadGenerator.cs: the fixed 150 m triangle edge limit and 10 000 m ray offset assume metres; in millimetre models the terrain rebuild will delete most triangles.

## Railing JSON Parser (`Components/RailParser.cs`)

- [ ] RailParser.cs: DeserializeCurve returns joined[0] only, so railings with disjoint pieces lose geometry silently.
- [ ] RailParser.cs: maintainer to confirm the engine writes railing segments at Z relative to true_z; if they are absolute, curves are lifted twice.
- [ ] RailParser.cs: flexible mode uses substring matching, so a filter like A also matches TOWER_A and BLOCK_AB.

## Raindrop Flow Engine (`Components/WaterFlow.cs`)

- [ ] WaterFlow.cs: GridSpacing of 0 or less makes the seeding loops never end (Grasshopper hangs); auto-wire puts it on a slider from 0. Validate GridSpacing > 0 and StepSize > 0.
- [ ] WaterFlow.cs: MaxSteps is auto-wired to a 0 to 10 number slider (default 10), so with StepSize 5 paths stop after 50 m; a larger default (e.g. 500) would match the intent.
- [ ] WaterFlow.cs: DropPoints branch index is the index in the projected hit list, not in the seed grid, and ProjectPointsToMeshes can return more than one hit per seed on overhanging meshes.

## RandomTreeSplit (`Components/RandomTreeSplit.cs`)

- [ ] RandomTreeSplit.cs: the auto-wired sliders set pA = pB = 1.5, so the component errors immediately on placement (sum must be ≤ 1). Defaults like 0.3 and 0.3 on 0 to 1 sliders would work.
- [ ] RandomTreeSplit.cs: all input and output descriptions are just the names (pA, pB, seed, A, B, C) and the component description is RandomTreeSplit.

## Recipe JSON Builder (`Components/RecipeBuilder.cs`)

- [ ] RecipeBuilder.cs: the JSON is built by string concatenation and program names are not escaped, so a name containing a double quote or backslash produces invalid JSON. Use JsonConvert.SerializeObject.

## Report Metrics (`Components/ReportMetricsExport.cs`)

- [ ] ReportMetrics.Build (Utils/ReportMetrics.cs): flags kept from an existing file with Merge on keep their old rating and value even when this run updates the metric they rate; stale ratings can appear in the scorecard.
- [ ] ReportMetrics.Build: flag lookup takes the first metric with the key regardless of scope, so the same key at site and buffer scopes is ambiguous.

## Road Profile & Slope Unroller (`Components/RoadProfileUnroller.cs`)

- [ ] RoadProfileUnroller.cs and RoadSlope3DAnalyzer.cs: a vertical segment (H = 0, V > 0) gets slope 0 and is marked Compliant in degree and percent modes.
- [ ] RoadProfileUnroller.cs and RoadSlope3DAnalyzer.cs: in ratio mode a flat segment gives H / 0 = infinity in Slope Values, which also makes the HUD Avg and Min statistics infinite.
- [ ] RoadProfileUnroller.cs: in ratio mode the legend labels read <= threshold Ratio and > threshold Ratio, which is the opposite of the compliance rule (ratio ≥ threshold is compliant).
- [ ] RoadProfileUnroller.cs: AddedToDocument adds a Mode value list whenever input 3 has no source, including when an existing file is reopened with Mode left unwired.

## Road Slope Analyzer (`Components/RoadSlopeAnalyzer.cs`)

- [ ] RoadSlopeAnalyzer.cs: pieces are made by equal parameter steps (Domain.ParameterAt), not equal length, so on non-uniform curves piece lengths differ from Segment Size; actualSegmentSize is computed but unused.
- [ ] RoadSlopeAnalyzer.cs: in ratio mode a flat piece gives Δxy / 0 = infinity in Slope Values.
- [ ] RoadSlopeAnalyzer.cs: outputs are grouped only by compliant or non-compliant branch, so the link between a slope value and its curve and position along it is lost.
- [ ] RoadSlopeAnalyzer.cs: an unknown Threshold Mode (e.g. 3) gives slope 0 and every piece compliant without warning.
- [ ] RoadSlopeAnalyzer.cs: the Compliance Percentage description says compliant/non-compliant but the value is the compliant share only.

## Slab JSON Parser (`Components/SlabParser.cs`)

- [ ] SlabParser.cs DeserializeCurve: when segments do not join into one curve, joined[0] is returned and the other pieces are dropped without a warning.

## Streamlines (`Components/Streamlines.cs`)

- [ ] Streamlines.cs: Info text says lines stop at buildings; that only holds for regular grids with missing cells (or scattered points with no neighbour inside SearchRadius).

## Surface Plane Finder (`Components/PlaneFinder.cs`)

- [ ] PlaneFinder.cs: for a trimmed Brep face the underlying (untrimmed) surface is used, so the origin is the centroid of the untrimmed surface, not of the visible face. Use AreaMassProperties of the BrepFace (or DuplicateFace) instead.
- [ ] PlaneFinder.cs: BrepFace.OrientationIsReversed is ignored, so the normal can point the wrong way relative to the Brep face.
- [ ] PlaneFinder.cs: a non-planar input only shows Surface is NOT planar in the HUD with no warning and empty outputs.

## Survey Outlier Cleaner (`Components/SurveyOutlierCleaner.cs`)

- [ ] SurveyOutlierCleaner.cs: invalid points are removed before processing, so Deviation, Score, Flags and Colors are aligned with the valid points only, not with the input list as the output descriptions say. Rarely matters, but Cull Pattern will misalign if any input point is invalid.
- [ ] SurveyOutlierCleaner.cs: the HUD shows Max off in m regardless of model units.

## Table to DataTree (`Components/ParseTable.cs`)

- [ ] ParseTable.cs SmartCast: double.TryParse uses the current culture. On a Spanish or other comma-decimal system 4.5 is read as 45 (dot as thousands separator) and 4,5 is split as two CSV cells. Use CultureInfo.InvariantCulture, or detect the decimal separator.
- [ ] ParseTable.cs SmartCast: (int)f overflows for whole numbers beyond about 2.1 billion; use long or keep them as numbers.

## Terrain Generator Pro (`Components/TerrainGeneratorPro.cs`)

- [ ] TerrainGeneratorPro.cs: the code defaults for PatternSizeXY / PatternHeightZ (500, 150, 30 and 1, 0.3, 0.05) differ from the auto-wired panels (150, 50, 20 and 1.0, 0.3, 0.1); documentation should name which applies.
- [ ] TerrainGeneratorPro.cs: the random hash uses sin(x × 12.9898 + ...) × 43758.5453 on absolute coordinates; at georeferenced (UTM-size) coordinates the hash loses precision and the noise degrades. Consider hashing coordinates relative to the boundary.
- [ ] TerrainGeneratorPro.cs: with Solid on, mesh.Weld(π) merges the wall and base vertices with the terrain edge vertices, so vertex colours can bleed between the base colour and the terrain colours along the edges.
- [ ] TerrainGeneratorPro.cs: MinHeight greater than MaxHeight is not checked and inverts the terrain; the HUD area is labelled m2 regardless of units.

## Terrain Grader (`Components/AdaptiveTerrainGrader.cs`)

- [ ] AdaptiveTerrainGrader.cs: cut and fill are measured against a re-triangulated copy of the whole terrain, so on irregular terrain resampling error outside the pads is counted as earthworks (only differences under 0.01 m are ignored). Consider measuring only inside the pad envelope or reusing the original vertices away from pads.
- [ ] AdaptiveTerrainGrader.cs: when no BoundaryCurves are given the Info, Contours and MainContours outputs are not set (early return).
- [ ] AdaptiveTerrainGrader.cs: contour levels and the 0.01 m and 0.05 m thresholds assume metres; the Contours output descriptions say 1m and 5m regardless of model units.
- [ ] AdaptiveTerrainGrader.cs: contours are generated only between the original terrain bounding box Z limits; a pad set above or below the existing terrain range would not be contoured fully.
- [ ] AdaptiveTerrainGrader.cs Info text mentions roads; the code only handles closed pad curves.

## Terrain Height (`Components/MeshHeightAnalysis.cs`)

- [ ] MeshHeightAnalysis.cs Read(): on file load it removes every output after index 9 and re-adds a generic Color Legend output at index 10, so saved definitions lose the Info output (11) and the text Dashboard Data output is replaced. Info is then never set because Params.Output.Count is 11.
- [ ] MeshHeightAnalysis.cs: GetNakedEdgePointStatus returns one flag per mesh vertex but is indexed with topology vertex indices, so AvoidBoundaries tests the wrong vertices on unwelded meshes.
- [ ] MeshHeightAnalysis.cs: each heatmap uses its own mesh's zMin and zMax, but the legend labels use the global min and max of all meshes; with several meshes the colours do not match the legend.
- [ ] MeshHeightAnalysis.cs: the default legend lists blue, cyan, lime, yellow, red, but the default heatmap ramp is cyan to yellow to red. A single custom colour also falls back to the default ramp.
- [ ] MeshHeightAnalysis.cs: the description says it unrolls sections and makes 3D and 2D labels; that code was removed (section and label structures are built but never output). The Instructions output is a one-line text.

## Terrain Mesher (`Components/TerrainMesher.cs`)

- [ ] TerrainMesher.cs: the Info text says breaklines are followed, but they are only densely sampled; long Delaunay triangles can still cross a breakline between samples (worth saying in the docs or adding a constrained triangulation).

## Terrain Sections (`Components/TerrainSections.cs`)

- [ ] TerrainSections.cs: the flat layout cursors are reset for every mesh, so with several meshes their flat sections are drawn on top of each other.
- [ ] TerrainSections.cs: output descriptions say X sections stack downwards (−Y) and Y sections leftwards (−X); the code stacks them upwards (+Y) and rightwards (+X) from the site bounding box.
- [ ] TerrainSections.cs: each bake adds a new Rhino group with the same name (doc.Groups.Add every time); with a toggle on Bake it re-bakes on every solution.
- [ ] TerrainSections.cs: Info is set inside the mesh loop, so it is missing when no valid mesh is input; SectionMetadata is only produced when LayoutFlat is on.

## Terrain Slope (`Components/SlopeTerrainPlus.cs`)

- [ ] SlopeTerrainPlus.cs: the metric named Total Area Over is a face count share, not an area share; on meshes with mixed face sizes it can differ a lot from the true area share.
- [ ] SlopeTerrainPlus.cs: gradient mode normalises per mesh (min to max of that mesh), so two meshes with different ranges get the same colour for different slopes, and the gradient legend has no slope values.
- [ ] SlopeTerrainPlus.cs: binary legend Type is Discrete, which Legend Geometry does not draw (title only).
- [ ] SlopeTerrainPlus.cs: Info text says faces are sorted; no sorting happens. Description ultra-fast ... safe UI automation is not a method description.

## Tile Pattern (`Components/TileGridGenerator.cs`)

- [ ] TileGridGenerator.cs: a tile with all corners and centre inside is classed as full even when the boundary cuts into it (concave boundaries, notches); the curve intersection test is computed but ignored in that case.
- [ ] TileGridGenerator.cs: for surfaces and Breps only joined[0] of the naked edges is used, which may be a hole loop rather than the outer edge, and holes are otherwise ignored.
- [ ] TileGridGenerator.cs: X Dim and Y Dim are not validated; negative values make the generation loops step backwards forever and hang Grasshopper, and very small values relative to the boundary create huge cell counts.
- [ ] TileGridGenerator.cs: an unrecognised Grid Type silently produces no tiles.
- [ ] TileGridGenerator.cs: for curve bases the grid X axis is whatever axis TryGetPlane returns, which is not guaranteed to be world X; worth documenting or aligning to world X by default.

## Topology Split Edge Classifier (`Components/TopologySplitEdgeClassifier.cs`)

- [ ] TopologySplitEdgeClassifier.cs: CurveCurve overlap events are ignored when splitting (only IsPoint events). Where two rooms share part of a collinear wall of different lengths, the longer segment is not split at the overlap ends; the midpoint test then marks the whole longer wall as internal (or misses it), so external portions can be lost.
- [ ] TopologySplitEdgeClassifier.cs: split parameters are kept only if more than Tolerance from the segment ends in parameter space, mixing curve parameters with model distance; for polylines (parameters 0..n) this is not a length.
- [ ] TopologySplitEdgeClassifier.cs: MetadataOut mixes internal and external entries in processing order, so its items do not line up with either ExternalEdges or InternalPartitions by index.
- [ ] TopologySplitEdgeClassifier.cs: output 0 Instructions_Out is leftover text from a C# script component (rename the inputs and outputs) and has no meaning in the compiled component; consider replacing it with an Info output.

## Turntable Camera (`Components/TurntableCamera.cs`)

- [ ] TurntableCamera.cs: if Directory is empty or cannot be created, Run does nothing and gives no warning.
- [ ] TurntableCamera.cs: RhinoDoc.ActiveDoc is used without a null check before reading Views.

## Urban Wind Vector Engine HT (Beta) (`Components/WindEngineHTVer.cs`)

- [ ] WindEngineHTVer.cs: grid indices are ix × vCount + iy (column by column), but default streamline seeds step by uCount × 2; this is not every other row as the comment says, and gives an irregular seed pattern that depends on the grid aspect ratio.
- [ ] WindEngineHTVer.cs: the gap rule takes only one closest point per building, so corridors inside a single U-shaped or courtyard building are never detected; the opposing wall must belong to another mesh.
- [ ] WindEngineHTVer.cs: for corner bending, when several buildings qualify the direction from the last one in the list overwrites earlier ones while their boosts still multiply.
- [ ] WindEngineHTVer.cs: description says it outputs a perfectly flat heatmap at a custom elevation, but DrapeHeatmap defaults to true (draped tiles).
- [ ] WindEngineHTVer.cs: Info text calls it physically-refined; the method is heuristic and should be described as a screening tool. The Reading 2016 venturi reference in the code comment could be cited once checked by the maintainer.

## Vector Arrow Generator (`Components/VArrow.cs`)

- [ ] VArrow.cs: in Double mode lines between 1.1 and about 2 × HeadLength pass the length check but the two heads overlap and the shaft is inverted, giving odd unions; the minimum length should be about 2.2 × HeadLength for Mode 2.

## Vertical Projection (`Components/VerticalProjection.cs`)

- [ ] VerticalProjection.cs: the above or below message uses the signed distance along the plane normal, not world Z, so with a downward-facing or tilted plane it reports above and projected downward for a point that is actually below.

## Wind Rose (`Components/WindRose.cs`)

- [ ] WindRose.cs: the Radius description says the busiest sector reaches it, but the scale is rounded up to the next ring step, so the busiest petal is usually shorter than Radius.
- [ ] WindRose.cs: the Direction output is the raw Selector angle, while Speed and Frequency belong to the whole sector around it; feeding Fast CFD with e.g. 30° on 16 sectors uses the NNE (22.5°) sector's statistics with a 30° direction.
