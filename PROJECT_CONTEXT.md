# Enzyme Grasshopper Plugin: Project Context

**Read this first.** It is the hand-over file for anyone, human or AI, picking up development: what the plugin is, how it is built and released, the standards every component follows, the decisions taken so far, the open issues and the next steps. Keep it current: when you take a decision, find a problem or finish a pipeline item, update the relevant section in the same pull request.

- Owner: Jorge Beneitez (JB), Enzyme APD. Authors in `manifest.yml`: Dushyant Basson, Jorge Beneitez, Hendriko Teguh.
- Repo: `ENZYME-APD/enzyme-grasshopper-plugin` (GitHub). Docs site: https://enzyme-apd.github.io/enzyme-grasshopper-plugin/
- Distribution: McNeel Yak package `Enzyme-Grasshopper-Plugin` (Rhino 8, Package Manager).
- Last updated: 2026-11-22.

Related files: `DEVELOPMENT_GUIDE.md` (detailed Grasshopper / CI quirks, HUD, base class, method notes rules), `changelogHT.md` (what changed per release), `generative_road_spec.md` (the target road pipeline), `docs/` (site).

---

## 1. What the plugin is

A toolkit of about 115 native C# Grasshopper components for Enzyme's work: urban design, masterplanning, site analysis and formation, facades. It replaces slow scripts and external tools with fast, documented components that feed one another and a Deep Site Analysis report.

| Family (Grasshopper tab Enzyme > ...) | Count | Examples |
|---|---|---|
| Site Analysis | 34 | Site Fetch, Sun Hours Analysis, Heliodon, Fast CFD, Fast Noise Analyzer, Global Flood Engine, Hydro-DEM, Terrain Slope, Analysis Heatmap / Difference / Dashboard, Report Metrics |
| Masterplan | 9 | Road Network, Road Corridor, Auto-Grade Road, Procedural Road Generator, road slope analysers, Masterplan Clearance Engine |
| JSON Masterplan (Beta) | 26 | JSON MP Engine V2, Master JSON Config Builder, Masses / Slab / Roof / Facade parsers, Archicad (Tapir) and massing adapters |
| Facade | 9 | Curtain Wall System, Facade Module Distributor, Pixel Image / Gradient, Tile Pattern |
| Curve, Data | 11 | Topology Split Edge Classifier, Polyline Healer, tree and list helpers |
| Utilities, Info | 24 | canvas tools, named views and exports, GIF compiler, gradients |

The Deep Site Analysis report (a separate project, "DVF collaboration") is fed by the analysis components through their **Dashboard Data** JSON and the **Report Metrics** component, which writes one `04_metrics/<module>.json` per report module.

---

## 2. Repository map

| Path | What |
|---|---|
| `Components/*.cs` | One Grasshopper component per file. |
| `Utils/*.cs` | Shared code. Rhino-free helpers (maths, file formats, algorithms) live here so they can be unit tested: `FieldMath`, `WindMath`, `VectorField`, `Palettes`, `ReportMetrics`, `CanopyMath`, `GeoTiff`, `GeoProj`, `SiteData`, `Hydrology`, `Corridor`, `RoadNetwork`, `MethodNotes`. Rhino-dependent helpers: `EnzymeComponent`, `Hud`, `AutoWireHelper`, `MeshPrep`. |
| `Tests/FieldMath.Tests` | Rhino-free test console (net8). Compiles selected `Utils/*.cs`; fixtures in `fixtures/`. Run in CI. |
| `Resources/` | 24 px PNG icons embedded in the .gha (`IconLoader.Load("Name.png")`, case-sensitive); only icons a component loads are kept. |
| `docs/` | GitHub Pages site: `index.html` cards, `components/<slug>.html` pages, guides (`Terrain_and_LEAP_Guide.md`, `Water_Analysis_Guide.md`). |
| `dev_scripts/gen_docs.py` | Syncs docs with the code (headers, port lists, cards, method notes). `--check` in CI. |
| `dev_scripts/yank_versions.py` | Yak clean-up (manual workflow). |
| `dev_scripts/get_icon.js`, `fetch_lucide.sh`, `script-env/` | Icon tool: Lucide SVG to 24 px PNG in `Resources/` (one-time `npm install` in `script-env`). |
| `dev_scripts/sync_galleries.py` | Adds image galleries from `docs/images/components/<page>/` to docs pages. |
| `LICENSE`, `NOTICE.md` | Enzyme licence (free use with attribution; selling needs permission) and third-party notices. |
| `REVIEW_FINDINGS.md` | Review checklist per component: fixed, open (owner decision / canvas test), not a bug. |
| `enzGhPlugin.csproj` | net48, `<Version>` = release switch; RhinoCommon / Grasshopper pinned to 8.0.23304.9001. |
| `manifest.yml` | Yak manifest. |
| `.github/workflows/` | `ci-check.yml`, `build.yml`, `deploy-docs.yml`, `cleanup-yak.yml`. |

