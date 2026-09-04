using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Enzyme;

namespace Enzyme.Components
{
    public class WindEngineHTVer : GH_Component
    {
        // --- Tunable model constants (previously unnamed magic numbers) ---
        // Wake length is expressed as a multiple of the obstructing building's height,
        // following simplified urban-wind wake-length rules of thumb (~6-8x obstacle height
        // for the visually-significant near-wake, well short of the full ~10-15x recovery
        // distance). Also hard-capped per solve against the site's own extents (see
        // WAKE_RANGE_DOMAIN_FRACTION) so a tall/placeholder building mesh can't blow the
        // wake out past the whole analysis grid and drown out terrain-driven variation.
        private const double WAKE_LENGTH_TO_HEIGHT_RATIO = 8.0;
        private const double WAKE_RANGE_DOMAIN_FRACTION = 0.35;
        private const double WAKE_MIN_INTENSITY = 0.12;

        // Corner/channeling search & influence radii are expressed as multiples of the
        // analysis GridSpacing (a real spatial dimension), not of wind speed.
        private const double CORNER_SEARCH_RADIUS_SPACING_MULTIPLIER = 5.0;
        private const double CORNER_INFLUENCE_RADIUS_SPACING_MULTIPLIER = 2.5;
        private const double CORNER_TANGENCY_THRESHOLD = 0.35;
        private const double CORNER_SPEED_BOOST_COEFFICIENT = 0.45;

        // Extra speed boost applied when a point sits between two buildings whose walls
        // roughly face each other (a venturi/tunneling corridor), on top of whatever the
        // single-wall corner bend above already did. A single-building corner model has
        // no notion of a second, opposing surface squeezing the flow - without this, two
        // close buildings never combine their effect and the gap between them reads as
        // plain, unaccelerated background flow.
        //
        // The naive continuity formula (A1*V1 = A2*V2) overpredicts real building-passage
        // speed-up, because a large share of the oncoming wind is blocked and diverted up
        // and over the buildings rather than forced through the gap. CFD/wind-tunnel study
        // of real building passages found the amplification factor tops out around ~1.49x
        // free-stream speed (Reading, "Revisiting the 'Venturi effect' in passage
        // ventilation between two non-parallel buildings", 2016 -
        // https://centaur.reading.ac.uk/45708/). At full narrowness our formula's multiplier
        // is (1 + GAP_TUNNELING_BOOST_COEFFICIENT), so 0.45 anchors the ceiling to that
        // ~1.45-1.5x empirical figure instead of an arbitrary round number.
        private const double GAP_TUNNELING_THRESHOLD = -0.2;
        private const double GAP_TUNNELING_BOOST_COEFFICIENT = 0.45;

        // Relaxation: each point can re-evaluate its own wake/corner/gap factors against
        // its own previous-pass direction for a few passes, so a single point can settle
        // through a sequential chain of building interactions (bend around one corner,
        // which then puts it tangent to a second wall it wasn't tangent to before, etc.)
        // instead of a single snapshot evaluation. This is NOT a real CFD relaxation - there
        // is no pressure/continuity solve and no data shared between neighboring points, so
        // more passes improve a point's own internal consistency, not physical accuracy.
        // Damping blends each new pass with the previous one to keep it from oscillating or
        // compounding runaway boosts across iterations.
        private const int ITERATIONS_MAX = 8;
        private const double ITERATION_DAMPING_DEFAULT = 0.5;

        private const double SLOPE_SPEEDUP_COEFFICIENT = 0.35;

        private const int STREAMLINE_SEED_ROW_STRIDE = 2;
        // Safety ceiling, not the intended stopping condition - a streamline is meant to stop
        // naturally the moment it exits the valid analysis grid (findNearestGridIndex returns
        // -1, i.e. it's genuinely left the mesh), goes solid, or its local speed dies out. This
        // just prevents a pathological case (e.g. a near-zero-speed eddy) from looping forever.
        private const int STREAMLINE_MAX_STEPS = 500;
        private const double STREAMLINE_STEP_SIZE = 0.2;
        private const double STREAMLINE_STEP_ACCEPT_SPACING_MULTIPLIER = 2.0;

        private const double COMFORT_SPEED_THRESHOLD_MS = 5.0;

        public WindEngineHTVer()
            : base("Urban Wind Vector Engine HT (Beta)", "WindEngineHTVer",
                "Simulates urban wind fields using terrain-parallel raycasting. Outputs a perfectly flat, crisp XY pixel-screen heatmap at a custom elevation.",
                "Enzyme", "Terrain")
        {
        }

        protected override Bitmap Icon
        {
            get
            {
                return IconLoader.Load("WindEngine.png");
            }
        }

