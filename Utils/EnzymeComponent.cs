using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Utils
{
    /// <summary>
    /// Shared base for Enzyme components. Gives every component the same behaviour for:
    ///
    ///  * Timing         — a Stopwatch is started before Solve() and passed in; Solve() reports
    ///                     its metrics with Hud.Done(this, sw, ...).
    ///  * Run toggle     — opt-in (override UsesRunGate => true) for analysis tools whose Boolean
    ///                     "Run" input means "compute". Run = false replays the last computed outputs
    ///                     with a "(cached)" HUD (or "Off" if nothing computed yet); Solve() is skipped.
    ///                     Tools whose Run means "do an action" (export, bake, draw) keep their own logic.
    ///  * Errors         — an exception escaping Solve() becomes Hud.Error + a red runtime balloon
    ///                     instead of Grasshopper's generic failure.
    ///  * Auto-wiring    — override AutoWire(doc) to spawn default sliders/toggles. The base only
    ///                     calls it on a fresh drop (no input already has sources), after ensuring
    ///                     Attributes exist (see DEVELOPMENT_GUIDE "AddedToDocument pattern").
    ///
    /// Migrating a component: inherit EnzymeComponent instead of GH_Component and rename
    /// SolveInstance(IGH_DataAccess DA) to Solve(IGH_DataAccess DA, Stopwatch sw).
    /// Remove its own Run/cache/HUD-on-off code; keep its Hud.Done(...) metric lines.
    /// </summary>
    public abstract class EnzymeComponent : GH_Component
    {
        public const string RunInputName = "Run";

        protected EnzymeComponent(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory) { }

        // ---------------- Solve pipeline ----------------

        /// <summary>The component's real work. sw is already running.</summary>
        protected abstract void Solve(IGH_DataAccess DA, Stopwatch sw);

        private List<GH_Structure<IGH_Goo>> _cache;
        private string _cacheHud;
        private bool _replayPending;

        /// <summary>True for analysis tools: Run = false skips the solve and replays cached results.</summary>
        protected virtual bool UsesRunGate => false;

        /// <summary>Index of the Boolean "Run" input when UsesRunGate, otherwise -1.</summary>
        protected int RunInputIndex
        {
            get
            {
                if (!UsesRunGate) return -1;
                for (int i = 0; i < Params.Input.Count; i++)
                {
                    var p = Params.Input[i];
                    if (p is Grasshopper.Kernel.Parameters.Param_Boolean &&
                        string.Equals(p.Name, RunInputName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
                return -1;
            }
        }

        protected sealed override void SolveInstance(IGH_DataAccess DA)
        {
            var sw = Stopwatch.StartNew();

            int runIdx = RunInputIndex;
            if (runIdx >= 0)
            {
                bool run = false;
                DA.GetData(runIdx, ref run);
                if (!run)
                {
                    _replayPending = _cache != null;
                    if (_cacheHud != null) Message = Hud.MarkCached(_cacheHud);
                    else Hud.Off(this);
                    return;
                }
            }

            try
            {
                Solve(DA, sw);
            }
            catch (Exception ex)
            {
                Hud.Error(this, ex.Message);
            }
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            _replayPending = false;
        }

        protected override void AfterSolveInstance()
        {
            base.AfterSolveInstance();
            if (RunInputIndex < 0) return;

            if (_replayPending && _cache != null)
            {
                // Run = false: put the last computed results back on the outputs.
                for (int i = 0; i < Params.Output.Count && i < _cache.Count; i++)
                {
                    Params.Output[i].ClearData();
                    Params.Output[i].AddVolatileDataTree(_cache[i]);
                }
                return;
            }

            // Run = true and the solve succeeded: remember outputs + HUD for later replays.
            if (RuntimeMessageLevel == GH_RuntimeMessageLevel.Error) return;
            var snap = new List<GH_Structure<IGH_Goo>>(Params.Output.Count);
            foreach (var p in Params.Output)
            {
                var s = new GH_Structure<IGH_Goo>();
                foreach (var path in p.VolatileData.Paths)
                {
                    var branch = p.VolatileData.get_Branch(path);
                    foreach (var item in branch)
                        s.Append(item is IGH_Goo g ? g.Duplicate() : null, path);
                }
                snap.Add(s);
            }
            _cache = snap;
            _cacheHud = Message;
        }

        // ---------------- Auto-wiring ----------------

        /// <summary>Spawn default input widgets (use AutoWireHelper). Called only on a fresh drop.</summary>
        protected virtual void AutoWire(GH_Document document) { }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (Attributes == null) CreateAttributes();
            foreach (var p in Params.Input)
                if (p.SourceCount > 0) return; // pasted / loaded already wired
            AutoWire(document);
        }
    }
}
