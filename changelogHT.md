# Changelog — HT

Running log of changes made on `main` going forward. Intended to eventually be folded into `DEVELOPMENT_GUIDE.md` / `docs/` once reviewed.

## Unreleased

**Earthworks and roads (methods seen in Open Infra Studio, OpenAEC Foundation; GPL, so written from scratch from the published methods):**
- New **Road Corridor** (Masterplan): a cross-section template (lanes, kerbs with upstands, cycle tracks, footways, verges, shoulders, ditches only in cut, pavement layers) swept along a 3D centreline; cut / fill slopes daylight to the terrain; superelevation on curves with runoff; earthworks measured to subgrade (average end area); mass haul with balance points, average haul and volume beyond free haul; pavement layer volumes, surface areas, kerb length; design surface coloured by part, subgrade, feature / daylight lines, sections, mass haul diagram. Presets: residential street, urban street, rural road, cycle path, access track, or a JSON template.
- **Hydro-DEM**: depressions filled with Priority-Flood (Barnes, Lehman & Mulla 2014) before routing, so streams run through hollows to the terrain edge; new Fill Pits / Min Pond Depth inputs and Pond Depth, Ponds (water surface) and Catchment outputs; dashboard adds catchments, ponds, pond volume, max depth.
- New **Road Network** (Masterplan): solves the topology of road centrelines (split at every crossing, ends snapped within a tolerance) into nodes, edges and arms with metadata: node type (dead end, continuation, T, Y, crossroads, multi-arm), arm headings, priority (through) arms by hierarchy then straightness, corner angles, kerb-return radii by hierarchy, tangent points and junction mouths. Draws the 2D road plan before any 3D: kerb lines and part edges offset from each template with concentric kerb returns, mouth lines, junction carriageway outlines, hatchable strips per part, labels. Road template / hierarchy / name read from `road_template` / `road_hierarchy` / `road_name` User Text on the referenced curves (override lists for Grasshopper-made centrelines, then defaults; Attributes Used shows the source). Corridor Edges are trimmed between mouths for Auto-Grade Road / Road Corridor. Node / Edge / Network JSON for the 3D junctions to come; Dashboard Data with junction counts, dead ends, skewed junctions and junction density.
- **Road Network** junction tags: points referenced from Rhino with `junction_type` (auto, priority, signals, roundabout, mini-roundabout, cul-de-sac), `junction_radius`, `junction_priority`, `junction_name` User Text, matched to the nearest node. Roundabouts (default R15, island R9.5; mini R8, island R2) and cul-de-sac bulbs (R9) drawn in 2D with entry fillets and concentric boundary lines; radius and through-road overrides for priority junctions. New Corridor Templates output (one template JSON per Corridor Edge) for Road Corridor, so 2D and 3D use the same street types.
- Rhino-free `Hydrology`, `Corridor` and `RoadNetwork` helpers with tests.
- **Method notes standard**: `Utils/MethodNotes.cs` holds each component's method, assumptions, simplifications, workflow and references; the component shows them in an **Info** output and `gen_docs.py` writes the same text into a "Method, assumptions and simplifications" section of its docs page (CI keeps them in sync, tests require all three parts). Applied to Sun Hours Analysis and Site Fetch (Info appended as last output), Hydro-DEM (Info text replaced), Road Corridor and Road Network. Water Analysis Guide updated for the Hydro-DEM pit filling.
- Road Network: Y junction detection fixed (three arms with no straight pair).

## 1.12.0

**Site context and porous trees (ideas adapted from VoxCity, Fujiwara et al. 2025, NUS / Takenaka, MIT):**
- New **Site Fetch**: lat/lon + size → Copernicus GLO-30 terrain, ESA WorldCover land cover (coloured mesh + class per cell), OSM buildings (height / levels / roof:levels / min_height, courtyards) as closed extrusions on the terrain plus one joined mesh for Context, and OSM trees as closed crowns. Local metres (UTM of the site centre at 0,0) with a Georeference output; Dashboard Data in Report Metrics format; disk cache; Run gate.
- **Sun Hours**: optional Trees / LAD / k inputs and Tree Shade % output. Crowns filter sun with Beer–Lambert (exp(-k·LAD·path)) instead of blocking it; per-crown LAD for species or season. Results unchanged with no Trees.
- Rhino-free helpers with tests: `GeoTiff` (COG range reads, DEFLATE, predictors), `GeoProj` (UTM), `SiteData` (sources, OSM parsing), `CanopyMath`.

## 1.11.0

**Analysis visualisation (ideas adapted from Meerkat, T.-T. Tran, MIT):**
- New **Analysis Heatmap**: one visualiser for any analysis mesh (CFD, noise, sun hours, comfort). Cells / Smooth / Contour / Classes modes; exact contour bands that line up with the isolines; masked smoothing (buildings excluded); round-number levels; isoline value labels that never overlap; peak labels; % area per band (`BandStats`) for reports; UTCI, Lawson (LDDC) and noise class schemes; geometry cached so label tweaks are instant.
- New **Analysis Difference**: option A − B on a diverging scale centred on zero, area-weighted % higher / lower / unchanged, and % area above a level in each option.
- New **Point Probe**: values at named points, interpolated in the face, several result sets at once, CSV table.
- New **Color Palette**: 32 palettes incl. colour-blind-safe Viridis / Cividis, Turbo and diverging sets; shared by all new components (`Utils/Palettes.cs`).
- **Fast CFD**: new VertexSpeeds (aligned with WindMesh, NaN in buildings) and BuildingMask outputs, so WindMesh plugs straight into the new components.
- **Streamlines** upgraded: regular grids (Fast CFD) are read directly with bilinear interpolation instead of a radius search per step; 4th-order Runge-Kutta steps; trace both ways from each seed; lines stop at calm zones, walls and closed loops; new outputs: speeds per vertex, speed-coloured tube mesh, legend JSON. Placed components keep their old ports and behaviour.
- **Legend Geometry**: gradients now use every colour stop (was only the first two); contour/class legends label every band.
- **Analysis Dashboard**: contour legends show the range of each band.
- New **Report Metrics**: writes one Deep Site Analysis module file (`04_metrics/<module>.json`: metrics, green/amber/red flags, maps, sources, plugin version) from any Dashboard Data plus hand-entered metrics; merges contributions from several definitions.
- Analysis Heatmap classes for the report: **Slope** bands (0-5 / 5-15 / 15-30 / >30 %) and **Flood depth** (0.1 / 0.3 / 0.5 m); its Dashboard Data now carries band areas.
- `Tests/FieldMath.Tests`: Rhino-free tests for the contour / isoline / tracing maths, run in CI.