        public override Guid ComponentGuid => new Guid("5620ba91-8c3d-4d5c-9229-22ca4df36a60");

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                PerformAutoWire(document);
            }
        }

        // Every Wire* call below self-guards on SourceCount>0 per input, so calling this
        // again later (e.g. from the right-click "Invoke Autowire" menu item) is safe and
        // idempotent - it only fills in whichever inputs/outputs are currently unconnected,
        // it won't touch or duplicate anything already wired.
        private void PerformAutoWire(GH_Document document)
        {
            Enzyme.Utils.AutoWireHelper.WireButton(this, document, 0, 210, -80);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 20, 10.0, 330, -40);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 5, 0.0, 3.0, 1.5, 330, 0);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 6, 0.0, 10.0, 5.0, 330, 40);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 8, 0.0, 3.0, 1.5, 330, 80);
            Enzyme.Utils.AutoWireHelper.WireGeneratedColorPalette(this, document, 7, 330, -180);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 9, 0.0, 20.0, WAKE_LENGTH_TO_HEIGHT_RATIO, 330, 120);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 10, 0.0, 1.0, SLOPE_SPEEDUP_COEFFICIENT, 330, 160);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 11, 0.0, 1.0, CORNER_TANGENCY_THRESHOLD, 330, 200);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 12, 0.0, 2.0, CORNER_SPEED_BOOST_COEFFICIENT, 330, 240);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 13, 0.0, 2.0, GAP_TUNNELING_BOOST_COEFFICIENT, 330, 280);
            Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 14, 1, ITERATIONS_MAX, 1, 330, 320);
            Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 15, 0.0, 1.0, ITERATION_DAMPING_DEFAULT, 330, 360);
            Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 16, true, 330, 400);
            Enzyme.Utils.AutoWireHelper.WireIntegerSlider(this, document, 18, 1, 1000, STREAMLINE_MAX_STEPS, 330, 440);
            Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -143);
            Grasshopper.Kernel.IGH_Param windVectorsRelay = Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "line", 220, -68);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 3, "curve", 220, -23);
            Grasshopper.Kernel.IGH_Param tagPointsRelay = Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 4, "point", 220, 67);
            Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 5, System.Drawing.Color.FromArgb(230, 230, 230), 220, 112);
            Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 6, "number", 220, 157);
            Enzyme.Utils.AutoWireHelper.WireVectorDisplayEx(document, tagPointsRelay, windVectorsRelay, System.Drawing.Color.Black, 2.0, 250, 0);
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendItem(menu, "Reset to Default Constants", Menu_ResetToDefaults_Clicked);
            GH_DocumentObject.Menu_AppendItem(menu, "Invoke Autowire (fill empty inputs/outputs)", Menu_InvokeAutowire_Clicked);
        }

        private void Menu_InvokeAutowire_Clicked(object sender, EventArgs e)
        {
            GH_Document doc = OnPingDocument();
            if (doc == null) return;
            PerformAutoWire(doc);
            this.ExpireSolution(true);
        }

        // Resets every currently-connected auto-wired slider/toggle back to the constant
        // defaults baked into this component - does not touch TerrainMesh/ContextBuildings/
        // WindDirection/CustomColors/SeedBox, and does nothing to an input whose source isn't
        // one of the auto-wired slider/toggle types (e.g. if it's fed by something custom).
        private void Menu_ResetToDefaults_Clicked(object sender, EventArgs e)
        {
            ResetSliderSource(4, 10.0);                                    // WindSpeed
            ResetSliderSource(5, 1.5);                                     // AnalysisHeight
            ResetSliderSource(6, 5.0);                                     // GridSpacing
            ResetSliderSource(9, WAKE_LENGTH_TO_HEIGHT_RATIO);             // WakeLengthRatio
            ResetSliderSource(10, SLOPE_SPEEDUP_COEFFICIENT);              // SlopeSpeedup
            ResetSliderSource(11, CORNER_TANGENCY_THRESHOLD);              // CornerTangency
            ResetSliderSource(12, CORNER_SPEED_BOOST_COEFFICIENT);         // CornerBoost
            ResetSliderSource(13, GAP_TUNNELING_BOOST_COEFFICIENT);        // GapBoost
            ResetSliderSource(14, 1.0);                                    // Iterations
            ResetSliderSource(15, ITERATION_DAMPING_DEFAULT);              // Damping
            ResetToggleSource(16, true);                                   // DrapeHeatmap
            ResetSliderSource(18, (double)STREAMLINE_MAX_STEPS);           // StreamlineMaxSteps

            this.ExpireSolution(true);
        }

        private void ResetSliderSource(int paramIndex, double defaultValue)
        {
            if (paramIndex >= this.Params.Input.Count) return;
            foreach (var source in this.Params.Input[paramIndex].Sources)
            {
                var slider = source as Grasshopper.Kernel.Special.GH_NumberSlider;
                if (slider != null)
                {
                    slider.Slider.Value = (decimal)defaultValue;
                    slider.ExpireSolution(true);
                }
            }
        }

        private void ResetToggleSource(int paramIndex, bool defaultValue)
        {
            if (paramIndex >= this.Params.Input.Count) return;
            foreach (var source in this.Params.Input[paramIndex].Sources)
            {
                var toggle = source as Grasshopper.Kernel.Special.GH_BooleanToggle;
                if (toggle != null)
                {
                    toggle.Value = defaultValue;
                    toggle.ExpireSolution(true);
                }
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Run", "Run", "Global execution toggle switch", GH_ParamAccess.item, false);
            pManager.AddMeshParameter("TerrainMesh", "TerrainMesh", "The underlying site topography", GH_ParamAccess.item);
            pManager.AddMeshParameter("ContextBuildings", "ContextBuildings", "Lightweight mesh context structures", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddVectorParameter("WindDirection", "WindDirection", "Travel vector of incoming air", GH_ParamAccess.item, new Vector3d(1, 1, 0));
            pManager.AddNumberParameter("WindSpeed", "WindSpeed", "Baseline velocity metric", GH_ParamAccess.item, 10.0);
            pManager.AddNumberParameter("AnalysisHeight", "AnalysisHeight", "Human pedestrian offset", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("GridSpacing", "GridSpacing", "Resolution size of pixel elements", GH_ParamAccess.item, 5.0);
            pManager.AddColourParameter("CustomColors", "CustomColors", "Custom color spectrum override", GH_ParamAccess.list);
            pManager[7].Optional = true;
            pManager.AddNumberParameter("HeatmapHeight", "HeatmapOffset", "Z offset applied to the heatmap: added to the terrain bounding box top in Flat mode (DrapeHeatmap=false), or added to each point's own local terrain height in Drape mode (DrapeHeatmap=true)", GH_ParamAccess.item, 0.0);
            pManager[8].Optional = true;
            pManager.AddNumberParameter("WakeLengthRatio", "WakeRatio", "Wake length as a multiple of the obstructing building's height (typical near-wake range 6-8x)", GH_ParamAccess.item, WAKE_LENGTH_TO_HEIGHT_RATIO);
            pManager[9].Optional = true;
            pManager.AddNumberParameter("SlopeSpeedup", "SlopeSpeedup", "Coefficient controlling how much upward terrain slope accelerates wind (0 = no slope effect)", GH_ParamAccess.item, SLOPE_SPEEDUP_COEFFICIENT);
            pManager[10].Optional = true;
            pManager.AddNumberParameter("CornerTangency", "CornerTangency", "How tangent (0-1) wind must be to a wall before corner-channeling kicks in; lower = stricter", GH_ParamAccess.item, CORNER_TANGENCY_THRESHOLD);
            pManager[11].Optional = true;
            pManager.AddNumberParameter("CornerBoost", "CornerBoost", "Speed boost coefficient applied when wind channels around a building corner", GH_ParamAccess.item, CORNER_SPEED_BOOST_COEFFICIENT);
            pManager[12].Optional = true;
            pManager.AddNumberParameter("GapBoost", "GapBoost", "Extra speed boost applied when a point is squeezed between two facing building walls (venturi/tunneling), scaled by how narrow the gap is", GH_ParamAccess.item, GAP_TUNNELING_BOOST_COEFFICIENT);
            pManager[13].Optional = true;
            pManager.AddIntegerParameter("Iterations", "Iterations", "Relaxation passes per point: each pass re-evaluates wake/corner/gap using the previous pass's own direction, letting a point settle through multiple sequential building interactions instead of a single snapshot. 1 = original single-pass behavior", GH_ParamAccess.item, 1);
            pManager[14].Optional = true;
            pManager.AddNumberParameter("Damping", "Damping", "Blend factor (0-1) between a point's previous iteration and its newly computed one. Lower = slower, more stable convergence; 1.0 = no damping (replace outright each pass)", GH_ParamAccess.item, ITERATION_DAMPING_DEFAULT);
            pManager[15].Optional = true;
            pManager.AddBooleanParameter("DrapeHeatmap", "Drape", "true = heatmap follows terrain relief, offset from each point's own local ground height. false = a single flat plane, offset from the terrain's bounding-box top", GH_ParamAccess.item, true);
            pManager[16].Optional = true;
            pManager.AddBoxParameter("SeedBox", "SeedBox", "Optional bounding box - every valid analysis grid point inside it is used as a streamline seed instead of the default sparse grid sampling", GH_ParamAccess.item);
            pManager[17].Optional = true;
            pManager.AddIntegerParameter("StreamlineMaxSteps", "MaxSteps", "Safety ceiling on streamline length. A streamline normally stops on its own once it exits the mesh, goes solid, or its speed dies out - this only guards against a pathological case looping forever", GH_ParamAccess.item, STREAMLINE_MAX_STEPS);
            pManager[18].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("VelocityHeatmap", "VelocityHeatmap", "Flat, unwelded crisp horizontal pixel-tile matrix", GH_ParamAccess.item);
            pManager.AddLineParameter("WindVectors", "WindVectors", "Spatial direction markers", GH_ParamAccess.list);
            pManager.AddColourParameter("VectorColors", "VectorColors", "Velocity color map matching lines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Streamlines", "Streamlines", "Continuous particle flow paths", GH_ParamAccess.list);
            pManager.AddPointParameter("TagPoints", "TagPoints", "Anchor coordinates for Text Tag", GH_ParamAccess.list);
            pManager.AddMeshParameter("PlainMesh", "PlainMesh", "Original topography mesh without vertex colors", GH_ParamAccess.item);
            pManager.AddNumberParameter("VelocityValues", "VelocityValues", "Raw unformatted velocity values, aligned with WindVectors", GH_ParamAccess.list);
                    pManager.AddTextParameter("Info", "I", "Component information and interpretation", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool execute = false;
            DA.GetData(0, ref execute);

            Mesh terrain = null;
            DA.GetData(1, ref terrain);

            List<Mesh> buildings = new List<Mesh>();
            DA.GetDataList(2, buildings);

            Vector3d baseWindDir = new Vector3d(1, 1, 0);
            DA.GetData(3, ref baseWindDir);

            double speed = 10.0;
            DA.GetData(4, ref speed);

            double height = 1.5;
            DA.GetData(5, ref height);

            double spacing = 5.0;
            DA.GetData(6, ref spacing);

            List<Color> userColors = new List<Color>();
            DA.GetDataList(7, userColors);

            double heatmapOffset = 0.0;
            DA.GetData(8, ref heatmapOffset);

            double wakeRatio = WAKE_LENGTH_TO_HEIGHT_RATIO;
            DA.GetData(9, ref wakeRatio);

            double slopeCoeff = SLOPE_SPEEDUP_COEFFICIENT;
            DA.GetData(10, ref slopeCoeff);

            double cornerTangency = CORNER_TANGENCY_THRESHOLD;
            DA.GetData(11, ref cornerTangency);

            double cornerBoost = CORNER_SPEED_BOOST_COEFFICIENT;
            DA.GetData(12, ref cornerBoost);

            double gapBoost = GAP_TUNNELING_BOOST_COEFFICIENT;
            DA.GetData(13, ref gapBoost);

            int iterations = 1;
            DA.GetData(14, ref iterations);
            iterations = Math.Max(1, Math.Min(ITERATIONS_MAX, iterations));

            double damping = ITERATION_DAMPING_DEFAULT;
            DA.GetData(15, ref damping);
            damping = Math.Max(0.01, Math.Min(1.0, damping));

            bool drapeHeatmap = true;
            DA.GetData(16, ref drapeHeatmap);

            Box seedBox = Box.Unset;
            bool hasSeedBox = DA.GetData(17, ref seedBox) && seedBox.IsValid;

            int streamlineMaxSteps = STREAMLINE_MAX_STEPS;
            DA.GetData(18, ref streamlineMaxSteps);
            streamlineMaxSteps = Math.Max(1, streamlineMaxSteps);

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            Mesh heatmapMesh = new Mesh();
            List<Line> vectorLines = new List<Line>();
            List<Color> vectorColorList = new List<Color>();
            List<Color> meshColorList = new List<Color>();
            List<PolylineCurve> computedStreamlines = new List<PolylineCurve>();
            List<double> velocityRawData = new List<double>();
            List<Point3d> tagAnchorPoints = new List<Point3d>();

            double minObservedSpeed = double.MaxValue;
            double maxObservedSpeed = double.MinValue;
            int activeSensorCount = 0;
            int comfortablePointCount = 0;

            if (execute && terrain != null && baseWindDir.IsValid && speed > 0 && spacing > 0)
            {
                baseWindDir.Unitize();

                BoundingBox bbox = terrain.GetBoundingBox(true);
                terrain.FaceNormals.ComputeFaceNormals();

                // --- Step 1: baseline grid, terrain-parallel deflection (sequential; single raycast per point) ---
                // Grid is built from integer cell indices (not accumulated floating-point steps),
                // so cell (ix,iy) always lands exactly on bbox.Min + (ix,iy)*spacing - no drift,
                // and the same math can be used later to look a world position back up by cell.
                // Clamped to at least 1 so the streamline seed stride (which divides by uCount)
                // can never end up stepping by zero.
                int uCount = Math.Max(1, (int)Math.Floor((bbox.Max.X - bbox.Min.X) / spacing) + 1);
                int vCount = Math.Max(1, (int)Math.Floor((bbox.Max.Y - bbox.Min.Y) / spacing) + 1);
                int cellCount = uCount * vCount;

                Point3d[] gridPoints = new Point3d[cellCount];
                Vector3d[] finalDirs = new Vector3d[cellCount];
                double[] finalSpeeds = new double[cellCount];
                bool[] solidMasks = new bool[cellCount];
                bool[] validMasks = new bool[cellCount];

                for (int ix = 0; ix < uCount; ix++)
                    {
                        double currentX = bbox.Min.X + ix * spacing;
                        for (int iy = 0; iy < vCount; iy++)
                        {
                            double currentY = bbox.Min.Y + iy * spacing;
                            int cellIdx = ix * vCount + iy;

                            Point3d rayStart = new Point3d(currentX, currentY, bbox.Max.Z + 10.0);
                            Ray3d downRay = new Ray3d(rayStart, -Vector3d.ZAxis);
                            double hit = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, downRay);

                            if (hit >= 0.0)
                            {
                                Point3d exactSurfacePt = downRay.PointAt(hit);
                                Point3d pt = exactSurfacePt + new Vector3d(0, 0, height);

                                Vector3d terrainNormal = Vector3d.ZAxis;
                                MeshPoint mp = terrain.ClosestMeshPoint(exactSurfacePt, 0.1);
                                if (mp != null)
                                {
                                    terrainNormal = new Vector3d(terrain.FaceNormals[mp.FaceIndex]);
                                }
                                terrainNormal.Unitize();

                                Vector3d slopedWindDir = baseWindDir - (terrainNormal * (baseWindDir * terrainNormal));

                                double localSpeed = speed;
                                if (slopedWindDir.Length > 0.001)
                                {
                                    slopedWindDir.Unitize();
                                    localSpeed *= (1.0 + (slopedWindDir.Z * slopeCoeff));
                                }
                                else
                                {
                                    slopedWindDir = baseWindDir;
                                }

                                gridPoints[cellIdx] = pt;
                                finalDirs[cellIdx] = slopedWindDir;
                                finalSpeeds[cellIdx] = localSpeed;
                                validMasks[cellIdx] = true;
                            }
                            else
                            {
                                // No terrain under this cell - e.g. a falloff/cliff edge where the mesh
                                // doesn't fully cover its own rectangular bbox footprint. Do NOT fabricate
                                // a flat fallback point here: that used to create a physically nonsensical
                                // elevation jump against terrain-following neighbors, which is what was
                                // dragging vectors/streamlines off into empty space at terrain edges.
                                // Leave this cell explicitly invalid and excluded downstream.
                                gridPoints[cellIdx] = new Point3d(currentX, currentY, bbox.Min.Z);
                                finalDirs[cellIdx] = Vector3d.Zero;
                                finalSpeeds[cellIdx] = 0.0;
                                validMasks[cellIdx] = false;
                            }
                        }
                    }

                    // Hard cap on wake length: a fraction of the site's own footprint diagonal,
                    // so a tall/placeholder building mesh can never blow the wake out past the
                    // analysis grid and swamp terrain-driven variation.
                    double domainDiagonal = Math.Sqrt(
                        (bbox.Max.X - bbox.Min.X) * (bbox.Max.X - bbox.Min.X) +
                        (bbox.Max.Y - bbox.Min.Y) * (bbox.Max.Y - bbox.Min.Y));
                    double maxWakeRange = Math.Max(domainDiagonal * WAKE_RANGE_DOMAIN_FRACTION, spacing * 4.0);

                    // --- Precompute per-building heights, needed for the height-based wake range below ---
                    int buildingCount = buildings.Count;
                    double[] buildingHeights = new double[buildingCount];
                    for (int b = 0; b < buildingCount; b++)
                    {
                        Mesh bld = buildings[b];
                        if (bld == null) continue;
                        BoundingBox bb = bld.GetBoundingBox(true);
                        buildingHeights[b] = Math.Max(bb.Max.Z - bb.Min.Z, 0.01);
                    }

                    // Warm up each building's lazily-built spatial acceleration structures
                    // (used internally by IsPointInside/ClosestPoint/MeshRay) sequentially,
                    // BEFORE the parallel loop below reads them concurrently. RhinoCommon does
                    // not guarantee that first-time lazy construction of these structures is
                    // thread-safe, so without this warm-up, multiple Parallel.For threads
                    // hitting the same building mesh for the first time simultaneously would
                    // be a real race condition.
                    for (int b = 0; b < buildingCount; b++)
                    {
                        Mesh building = buildings[b];
                        if (building == null) continue;
                        building.FaceNormals.ComputeFaceNormals();
                        Point3d warmupCenter = building.GetBoundingBox(true).Center;
                        Point3d warmupClosest;
                        Vector3d warmupNormal;
                        building.ClosestPoint(warmupCenter, out warmupClosest, out warmupNormal, 0.0);
                        Ray3d warmupRay = new Ray3d(warmupCenter + Vector3d.ZAxis * 1000.0, -Vector3d.ZAxis);
                        Rhino.Geometry.Intersect.Intersection.MeshRay(building, warmupRay);
                        building.IsPointInside(warmupCenter, 0.01, false);
                    }

                    // --- Step 2: occlusion + wake + corner-channeling + gap-tunneling (parallel; each index owns its own slot) ---
                    // All of these are independent multiplicative factors on the baseline
                    // (terrain/slope-adjusted) speed, accumulated together rather than a
                    // binary either/or chain of branches. The only factor that legitimately
                    // ever zeroes things out is occlusion (a point literally inside solid
                    // geometry has no meaningful flow) - and even that is expressed as a
                    // multiply-by-0 term here, not a special early-exit, so it composes the
                    // same way as every other factor instead of skipping them.
                    //
                    // Previously wake-deceleration and corner-bend/gap-boost were mutually
                    // exclusive (if in wake, skip corner entirely). Since most points near a
                    // building end up wake-classified (their backward ray usually clips
                    // something nearby), that meant direction almost never actually bent
                    // around buildings - only slowed down in a straight line. Computing both
                    // independently, off the same original pre-deflection direction, fixes
                    // that and lets a point be simultaneously slowed by one building's wake
                    // AND bent/accelerated by another nearby wall or gap.
                    Parallel.For(0, gridPoints.Length, i =>
                    {
                        if (!validMasks[i]) return; // no terrain here (edge/falloff) - excluded from physics entirely

                        Point3d pt = gridPoints[i];
                        Vector3d baseDir = finalDirs[i];
                        double baseSpeed = finalSpeeds[i];

                        bool isInsideSolid = false;
                        for (int b = 0; b < buildingCount; b++)
                        {
                            Mesh building = buildings[b];
                            if (building == null) continue;
                            if (building.IsPointInside(pt, 0.01, false))
                            {
                                isInsideSolid = true;
                                break;
                            }
                        }

                        solidMasks[i] = isInsideSolid;

                        if (isInsideSolid)
                        {
                            // Occlusion is a hard 0 multiplier - no amount of iterating changes
                            // that, so skip the relaxation loop entirely for these points.
                            finalDirs[i] = baseDir;
                            finalSpeeds[i] = 0.0;
                            return;
                        }

                        double searchRadius = spacing * CORNER_SEARCH_RADIUS_SPACING_MULTIPLIER;
                        double infRadius = spacing * CORNER_INFLUENCE_RADIUS_SPACING_MULTIPLIER;

                        Vector3d currentDir = baseDir;
                        double currentSpeed = baseSpeed;

                        // Relaxation: each pass re-evaluates wake/corner/gap against currentDir
                        // (the previous pass's own settled direction) rather than always against
                        // the original baseDir. With Iterations=1 this reduces to exactly the
                        // single-pass behavior. Damping blends each new pass in gradually instead
                        // of replacing outright, so repeated corner/gap boosts can't compound into
                        // a runaway speed and the direction can't oscillate wildly pass to pass.
                        for (int iter = 0; iter < iterations; iter++)
                        {
                            double wakeFactor = 1.0;
                            double cornerFactor = 1.0;
                            double gapFactor = 1.0;
                            Vector3d bentDir = currentDir;

                            // --- Wake: how much this point is shadowed by whichever building is
                            // upwind of it, independent of any corner/gap effect below. ---
                            Ray3d backRay = new Ray3d(pt, -currentDir);
                            double closestHit = double.MaxValue;
                            int closestBuildingIdx = -1;

                            for (int b = 0; b < buildingCount; b++)
                            {
                                Mesh building = buildings[b];
                                if (building == null) continue;

                                double t = Rhino.Geometry.Intersect.Intersection.MeshRay(building, backRay);
                                if (t >= 0.0 && t < closestHit)
                                {
                                    closestHit = t;
                                    closestBuildingIdx = b;
                                }
                            }

                            if (closestBuildingIdx >= 0)
                            {
                                double wakeRange = Math.Min(buildingHeights[closestBuildingIdx] * wakeRatio, maxWakeRange);
                                if (closestHit < wakeRange)
                                {
                                    double wakeIntensity = closestHit / wakeRange;
                                    wakeFactor = Math.Max(WAKE_MIN_INTENSITY, wakeIntensity * wakeIntensity);
                                }
                            }

                            // --- Corner bend + boost, and gap/tunneling boost: both evaluated
                            // off currentDir (not off each other), so they stay order-independent
                            // within a single pass and always run regardless of wake status. ---
                            double nearestDist = double.MaxValue;
                            Vector3d nearestNormal = Vector3d.Zero;
                            bool hasNearest = false;

                            for (int b = 0; b < buildingCount; b++)
                            {
                                Mesh building = buildings[b];
                                if (building == null) continue;

                                Point3d closestPt;
                                Vector3d normal;
                                int faceIdx = building.ClosestPoint(pt, out closestPt, out normal, searchRadius);

                                if (faceIdx >= 0 && closestPt.IsValid)
                                {
                                    double dist = pt.DistanceTo(closestPt);

                                    if (dist < infRadius && dist > 0.001)
                                    {
                                        normal.Unitize();

                                        if (dist < nearestDist)
                                        {
                                            nearestDist = dist;
                                            nearestNormal = normal;
                                            hasNearest = true;
                                        }

                                        if (Math.Abs(normal * currentDir) < cornerTangency)
                                        {
                                            double blend = 1.0 - (dist / infRadius);
                                            Vector3d bypass = Vector3d.CrossProduct(normal, new Vector3d(0, 0, 1));
                                            if ((bypass * currentDir) < 0) bypass = -bypass;
                                            bypass.Unitize();

                                            bentDir = (currentDir * (1.0 - blend)) + (bypass * blend);
                                            bentDir.Unitize();
                                            cornerFactor *= (1.0 + (cornerBoost * blend));
                                        }
                                    }
                                }
                            }

                            // Gap/tunneling: a second, roughly opposing wall nearby means the
                            // point is squeezed in a corridor, not just beside one building.
                            if (hasNearest)
                            {
                                double oppositeDist = double.MaxValue;
                                bool hasOpposite = false;

                                for (int b = 0; b < buildingCount; b++)
                                {
                                    Mesh building = buildings[b];
                                    if (building == null) continue;

                                    Point3d closestPt;
                                    Vector3d normal;
                                    int faceIdx = building.ClosestPoint(pt, out closestPt, out normal, searchRadius);

                                    if (faceIdx >= 0 && closestPt.IsValid)
                                    {
                                        double dist = pt.DistanceTo(closestPt);
                                        if (dist < infRadius && dist > 0.001)
                                        {
                                            normal.Unitize();
                                            if (normal * nearestNormal < GAP_TUNNELING_THRESHOLD && dist < oppositeDist)
                                            {
                                                oppositeDist = dist;
                                                hasOpposite = true;
                                            }
                                        }
                                    }
                                }

                                if (hasOpposite)
                                {
                                    double corridorWidth = nearestDist + oppositeDist;
                                    double narrowness = 1.0 - Math.Min(1.0, corridorWidth / (2.0 * infRadius));
                                    if (narrowness > 0.0)
                                    {
                                        gapFactor *= (1.0 + (gapBoost * narrowness));
                                    }
                                }
                            }

                            double newSpeed = baseSpeed * wakeFactor * cornerFactor * gapFactor;

                            if (iter == 0)
                            {
                                // First pass has nothing settled to blend against yet - accept it
                                // outright. This guarantees Iterations=1 is bit-for-bit identical
                                // to the original single-pass behavior regardless of Damping.
                                currentSpeed = newSpeed;
                                currentDir = bentDir;
                            }
                            else
                            {
                                // Damped blend into the running result instead of an outright replace.
                                currentSpeed = (currentSpeed * (1.0 - damping)) + (newSpeed * damping);
                                Vector3d blendedDir = (currentDir * (1.0 - damping)) + (bentDir * damping);
                                currentDir = blendedDir.Length > 0.0001 ? blendedDir : bentDir;
                                currentDir.Unitize();
                            }
                        }

                        finalDirs[i] = currentDir;
                        finalSpeeds[i] = currentSpeed;
                    });

                // --- Step 3: cheap sequential aggregation (min/max/counts) ---
                for (int i = 0; i < gridPoints.Length; i++)
                {
                    if (!validMasks[i] || solidMasks[i]) continue;
                    activeSensorCount++;
                    double s = finalSpeeds[i];
                    if (s < minObservedSpeed) minObservedSpeed = s;
                    if (s > maxObservedSpeed) maxObservedSpeed = s;
                    if (s <= COMFORT_SPEED_THRESHOLD_MS) comfortablePointCount++;
                }

                double speedRange = maxObservedSpeed - minObservedSpeed;
                if (speedRange < 0.01) speedRange = 1.0;

                // --- Step 4: color mapping + vector/tag outputs (cheap, always recomputed so CustomColors changes are instant) ---
                for (int i = 0; i < gridPoints.Length; i++)
                {
                    if (!validMasks[i])
                    {
                        meshColorList.Add(Color.Empty); // placeholder to keep index alignment with gridPoints; tile skipped below
                        continue;
                    }

                    Point3d pt = gridPoints[i];
                    Vector3d localDir = finalDirs[i];
                    double localSpeed = finalSpeeds[i];
                    bool isInsideSolid = solidMasks[i];

                    double intensity = (localSpeed - minObservedSpeed) / speedRange;
                    intensity = Math.Min(1.0, Math.Max(0.0, intensity));
                    Color mappedColor;

                    if (isInsideSolid)
                    {
                        mappedColor = Color.FromArgb(255, 12, 22, 52);
                    }
                    else if (userColors.Count >= 2)
                    {
                        double position = intensity * (userColors.Count - 1);
                        int lowIdx = (int)Math.Floor(position);
                        int highIdx = (int)Math.Ceiling(position);
                        double blend = position - lowIdx;

                        Color c1 = userColors[lowIdx];
                        Color c2 = userColors[highIdx];

                        int r = (int)(c1.R * (1.0 - blend) + c2.R * blend);
                        int g = (int)(c1.G * (1.0 - blend) + c2.G * blend);
                        int b = (int)(c1.B * (1.0 - blend) + c2.B * blend);
                        mappedColor = Color.FromArgb(255, r, g, b);
                    }
                    else if (userColors.Count == 1)
                    {
                        mappedColor = userColors[0];
                    }
                    else
                    {
                        int r = (int)(15 * (1.0 - intensity) + 255 * intensity);
                        int g = (int)(45 * (1.0 - intensity) + 200 * intensity);
                        int b = (int)(120 * (1.0 - intensity) + 255 * intensity);
                        mappedColor = Color.FromArgb(255, r, g, b);
                    }

                    meshColorList.Add(mappedColor);

                    if (localSpeed > 0.01)
                    {
                        vectorLines.Add(new Line(pt, localDir * (localSpeed * 0.5)));
                        velocityRawData.Add(localSpeed);
                        tagAnchorPoints.Add(pt);
                        vectorColorList.Add(mappedColor);
                    }
                }

                double halfGrid = spacing * 0.5;

                // Flat mode: one plane, offset from the terrain's own bounding-box top.
                // Drape mode: each tile at its own point's local terrain height, offset the
                // same way. gridPoints[i].Z already includes the AnalysisHeight pedestrian
                // offset, so that's subtracted back out for Drape mode or the heatmap would
                // float above the mesh instead of sitting on it.
                double flatModeZ = bbox.Max.Z + heatmapOffset;

                for (int i = 0; i < gridPoints.Length; i++)
                {
                    if (!validMasks[i]) continue; // no terrain here - leave the heatmap tile out rather than fake-flat

                    Point3d centerPt = gridPoints[i];
                    Color tileColor = meshColorList[i];

                    double tileZ = drapeHeatmap ? (centerPt.Z - height + heatmapOffset) : flatModeZ;

                    int vIndex = heatmapMesh.Vertices.Count;

                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X - halfGrid, centerPt.Y - halfGrid, tileZ));
                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X + halfGrid, centerPt.Y - halfGrid, tileZ));
                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X + halfGrid, centerPt.Y + halfGrid, tileZ));
                    heatmapMesh.Vertices.Add(new Point3d(centerPt.X - halfGrid, centerPt.Y + halfGrid, tileZ));

                    heatmapMesh.VertexColors.Add(tileColor);
                    heatmapMesh.VertexColors.Add(tileColor);
                    heatmapMesh.VertexColors.Add(tileColor);
                    heatmapMesh.VertexColors.Add(tileColor);

                    heatmapMesh.Faces.AddFace(vIndex, vIndex + 1, vIndex + 2, vIndex + 3);
                }

                // --- Step 5: streamlines, O(1) grid-indexed lookup instead of brute-force nearest neighbor ---
                Dictionary<(int, int), int> cellLookup = new Dictionary<(int, int), int>(gridPoints.Length);
                for (int i = 0; i < gridPoints.Length; i++)
                {
                    if (!validMasks[i]) continue; // never route streamlines onto/through a cell with no real terrain
                    int cx = (int)Math.Round((gridPoints[i].X - bbox.Min.X) / spacing);
                    int cy = (int)Math.Round((gridPoints[i].Y - bbox.Min.Y) / spacing);
                    cellLookup[(cx, cy)] = i;
                }

                // How many cells a single streamline step could possibly cross, so the
                // search ring below always covers the full step distance regardless of
                // how GridSpacing is set. A fixed 1-ring search would silently under-cover
                // (and kill a streamline early, or misroute it) whenever STREAMLINE_STEP_SIZE
                // is larger than GridSpacing - e.g. a user setting a fine GridSpacing while
                // the step size constant stays the same.
                int streamlineSearchRing = Math.Max(1, (int)Math.Ceiling(STREAMLINE_STEP_SIZE / spacing) + 1);

                Func<Point3d, int> findNearestGridIndex = (Point3d p) =>
                {
                    int cx = (int)Math.Round((p.X - bbox.Min.X) / spacing);
                    int cy = (int)Math.Round((p.Y - bbox.Min.Y) / spacing);

                    int bestIdx = -1;
                    double bestDist = double.MaxValue;
                    // Exact cell plus a neighborhood sized to the step/spacing ratio, in case
                    // the particle drifted between lattice cells. Still O(ring^2), independent
                    // of total grid size N, so this stays effectively O(1) per step.
                    for (int dx = -streamlineSearchRing; dx <= streamlineSearchRing; dx++)
                    {
                        for (int dy = -streamlineSearchRing; dy <= streamlineSearchRing; dy++)
                        {
                            int idx;
                            if (cellLookup.TryGetValue((cx + dx, cy + dy), out idx))
                            {
                                double d = p.DistanceTo(gridPoints[idx]);
                                if (d < bestDist)
                                {
                                    bestDist = d;
                                    bestIdx = idx;
                                }
                            }
                        }
                    }
                    return bestIdx;
                };

                // Traces one streamline from grid index seedIdx, stepping until it genuinely
                // exits the mesh (findNearestGridIndex returns -1), goes solid, or its local
                // speed dies out - streamlineMaxSteps is only a safety ceiling, not the
                // intended stopping condition.
                Action<int> traceStreamlineFrom = (seedIdx) =>
                {
                    List<Point3d> pathVertices = new List<Point3d>();
                    Point3d trackingParticle = gridPoints[seedIdx];
                    pathVertices.Add(trackingParticle);

                    for (int step = 0; step < streamlineMaxSteps; step++)
                    {
                        int closestIdx = findNearestGridIndex(trackingParticle);

                        if (closestIdx != -1
                            && trackingParticle.DistanceTo(gridPoints[closestIdx]) < spacing * STREAMLINE_STEP_ACCEPT_SPACING_MULTIPLIER
                            && !solidMasks[closestIdx])
                        {
                            Vector3d stepVec = finalDirs[closestIdx];
                            if (stepVec.Length < 0.05) break;

                            trackingParticle += stepVec * STREAMLINE_STEP_SIZE;
                            pathVertices.Add(trackingParticle);
                        }
                        else
                        {
                            break; // fell out of the mesh, hit solid, or stalled
                        }
                    }

                    if (pathVertices.Count > 1)
                    {
                        computedStreamlines.Add(new PolylineCurve(pathVertices));
                    }
                };

                if (hasSeedBox)
                {
                    // Every valid, non-solid analysis point inside the box becomes its own seed,
                    // instead of the default sparse every-other-row sampling.
                    for (int i = 0; i < gridPoints.Length; i++)
                    {
                        if (!validMasks[i] || solidMasks[i]) continue;
                        if (!seedBox.Contains(gridPoints[i], true)) continue;
                        traceStreamlineFrom(i);
                    }
                }
                else
                {
                    for (int i = 0; i < gridPoints.Length; i += uCount * STREAMLINE_SEED_ROW_STRIDE)
                    {
                        if (!validMasks[i] || solidMasks[i]) continue;
                        traceStreamlineFrom(i);
                    }
                }
            }

            DA.SetData(0, heatmapMesh);
            DA.SetDataList(1, vectorLines);
            DA.SetDataList(2, vectorColorList);
            DA.SetDataList(3, computedStreamlines);
            DA.SetDataList(4, tagAnchorPoints);
            DA.SetDataList(6, velocityRawData);

            if (terrain != null)
            {
                Mesh cleanMesh = terrain.DuplicateMesh();
                cleanMesh.VertexColors.Clear();
                DA.SetData(5, cleanMesh);
            }

            DA.SetData(7, "URBAN WIND VECTOR ENGINE HT (BETA)\n"
                + "\n"
                + "HOW IT WORKS:\n"
                + "A physically-refined wind analysis engine: occlusion, wake, corner-channeling and gap/tunneling are combined as independent multiplicative factors (rather than an either/or switch), and an optional multi-pass relaxation lets each point settle through sequential building interactions.\n\n"
                + "INTERPRETATION & IMPORTANCE:\n"
                + "Use once massing is known, to check pedestrian comfort, wake shadowing, and speed-up in narrow gaps between buildings before committing to facade or landscape design.");

            sw.Stop();
            if (execute)
            {
                double reportedMin = minObservedSpeed == double.MaxValue ? 0.0 : minObservedSpeed;
                double reportedMax = maxObservedSpeed == double.MinValue ? 0.0 : maxObservedSpeed;
                double finalComfortPercent = activeSensorCount > 0 ? ((double)comfortablePointCount / activeSensorCount) * 100.0 : 0.0;
                Message = $"{this.NickName}\nTime: {sw.ElapsedMilliseconds} ms\n---\n● Min Speed: {reportedMin:F1} m/s\n○ Max Speed: {reportedMax:F1} m/s\n● Comfort: {finalComfortPercent:F1}% (≤ {COMFORT_SPEED_THRESHOLD_MS:F1} m/s)";
            }
            else
            {
                Message = $"{this.NickName}\nSTATUS: SLEEPING";
            }
        }
    }
}
