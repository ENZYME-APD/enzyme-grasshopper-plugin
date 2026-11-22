# Instructions for AI assistants

Read `PROJECT_CONTEXT.md` first: it is the hand-over context for this repo (what the plugin is, build / test / docs / release, standards, decision log, open issues, next steps). Then `DEVELOPMENT_GUIDE.md` for Grasshopper and CI details.

Must do on every change:
- Work on a branch; never push to `main`, never merge, never bump `<Version>` in `enzGhPlugin.csproj` unless asked to release.
- `dotnet build enzGhPlugin.csproj -c Release`, `dotnet run --project Tests/FieldMath.Tests -c Release` (ALL PASS), `python3 dev_scripts/gen_docs.py`.
- New or changed components: method notes in `Utils/MethodNotes.cs` + Info output (DEVELOPMENT_GUIDE section 8), HUD via `Hud`, Rhino-free maths in `Utils/` with tests.
- Released components: only append ports at the end.
- Update `changelogHT.md` (Unreleased) and `PROJECT_CONTEXT.md` (decisions, issues, next steps) in the same PR.
- GPL sources: methods only, never code.
