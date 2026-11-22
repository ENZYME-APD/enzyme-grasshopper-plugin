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
| `Resources/` | 24 px PNG icons (`IconLoader.Load("Name.png")`, case-sensitive). New icons from Lucide (`get_icon.js`, `fetch_lucide.sh`). |
| `docs/` | GitHub Pages site: `index.html` cards, `components/<slug>.html` pages, guides (`Terrain_and_LEAP_Guide.md`, `Water_Analysis_Guide.md`). |
| `dev_scripts/gen_docs.py` | Syncs docs with the code (headers, port lists, cards, method notes). `--check` in CI. |
| `dev_scripts/yank_versions.py` | Yak clean-up (manual workflow). |
| `ScriptsToProcess/` | Older copies of scripts (see open question Q4). |
| `Archive/` | Superseded component versions kept for reference; not compiled. |
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
- Q1. Licence: the repo has no LICENSE file. Which licence applies to the plugin (it is public on GitHub and sold through Enzyme Digital Solutions)?
- Q2. Which street design guide should the road templates follow by default (Spain, UK, NL CROW, US AASHTO, per project)?
- Q3. Should junction levels be pinned to the terrain average (spec phase 4) or to the priority road's profile?
- Q4. `ScriptsToProcess/` holds older copies of `dev_scripts`; can it be removed?

Component-level findings from the method-notes review are listed in section 9.

---

## 8. Pipeline / next steps

In order of priority (update as items finish):

1. **Test PR #2 in Rhino** (I1 to I3), fix, merge, release (next version after 1.13.0).
2. **Road Junction (3D):** priority / T / crossroads patches tied to the corridor end sections, roundabouts (with splitter islands) and cul-de-sacs in 3D, earthworks of the junction patches added to the totals, junction levels from the network (Q3).
3. **Auto-Grade from the network:** grade edges with shared node levels so roads meet at junctions.
4. **Stitch corridors into the terrain** (spec phase 5): one finished-ground mesh for CFD, Hydro-DEM, sun and noise.
5. **More road tags** from the spec: one-way, lane counts, medians, boulevards, crossings, mapped to templates.
6. **Design criteria check** for roads (radii, grades, vertical curves, sight distance) from primary-source tables (AASHTO, CROW or the chosen guide, Q2).
7. **Site Fetch extras:** Meta 1 m canopy height, Overture buildings, optional FABDEM (non-commercial projects), OSM building:part.
8. **VoxCity items not yet done:** green view index, sky view on facades, landmark visibility, street-network aggregation of results, ENVI-met / PALM export, Perez sky and reflections in Sun Hours.
9. **IFC 4.3 alignment export** for BIM handover.
10. **Work through `REVIEW_FINDINGS.md`**, Priority 1 first (section 9); spot-check the AI-written method notes of the analysis tools used in reports.

---

## 9. Component review findings

Writing the method notes meant reading every component; 250 candidate findings in 91 components are listed in **`REVIEW_FINDINGS.md`** (checklist per component). They come from code reading, not Rhino runs: confirm each one before fixing. Where an old Info text claimed something the code does not do (Keypoint Engine, Keyline Engine, Legend Geometry, Data Visualizer, Elevation Label, Analysis Heatmap), the new method notes now describe what the code actually does.

**Priority 1: results can be wrong**
- P1. Microclimate Comfort "precise UTCI" uses 98 of the 210 terms of the UTCI polynomial (Bröde et al. 2012) and no validity limits: results wrong, worst in hot humid conditions.
- P2. Fast CFD: diffusion and friction use a time step scaled by CellSize / wind speed, so Viscosity and Friction depend on cell size (contradicts "grid-independent"); terrain speed-up only seeds the initial field.
- P3. Fast Noise Analyzer: barrier term 10·log10(3 + 20N) equals ISO 9613-2 with C2 = 40, not 20: up to about 3 dB more screening than the standard. Confirm intent.
- P4. Road Slope Analyzer / 3D Road Slope / Road Profile Unroller: vertical segments read as 0 % and compliant; flat segments give infinity in ratio mode; ratio legend reversed.
- P5. Auto-Grade Road: smoothing can push the ends above Max Slope.
- P6. Terrain Grader: cut / fill measured against a re-triangulated copy of the whole terrain, so resampling error counts as earthworks.
- P7. Regional number parsing (Table to DataTree, BIM Attribute Serializer): on Spanish Windows 3.5 reads as 35. Use invariant culture.
- P8. Topology Split Edge Classifier: walls shared over part of their length are not split, so external edges can be marked internal.
- P9. Heliodon samples only the 21st of each month; non-half-hour start times give no vectors; no North input.

**Priority 2: crashes, lost data, unstable output**
- P10. Raindrop Flow Engine: GridSpacing ≤ 0 loops forever (its auto-wired slider starts at 0).
- P11. Terrain Height (MeshHeightAnalysis): `Read()` deletes outputs after index 9 on file load, so saved definitions lose Info and Dashboard Data.
- P12. JSON MP Engine V2: fallback programme colours seeded with `string.GetHashCode()` change every Rhino session.
- P13. JSON builders (Adapter: The Lego Builder, Recipe JSON Builder): names are not escaped, so a quote breaks the JSON.
- P14. Fast Noise Analyzer and Keypoint Engine have hand-typed placeholder component GUIDs (risk of clashes with other plug-ins; changing them breaks saved files: decide).
- P15. Several facade modules (Frames, Fins, Rhythmic Balconies, Balcony) extrude along each curve's own plane normal: clockwise outlines may extrude downwards. Test.

**Priority 3: misleading names and descriptions** (typos such as Higrothermal, placeholder names on CWT, leftover debug descriptions, outputs that do not match their descriptions): see `REVIEW_FINDINGS.md`.

## 10. Working on this repo with an AI assistant

- Point the assistant at this file first, then `DEVELOPMENT_GUIDE.md`.
- A Linux sandbox can build the `.gha` and run the tests (`dotnet` 8 SDK; net48 reference assemblies come from NuGet). It cannot run Rhino or Grasshopper: say so in the PR and list what needs a canvas test.
- Keep algorithms Rhino-free in `Utils/` with tests; keep components thin.
- Before pushing: build, tests (`ALL PASS`), `gen_docs.py`, changelog, and this file if anything in sections 6 to 9 changed.
- Never merge or push to `main`; never bump `<Version>` unless asked to release.
