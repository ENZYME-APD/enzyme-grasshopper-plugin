# Enzyme Grasshopper Plugin: Development & Best Practices

This document compiles the accumulated knowledge, best practices, and specific quirks discovered while developing the Enzyme Grasshopper plugin, managing its GitHub Actions CI/CD pipeline, and deploying via YAK (Rhino Package Manager).

## 1. Grasshopper API & UI Auto-Wiring

### The `AddedToDocument` Pattern
To spawn default UI elements (sliders, panels, swatches, relays) when a component is dropped onto the canvas, we override `AddedToDocument(GH_Document document)`. 
*   **The Initialization Trap:** `AddedToDocument` is triggered by the canvas *before* Grasshopper officially initializes the component's UI attributes or marks it as "Selected". Relying on `if (!this.Attributes.Selected) return;` will instantly abort the script on fresh drops.
*   **The Fix:** Always ensure attributes exist by calling `if (this.Attributes == null) this.CreateAttributes();` before doing canvas math.
*   **Preventing Duplication:** Check if `SourceCount > 0` on your inputs to ensure you don't spawn new sliders when the user copy-pastes an already-wired component or opens a saved file.

### Instantiating Grasshopper Objects
*   **Sliders (`GH_NumberSlider`):** Requires setting `Slider.Minimum`, `Slider.Maximum`, and `Slider.Value`. **Crucial:** The `GH_SliderAccuracy` enum (used for Float, Integer, etc.) resides in the `Grasshopper.GUI.Base` namespace.
*   **Value Lists (`GH_ValueList`):** Requires clearing `ListItems` and adding `GH_ValueListItem(key, value)`. **Crucial:** In Grasshopper, string values in Value Lists must be explicitly wrapped in literal quotes (e.g., `"\"Tower\""`) otherwise the output will fail to cast to a string.
*   **Buttons (`GH_ButtonObject`):** Excellent for "Run" or "Execute" boolean triggers instead of `GH_BooleanToggle` to prevent accidental double-execution (e.g. `InitKeys`).
*   **Layout:** Position spawned nodes using `comp.Attributes.Pivot` and offsetting the X and Y coordinates. Use `document.AddObject(component, false)` to add them quietly without locking the UI.

## 2. C# Component Data Handling

*   **DataTree Iteration:** Grasshopper's `GH_Structure<T>.get_Branch(GH_Path)` returns a weakly-typed `IList`. When iterating, you *must* explicitly cast the elements (e.g., `foreach (GH_Curve x in branch.Cast<GH_Curve>())`) to avoid runtime binding errors.
*   **Variable Shadowing:** GitHub Actions uses a strict C# compiler. Re-using loop variable names (like `int p`) inside nested scopes or LINQ queries will cause `CS0136` build failures, even if it runs fine locally in some IDEs.
*   **Spatial Searching:** For high-performance topological queries (like the `TopologySplitEdgeClassifier`), rely on `Rhino.Geometry.RTree`. It prevents $O(N^2)$ bottlenecks when checking thousands of shared boundary collisions.

## 3. GitHub Actions & CI/CD

*   **Build Pipeline:** The plugin is compiled via `dotnet build enzGhPlugin.csproj -c Release` on the cloud runner.
*   **Workflows:**
    *   `ci-check.yml` — compile-only check (plus docs-sync check) on every branch push / PR. Nothing is packaged or published.
    *   `build.yml` — **automatic release.** Runs on every push to `main`: if tag `v<Version>` doesn't exist yet, it builds, packages the `.yak`, publishes to Yak, then creates the GitHub Release and the tag. If the tag already exists it does nothing. A failed Yak push fails the run.
    *   `deploy-docs.yml` — redeploys the docs site (GitHub Pages) whenever `docs/**` changes on `main`.
    *   `cleanup-yak.yml` — manual: yanks old Yak versions via `dev_scripts/yank_versions.py`.