---

## 3. Build, test, document, release

```bash
dotnet build enzGhPlugin.csproj -c Release          # builds bin/Release/net48/enzyme.gha (works on Linux too)
dotnet run --project Tests/FieldMath.Tests -c Release # Rhino-free tests, must print ALL PASS
python3 dev_scripts/gen_docs.py                       # sync docs; CI runs it with --check
```

- **CI Check** (every push / PR): compile + docs in sync + tests. Must be green before merging.
- **Release = version bump on `main`.** `build.yml` runs on every push to `main`; if tag `v<Version>` does not exist it builds, packages, publishes to Yak, creates the GitHub release and tag. No bump, no release. Yak rejects a version that already exists.
- **Docs deploy** when `docs/**` changes on `main`.
- **Branches:** work on a branch (`claude/<topic>` for AI sessions), open a PR, wait for green CI. A human (JB) merges; AI sessions cannot merge or push to `main`.
- **Rhino testing:** nothing in CI runs Rhino. Geometry code must be checked on a canvas before a release (see the open issues for what is still untested).
- **Placed components keep their old ports** after an update (Yak quirk): users must drop a fresh component to see new inputs / outputs. So on a **released** component, only **append** ports at the end; never reorder, rename or remove. Unreleased components may change freely.
- Rhino SDK pin: see `DEVELOPMENT_GUIDE.md` section 7 (raising it hides releases from older Rhino 8 installs).

---

## 4. Standards (every component)

1. **Base class:** inherit `Enzyme.Utils.EnzymeComponent`, implement `Solve(DA, sw)`. Exceptions become a HUD error. Analysis tools set `UsesRunGate => true` and have a `Run` input (false = replay cached outputs).
2. **HUD:** always through `Hud.Done / Cached / Waiting / Off / Error` (`DEVELOPMENT_GUIDE.md` section 5): NickName, time, then 2 to 6 metrics, invariant culture, SI units.
3. **Method notes:** every component's method, assumptions, simplifications, workflow and references are written once in `Utils/MethodNotes.cs`. The component shows them in an **Info** text output (`MethodNotes.Info("<Key>")`) and `gen_docs.py` writes the same text into its docs page. Tests fail if a note lacks any of the three core parts. Rules in `DEVELOPMENT_GUIDE.md` section 8. State defaults and numbers, name sources, say plainly where a result is approximate or must be checked against local standards.
4. **Dashboard Data** (analysis outputs): JSON with `Type` (Gradient, Blocks, Discrete, Metrics), `Title`, `Colors` [`{R,G,B}`], `Labels`, `Metrics` [`{Name, Value}`] and, where areas matter, `BandAreas` [`{Name, Area (m²), Pct}`]. Report Metrics and Analysis Dashboard read it.
5. **Units and frames:** metres, m², m³; Rhino model in metres. Site Fetch places the site centre at 0,0 in the UTM zone of the site (Georeference output gives EPSG and origin easting/northing; Y is grid north).
6. **Rhino-free core + tests:** put algorithms in `Utils/` without RhinoCommon and add checks to `Tests/FieldMath.Tests/Program.cs` (analytic cases, edge cases). Components only convert geometry and wire ports.
7. **Docs:** never hand-edit generated parts (header, port lists, method-notes block). Hand-written sections after the port grid are kept.
8. **Naming:** clear Grasshopper names and nicknames, sentence-case descriptions that say what the component does and how to use it; new icon for each new component.
9. **Commits / PRs:** descriptive messages (`feat:`, `fix:`, `docs:`, `build:`), update `changelogHT.md` under `Unreleased`, and this file when a decision, issue or pipeline item changes.

