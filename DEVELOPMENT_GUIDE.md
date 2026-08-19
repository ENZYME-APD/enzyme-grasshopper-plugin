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
*   **Version Bumping:** The pipeline and YAK distribution are driven by the `<Version>` tag inside `enzGhPlugin.csproj`. You must manually increment this (e.g., `1.8.18` -> `1.8.19`) before tagging and pushing a release.
*   **Release Workflow:** 
    1. Update code.
    2. Bump version in `.csproj`.
    3. `git commit -m "..."`
    4. `git tag -a v1.8.X -m "Release v1.8.X"`
    5. `git push origin main && git push origin v1.8.X`
*   **Debugging Actions:** Use the GitHub CLI (`gh run list` and `gh run view <run_id> --log-failed`) to quickly diagnose cloud compilation errors without leaving the terminal.

## 4. YAK (Rhino Package Manager) Quirks

*   **Canvas Caching:** When a user updates the plugin version via Yak, existing components already placed on their Grasshopper canvas *retain their legacy Input/Output port mappings, names, and internal states*. 
*   **Applying Updates:** To see updated port structures, modified descriptions, or to trigger newly added auto-wiring logic, the user *must* delete the old component and place a fresh one from the toolbar. Reloading Rhino does not update legacy components embedded in a saved `.gh` script.