*   **Version Bumping:** `<Version>` in `enzGhPlugin.csproj` is the release switch. Bump it (e.g. `1.10.78` -> `1.10.79`) only when you want a release; pushes to `main` without a bump don't publish anything. Yak rejects a version that already exists.
*   **Release Workflow:**
    1. Work on a branch; push and wait for **CI Check** to go green.
    2. Run `python3 dev_scripts/gen_docs.py` to sync the docs site with the code. Hand-written sections and port descriptions are kept unless the ports changed. CI fails if this was forgotten.
    3. Bump `<Version>` in `.csproj`, commit `build: bump version to X.Y.Z`, merge to `main` and push.
    4. Confirm **Build Enzyme Grasshopper Plugin** and **Deploy Docs** are green. No manual tagging needed (pushing a matching `vX.Y.Z` tag by hand still works, but don't do both).
*   **Debugging Actions:** Use the GitHub CLI (`gh run list` and `gh run view <run_id> --log-failed`) to quickly diagnose cloud compilation errors without leaving the terminal.

## 4. YAK (Rhino Package Manager) Quirks

*   **Canvas Caching:** When a user updates the plugin version via Yak, existing components already placed on their Grasshopper canvas *retain their legacy Input/Output port mappings, names, and internal states*. 
*   **Applying Updates:** To see updated port structures, modified descriptions, or to trigger newly added auto-wiring logic, the user *must* delete the old component and place a fresh one from the toolbar. Reloading Rhino does not update legacy components embedded in a saved `.gh` script.

## 5. HUD Standard (component message block)

Every component shows a HUD under its body via `this.Message`. **Always set it through `Enzyme.Utils.Hud`**, never by hand, so all components read the same way.

```
{NickName}            <- title: always the component NickName (never a hard-coded string)
12.34 ms              <- solve time; "1.20 s" above 1000 ms; "(cached)" suffix when re-emitting cache
---                   <- separator
Points: 1,204         <- aim for <=6 metric lines (hard cap 8), "Label: value", SI units, 40 chars max
Rays: 38,520
```

| State | Call | Shows |
|---|---|---|
| Solved | `Hud.Done(this, sw, lines…)` | title / time / --- / metrics |
| Re-emitting cached outputs | `Hud.Cached(this, ms, lines…)` | time with `(cached)` |
| Required input missing | `Hud.Waiting(this)` or `Hud.Waiting(this, "payload")` | `Awaiting data` |
| Run toggle false, nothing cached | `Hud.Off(this)` | `Off` |
| Exception / invalid input | `Hud.Error(this, ex.Message)` | `Error: …` + runtime error balloon |

Rules:
*   Time every solve with a `Stopwatch` started at the top of `SolveInstance`.
*   Metric helpers: `Hud.Metric("Points", n)` (thousands separators), `Hud.Metric("Area", a, "m²", 0)`, `Hud.Metric("Mode", "Grid")`.
*   Pick the 2–4 numbers a designer checks at a glance (counts, totals, min/max, % compliant). Detailed reports go to an `Info` / `Dashboard` output, not the HUD.
*   Invariant culture formatting (handled by the helper) so Spanish-locale machines show the same decimals.
*   Sentence case for status words (`Awaiting data`, `Off`), not ALL CAPS.

## 6. EnzymeComponent base class

New and migrated components inherit `Enzyme.Utils.EnzymeComponent` instead of `GH_Component`:

```csharp
public class MyTool : EnzymeComponent
{
    protected override bool UsesRunGate => true;   // only for analysis tools with a "Run" input

    protected override void Solve(IGH_DataAccess DA, Stopwatch sw)
    {
        // ...work...
        Hud.Done(this, sw, Hud.Metric("Points", n));
    }

    protected override void AutoWire(GH_Document doc)   // optional: default sliders on a fresh drop
    {
        AutoWireHelper.WireSlider(this, doc, 2, 0.5, 10, 2, 200, 0);
    }
}
```

*   **Errors:** any exception escaping `Solve()` becomes `Hud.Error` + a red balloon.
*   **Run gate (opt-in):** with `UsesRunGate => true`, `Run = false` skips `Solve()` and replays the last computed outputs with a `(cached)` HUD, or shows `Off` if nothing has run yet. Only for tools where Run means "compute"; tools where Run means "do an action" (export, bake, draw a HUD) keep their own logic.
*   **Run inputs are not added to tools that don't already have one** — a Run toggle exists only where it was put on purpose.
*   **Auto-wiring:** override `AutoWire(doc)`; the base already ensures Attributes exist and skips wiring when any input is already connected.
*   **CI runners:** all workflows run on Linux (`ubuntu-24.04`); the `.gha` is platform-independent .NET, and the Linux Yak CLI packages it. Resource file names are case-sensitive on Linux — keep `IconLoader.Load("Name.png")` exactly matching the file.