### Data conventions

| User Text key | On | Values |
|---|---|---|
| `road_template` | road centrelines | preset name (residential street, urban street, rural road, cycle path, access track) or the name of a custom template |
| `road_hierarchy` | road centrelines | main, secondary, local |
| `road_name` | road centrelines | free text (also used by `junction_priority`) |
| `junction_type` | points at junctions | auto, priority, signals, roundabout, mini-roundabout, cul-de-sac |
| `junction_radius` | points at junctions | kerb radius (priority) or outer radius (roundabout, cul-de-sac), m |
| `junction_priority` | points at junctions | `road_name` of the road that runs through |
| `junction_name` | points at junctions | free text |

Road templates are JSON (`RoadTemplate` in `Utils/Corridor.cs`): `name`, `cut_slope`, `fill_slope` (H:V), `max_daylight`, and `parts` (both sides) or `right` / `left`: each part `type` (lane, parking, shoulder, track, kerb, cycle, footway, verge, median, ditch), `width`, `slope` (%; negative falls away from the axis), `rise` (kerb upstand), `only_in` (cut / fill), `layers` [[material, thickness]].

### Third-party code and data

- **MIT / permissive code** (VoxCity, Meerkat): ideas and code may be adapted with attribution in comments and method notes.
- **GPL code** (Open Infra Studio, FreeCAD Road): **methods only**, never copy code; implement from the published method or paper. The plugin is not GPL.
- **Our licence** (`LICENSE`): anyone may use, modify and share the plugin for any purpose, including paid design work, with attribution; selling it or putting any of its code into a commercial software product or service needs written permission from Enzyme APD. Outputs (models, drawings, reports) are free of conditions. Source-available, not OSI open source. Any code ported from elsewhere must be MIT/BSD/Apache-compatible and listed in `NOTICE.md`.
- **Data:** OpenStreetMap is ODbL (attribution required on any output: Site Fetch's Sources output); Copernicus DEM and ESA WorldCover (CC BY 4.0) need credit. FABDEM is non-commercial (CC BY-NC-SA), so it is not used by default.

---

## 5. Road pipeline (target: `generative_road_spec.md`)

| Spec phase | Component(s) | Status |
|---|---|---|
| 1. 2D topological network, tagged edges and nodes | **Road Network** (topology, node / edge metadata, User Text tags, junction points) | Built (PR #2), untested in Rhino |
| 2. 2D drafting and JSON export | **Road Network** 2D plan (kerb lines, returns, mouths, hatch strips, labels), Node / Edge / Network JSON | Built (PR #2) |
| 3. Drape and feasibility | Road Slope Analyzer, Road Profile & Slope Unroller | Existing |
| 4. Auto-Grade (vertical alignment) | **Auto-Grade Road** (slope clamp + smoothing) | Existing; junction level pinning from the network not yet done |
| 5. Terrain grader / daylight | **Road Corridor** (template sections, daylight, earthworks to subgrade, mass haul); Terrain Grader | Corridor built (PR #2); stitching corridors into the terrain mesh not done |
| 6. Road and junction geometry | Road Corridor surfaces; **Road Junction** (3D patches) | Junctions planned |
| 7. Analytics | Road Corridor quantities and mass haul, Hydro-DEM (Priority-Flood), report metrics | Partly built |

Flow today: tagged centrelines (and junction points) in Rhino -> Road Network -> Corridor Edges -> Auto-Grade Road -> Road Corridor (Template = Road Network's Corridor Templates, Terrain = Site Fetch or survey mesh).

---

## 6. Decision log

| Date | Decision | Why |
|---|---|---|
| 2026-11-01 | Adopt porous tree crowns (Beer-Lambert, per-crown LAD) in Sun Hours instead of solid trees. | VoxCity review; trees are the main shading error in solid models. |
| 2026-11-01 | Site Fetch uses Copernicus GLO-30 (AWS, range reads), ESA WorldCover 2021, OSM via Overpass; local frame = UTM of the site centre at 0,0. | Free, global, no keys; precision in Rhino; FABDEM is non-commercial. |
| 2026-11-01 | Released as v1.12.0 (PR #1). | |
| 2026-11-22 | Open Infra Studio (GPL) used for methods only; everything written from scratch. | Licence. |
| 2026-11-22 | Hydro-DEM fills depressions (Priority-Flood) by default, with a Fill Pits switch for the old behaviour. | Streams used to stop in every hollow. |
| 2026-11-22 | Roads are solved **topology first** (Road Network), and the **2D plan comes before 3D**. | Junctions with any number of arms; drawings early; one source for 2D and 3D. |
| 2026-11-22 | Road attributes come from **User Text** on referenced centrelines (`road_*`), with override lists for Grasshopper-made curves, then defaults. | Data stays with the geometry; editable in UserText DB; robust to reordering. |
| 2026-11-22 | Junction types are tagged with **points** carrying `junction_*` User Text; untagged junctions are classified automatically. | One clear tag per junction; roundabouts are design choices, not detectable. |
| 2026-11-22 | The **same templates** drive the 2D plan and the 3D corridor (Corridor Templates output). | Consistency between drawing and model. |
| 2026-11-22 | Road templates are Enzyme's own typical dimensions, flagged as such. | Not copied (GPL); to be checked against local guides. |
| 2026-11-22 | **Method notes standard**: one source (`MethodNotes.cs`) for Info outputs and docs pages; Info port appended on released components, before Dashboard Data on new ones. | Designers and reviewers must see method, assumptions and simplifications in the UI and the docs. |
| 2026-11-22 | This file (`PROJECT_CONTEXT.md`) is the hand-over context, with `CLAUDE.md` / `AGENTS.md` pointing to it. | Development from other accounts and colleagues' AI tools. |
| 2026-11-22 | Method notes written for all 115 components. Info ports: existing fixed-text Info replaced by the notes; Info with run values kept and the notes appended; analysis / generator / data components without Info got one appended as their last output; trivial utilities got docs only. | Standard applied everywhere without breaking placed canvases. |
| 2026-11-22 | Findings from the review go to `REVIEW_FINDINGS.md`; no behaviour was changed in that pass. | Fixes need Rhino testing and owner decisions. |
| 2026-11-22 | Licence: free use with attribution; commercial products need permission (`LICENSE`, `NOTICE.md`). | Owner's choice. |
| 2026-11-22 | Repo clean-up: removed `Archive/`, `ScriptsToProcess/`, `dev_scripts/_archive/` and 14 unused icons (all still in git history); icon tool moved to `dev_scripts/`. | Owner asked to delete garbage. |
| 2026-11-22 | Review-fixes pass: 197 findings fixed in 89 components, 37 left open for owner decisions or canvas tests, 17 not bugs. Ports and GUIDs unchanged. | Owner asked to fix the bugs. |
| 2026-11-22 | Results deliberately changed by fixes: UTCI is the full Bröde et al. 2012 polynomial (from pythermalcomfort, MIT); Fast CFD uses the physical time step for viscosity and friction; Auto-Grade never exceeds Max Slope; Terrain Grader measures earthworks against the grading rule; slope tools treat vertical pieces as non-compliant; Terrain Slope over-threshold shares are areas. | Correctness. |
| 2026-11-22 | Higrothermal Comfort renamed Hygrothermal Comfort (GUID unchanged). | Typo. |
| 2026-11-22 | Copyright holder: **Enzyme APD Limited**. | Owner. |
| 2026-11-22 | **Facade Module Distributor unpublished**: hidden from the toolbar, download / run of remote Python disabled (placed copies show an error). Re-publish only with the script shipped inside the plugin. | Security: it ran unverified code from GitHub. |
| 2026-11-22 | **Fast Noise barrier term follows ISO 9613-2** (C2 = 20: Dz = 10 log10(3 + 10 N)); about 3 dB less screening than before. | Owner chose the standard. |
| 2026-11-22 | **Heliodon North input** added (last input; degrees CCW from +Y, Ladybug convention, same as Fast CFD). | Rotated models. |
| 2026-11-22 | **Junction levels**: the priority road keeps its own profile and side roads meet it at its height (option b); option (a), junction at the average ground level, kept as a choice for flat plazas. | Owner; usual engineering practice. |

---

## 7. Open issues and questions

**Untested in Rhino** (built and unit-tested on the maths only):
- I1. Road Network: curved arms near junctions (small kinks at returns), skewed junctions, roundabout / cul-de-sac drawing, User Text reading from referenced curves and points.
- I2. Road Corridor: daylight on real terrain, colouring, mass haul diagram placement, Corridor Templates wiring.
- I3. Hydro-DEM pit filling on large meshes (speed, pond surfaces).
- I4. Site Fetch: OSM parsing on a live Overpass response (only a hand-made fixture was tested); building extrusions with courtyards on terrain.

**Known limits** (also stated in the method notes):
- I5. Copernicus GLO-30 is a surface model (includes some buildings / canopy): not for detailed grading.
- I6. Road Corridor: superelevation without side friction; no curvature correction (overstated volumes on tight curves); no topsoil strip.
- I7. Road Network: mismatched templates only wrap kerb face / kerb back / boundary round corners; footway hatches stop at mouths; no splitter islands or hammerhead turning heads; radii not checked against swept paths.

**Questions for the owner:**
- Q1. ~~Licence~~: decided; copyright holder Enzyme APD Limited (section 6). A lawyer's review of the custom licence is still recommended.
- Q2. Which street design guide should the road templates follow by default (Spain, UK, NL CROW, US AASHTO, per project)?
- Q3. ~~Junction levels~~: decided, priority road profile with side roads tied to it; ground-level option for flat plazas (section 6).
- Q4. ~~ScriptsToProcess~~: deleted.
- Q5. Open review items needing a decision (details and recommendations in `REVIEW_FINDINGS.md`): hand-typed GUIDs (Fast Noise, Keypoint, Pixel Gradient, Noise Emitter, Pixelated Surface: keep unless a clash appears); JSON MP Engine setback facades; MP adapters' absolute vs relative Z; branch-path stability in Masses and Facade parsers; Global Flood Engine volume conservation; Report Metrics re-rating merged flags (fix in `Utils/ReportMetrics.cs`); Terrain Generator hash at UTM coordinates.

Component-level findings from the method-notes review are listed in section 9.

---

## 8. Pipeline / next steps

In order of priority (update as items finish):

1. **Test PR #2 and the review-fixes PR in Rhino** (I1 to I4 and the canvas list in section 9), fix, merge, release (next version after 1.13.0).
2. **Road Junction (3D):** priority / T / crossroads patches tied to the corridor end sections, roundabouts (with splitter islands) and cul-de-sacs in 3D, earthworks of the junction patches added to the totals; levels: priority road profile, side roads tied to it (option for flat plazas at ground level).
3. **Auto-Grade from the network:** grade priority roads first, then side roads starting from the priority road's level at each junction, so roads meet; optional flat-plaza junctions at the average ground level.
3b. **Facade Module Distributor:** bring `facade_modules.py` into the repo, document it, ship it inside the plugin, then re-publish the node.
4. **Stitch corridors into the terrain** (spec phase 5): one finished-ground mesh for CFD, Hydro-DEM, sun and noise.
5. **More road tags** from the spec: one-way, lane counts, medians, boulevards, crossings, mapped to templates.
6. **Design criteria check** for roads (radii, grades, vertical curves, sight distance) from primary-source tables (AASHTO, CROW or the chosen guide, Q2).
7. **Site Fetch extras:** Meta 1 m canopy height, Overture buildings, optional FABDEM (non-commercial projects), OSM building:part.
8. **VoxCity items not yet done:** green view index, sky view on facades, landmark visibility, street-network aggregation of results, ENVI-met / PALM export, Perez sky and reflections in Sun Hours.
9. **IFC 4.3 alignment export** for BIM handover.
10. **Decide the open review items** (Q5) and fix them; spot-check the AI-written method notes of the analysis tools used in reports.
11. **Legal review of `LICENSE`** (custom licence) and confirm the copyright holder (Q1).

---

## 9. Component review findings

`REVIEW_FINDINGS.md` lists every finding with its status: **200 fixed, 34 open, 17 not a bug** (2026-11-22). Nothing in the fixes has been run in Rhino yet.

Status of the priorities:
- P1 UTCI: **fixed** (full 210-term polynomial, Hardy vapour pressure, validity clamps with a warning, extreme categories; matches pythermalcomfort's reference values).
- P2 Fast CFD time step: **fixed** (physical seconds; Viscosity / Friction now about 1.6× stronger per step at defaults: review defaults on a canvas). Terrain speed-up only seeding the initial field: **open**, documented.
- P3 Fast Noise barrier constant: **fixed** (ISO 9613-2, C2 = 20).
- P4 slope tools: **fixed** (vertical = 90°, ∞ %, 1:0, never compliant; ratio legend corrected). Representation of flat pieces in ratio mode (∞) and per-curve output order: open.
- P5 Auto-Grade: **fixed** (clamped back to Max Slope after smoothing).
- P6 Terrain Grader: **fixed** (earthworks against the grading rule per cell).
- P7 number parsing: **fixed** (invariant culture).
- P8 Topology Split Edge Classifier partial walls: **fixed**.
- P9 Heliodon: **fixed** (wrap-around months, half-hour start); still only the 21st of each month (documented); North input **added**.
- P10 Raindrop Flow hang: **fixed** (error on spacing ≤ 0, seed cap, sane sliders).
- P11 Terrain Height outputs lost on load: **fixed** (`Read()` keeps the layout).
- P12 MP Engine colours: **fixed** (stable hash).
- P13 JSON escaping: **fixed**.
- P14 placeholder GUIDs: **open** (keep unless a clash appears).
- P15 facade extrusions downwards: **fixed** (forced to world +Z) in Frames, Fins, Rhythmic Balconies, Balcony, Retail, Masses parser; Spandrels still to check.

**Canvas tests needed before releasing the fixes:** facade modules (+Z extrusions, clockwise outlines), Pixel Gradient concave cells and bakes, Terrain Height opening old files, Fast CFD defaults, Terrain Grader volumes, Road Generator contours, Topology Split overlaps, Auto-Phase undo, CWT outward side on L / U plans.

## 10. Working on this repo with an AI assistant

- Point the assistant at this file first, then `DEVELOPMENT_GUIDE.md`.
- A Linux sandbox can build the `.gha` and run the tests (`dotnet` 8 SDK; net48 reference assemblies come from NuGet). It cannot run Rhino or Grasshopper: say so in the PR and list what needs a canvas test.
- Keep algorithms Rhino-free in `Utils/` with tests; keep components thin.
- Before pushing: build, tests (`ALL PASS`), `gen_docs.py`, changelog, and this file if anything in sections 6 to 9 changed.
- Never merge or push to `main`; never bump `<Version>` unless asked to release.
