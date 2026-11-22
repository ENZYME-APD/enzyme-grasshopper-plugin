using System.Collections.Generic;
using System.Text;

namespace Enzyme.Utils
{
    /// <summary>
    /// Method notes: one source for a component's Info output and the "Method, assumptions and
    /// simplifications" section of its docs page (dev_scripts/gen_docs.py reads this file).
    /// Keys are the component's C# file name without .cs. Keep strings plain: no braces, no quotes.
    /// </summary>
    public static class MethodNotes
    {
        public sealed class Note
        {
            public string Title;
            public string[] Method;
            public string[] Assumptions;
            public string[] Simplifications;
            public string[] Workflow;
            public string[] References;
        }

        /// <summary>Plain-text block for a component's Info output.</summary>
        public static string Info(string key)
        {
            if (!All.TryGetValue(key, out Note n)) return "";
            var sb = new StringBuilder();
            sb.AppendLine(n.Title.ToUpperInvariant());
            Section(sb, "METHOD", n.Method);
            Section(sb, "ASSUMPTIONS", n.Assumptions);
            Section(sb, "SIMPLIFICATIONS AND LIMITS", n.Simplifications);
            Section(sb, "WORKFLOW", n.Workflow);
            Section(sb, "REFERENCES", n.References);
            return sb.ToString().TrimEnd();
        }

        private static void Section(StringBuilder sb, string title, string[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            sb.AppendLine();
            sb.AppendLine(title + ":");
            foreach (var l in lines) sb.AppendLine("- " + l);
        }

        public static readonly Dictionary<string, Note> All = new Dictionary<string, Note>
        {
            ["SunHoursAnalysis"] = new Note
            {
                Title = "Sun Hours Analysis",
                Method = new[]
                {
                    "Each test point casts one ray towards every sun vector from the Heliodon, starting 0.01 m off the point.",
                    "A ray that hits the Context mesh is blocked. On massing (Workflow 1), rays from faces turned away from the sun are not counted.",
                    "Tree crowns are porous: a ray through foliage keeps a fraction T = exp(−k × LAD × L) of the sun, where L is the length of the ray inside each crown (from its crossings of the closed crown mesh) and LAD the leaf area density of that crown (Beer-Lambert law).",
                    "Sun Hours = sum of the transmitted fractions over all sun vectors, rounded; Exposure = that sum / number of sun vectors.",
                    "Tree Shade = 1 - (sun with trees / sun without trees) for the rays not blocked by Context.",
                },
                Assumptions = new[]
                {
                    "Each sun vector stands for an equal time step (the Heliodon samples the period evenly), so counts read as hours.",
                    "Leaf area density is uniform inside a crown; k = 0.5 matches a spherical (random) leaf-angle distribution.",
                    "Tree crowns are closed meshes and are not also in Context.",
                },
                Simplifications = new[]
                {
                    "Direct sun only: no diffuse sky, no reflections, no light scattered by leaves.",
                    "Trunks and branches are ignored; overlapping crowns add their optical depths.",
                    "Crowns that are not closed meshes are ignored (warning).",
                },
                Workflow = new[]
                {
                    "Heliodon -> Sun Vectors. Massing or Test Points -> Geometry or Test Points. Buildings and terrain -> Context.",
                    "Tree crowns (for example Site Fetch Tree Crowns) -> Trees; LAD per crown or one value; lower LAD for winter or sparse species.",
                },
                References = new[]
                {
                    "Monsi M. and Saeki T. (1953), light extinction in plant canopies (Beer-Lambert law).",
                    "Campbell G.S. and Norman J.M. (1998), An Introduction to Environmental Biophysics: k = 0.5 for spherical leaf angles.",
                    "Fujiwara K. et al. (2025), VoxCity, Computers, Environment and Urban Systems 102366: porous canopy approach.",
                },
            },

            ["SiteFetch"] = new Note
            {
                Title = "Site Fetch",
                Method = new[]
                {
                    "Local frame: UTM zone of the site centre (Krüger series, WGS84); the centre is moved to 0,0 so Rhino keeps full precision. Georeference gives the EPSG code and the origin easting and northing.",
                    "Terrain: Copernicus DEM GLO-30 tiles (1 degree, Cloud-Optimised GeoTIFF) read with HTTP range requests, so only the needed tiles and blocks are downloaded; heights are sampled bilinearly at every grid vertex.",
                    "Land cover: ESA WorldCover 2021 v200 (10 m) sampled at each cell centre (nearest pixel) and coloured with the official class colours.",
                    "Buildings and trees: OpenStreetMap through the Overpass API (ways and multipolygon relations tagged building, nodes tagged natural=tree), with a margin round the site.",
                    "Building height = height tag; else building:levels × Level Height (+ half of roof:levels); else Default Height. min_height or building:min_level lifts the underside.",
                    "Each building is a closed extrusion standing at the lowest terrain level under its footprint; courtyards (inner rings) are kept.",
                    "Each tree is a closed crown: a sphere of radius min(crown diameter / 2, 0.45 × height) touching the tree top.",
                    "Downloads are cached on disk, so a re-run of the same site reads from the cache.",
                },
                Assumptions = new[]
                {
                    "Copernicus GLO-30 is a surface model (DSM): in towns and forests it includes some building and canopy height, smoothed to 30 m. Treat it as terrain for site-scale studies, not for detailed grading.",
                    "Heights are orthometric (EGM2008 geoid), not ellipsoidal.",
                    "OpenStreetMap completeness and tagging vary by place: untagged buildings get the Default Height (the Dashboard shows the share with real height data).",
                    "A building belongs to the site when the centre of its largest outline is inside the square.",
                    "Land cover is the 2021 snapshot.",
                },
                Simplifications = new[]
                {
                    "Flat roofs: roof shapes are not modelled; building:part pieces are not used.",
                    "Buildings sit on one base level (no stepped bases on slopes).",
                    "Tree crowns are spheres; tree heights and crown diameters default when not tagged.",
                    "Rhino Y is UTM grid north, which differs slightly from true north away from the zone centre.",
                    "Sites up to 5 km square; sea tiles with no DEM get elevation 0.",
                },
                Workflow = new[]
                {
                    "Run once per site (Run gate). Building Mesh -> Context of Sun Hours, Fast CFD, noise. Tree Crowns -> Trees of Sun Hours. Terrain -> Hydro-DEM, Road Corridor.",
                    "Put the Sources text on every drawing or report that uses the data (OpenStreetMap attribution is required).",
                },
                References = new[]
                {
                    "Copernicus DEM GLO-30, ESA / Airbus, distributed on AWS Open Data.",
                    "Zanaga D. et al. (2022), ESA WorldCover 10 m 2021 v200, CC BY 4.0.",
                    "OpenStreetMap contributors (ODbL); Overpass API.",
                    "Karney C.F.F. (2011), Transverse Mercator with an accuracy of a few nanometers (Krüger series).",
                },
            },

            ["HydroDEM"] = new Note
            {
                Title = "Hydro-DEM Engine",
                Method = new[]
                {
                    "Graph: one node per mesh topology vertex, linked to its mesh neighbours.",
                    "Outlets: vertices on the open edge of the mesh (water leaves the model there); a closed mesh drains at its lowest vertex.",
                    "Depression filling (Priority-Flood): a flood grows inward from the outlets, lowest first; every vertex is raised to at least the level it was reached from, so each hollow fills to its spill level.",
                    "Flow direction: steepest descent (drop / plan distance) on the filled surface; on flats and inside filled hollows a vertex drains to the vertex that flooded it, so every vertex reaches an outlet and there are no loops.",
                    "Accumulation: one unit of rain per vertex, summed downstream; Streams follow vertices with accumulation at or above Threshold.",
                    "Ponds: connected wet vertices (depth above Min Pond Depth) at one spill level; volume = depth × the vertex's share of the surrounding faces' plan area.",
                    "Catchment: the outlet vertex each vertex drains to.",
                },
                Assumptions = new[]
                {
                    "Water leaves only at the mesh edge; hollows fill completely before they overflow (static spill level).",
                    "Uniform rain per vertex, no infiltration, evaporation or storage in the soil.",
                    "Use a regular mesh: accumulation counts vertices, so a denser area collects more.",
                },
                Simplifications = new[]
                {
                    "Single flow direction per vertex (D8-style on mesh neighbours): no splitting of flow.",
                    "Accumulation is a vertex count, not an area or a discharge.",
                    "Pond volume is approximate (vertex area share); culverts, pipes and drains are not modelled.",
                    "Fill Pits = false restores the old behaviour: flow stops in every hollow.",
                },
                Workflow = new[]
                {
                    "Terrain mesh (for example Site Fetch Terrain) -> Terrain. Streams -> Keypoint Engine. Ponds and Pond Depth show where water collects.",
                },
                References = new[]
                {
                    "Barnes R., Lehman C. and Mulla D. (2014), Priority-Flood: an optimal depression-filling and watershed-labeling algorithm for digital elevation models, Computers and Geosciences 62.",
                    "O'Callaghan J.F. and Mark D.M. (1984), the extraction of drainage networks from digital elevation data (D8).",
                },
            },

            ["RoadCorridor"] = new Note
            {
                Title = "Road Corridor",
                Method = new[]
                {
                    "Cross sections every Interval along the centreline (and at its end), square to its plan direction.",
                    "Each side of the template is built outward from the axis: an optional rise (kerb upstand), then the part width at its cross slope (negative % falls away from the axis).",
                    "Superelevation on curves: e = min(eMax, V² / 127 R) %, applied to lane parts (outside lane up to +e, inside to -max(crown, e)), smoothed with a moving average over the Runoff Length.",
                    "Daylight: from the outer edge a cut slope rises (or a fill slope falls) at 1 : H until it meets the terrain, found by 0.25 m steps then bisection, up to the template's maximum daylight width.",
                    "Earthwork line: the subgrade (surface minus the pavement layer depth) under paved parts, the finished surface elsewhere. Cut and fill areas integrate the gap to the existing ground every 0.1 m across the section.",
                    "Volumes by average end area between sections; pavement layer volumes and surface areas the same way.",
                    "Mass haul: cumulative (cut × Cut Reuse Factor - fill) along the road; balance points where it crosses zero; haul = area between the mass line and zero (m³ × m); average haul = haul / volume moved; volume beyond free haul = loop height above the level where the loop is Free Haul wide.",
                },
                Assumptions = new[]
                {
                    "The centreline Z is the finished road level on the axis (use Auto-Grade Road for a draped, slope-limited profile).",
                    "Daylight slopes are given as H:V (2 = 1 vertical in 2 horizontal).",
                    "Cut Reuse Factor turns bank cut into usable compacted fill (losses and shrinkage together).",
                    "Preset templates use typical dimensions for early design, not a specific standard: check them against the local street design guide.",
                    "Ground is read by vertical rays onto the terrain mesh; sections off the terrain or beyond the maximum daylight are flagged (partial volumes).",
                },
                Simplifications = new[]
                {
                    "Superelevation ignores side friction (e = V² / 127 R, capped at eMax), which overstates it on gentle curves.",
                    "No prismoidal or curvature correction to average end area; on tight curves the inner sides of sections can overlap and volumes are overstated.",
                    "Topsoil strip and bulking are not separated; earthwork under kerbs uses the kerb bed depth.",
                    "Conditional parts (ditches only in cut) are tested at the part's start.",
                    "Junctions are not handled here: feed Road Network's Corridor Edges so corridors stop at the junction mouths.",
                },
                Workflow = new[]
                {
                    "Road Network -> Corridor Edges -> Auto-Grade Road -> Centreline; Corridor Templates -> Template; terrain -> Terrain.",
                    "Quantities and Dashboard Data -> Report Metrics; Mass Haul curves are drawn below the corridor.",
                },
                References = new[]
                {
                    "AASHTO, A Policy on Geometric Design of Highways and Streets (Green Book): e + f = V² / 127 R.",
                    "Average end area method and mass haul (Brückner) diagram: standard earthworks practice.",
                },
            },

            ["RoadNetworkComponent"] = new Note
            {
                Title = "Road Network",
                Method = new[]
                {
                    "Centrelines are projected to the XY plane and split at every crossing (curve-curve intersection at document tolerance) and wherever an end comes within Snap Tolerance of another centreline; pieces shorter than the tolerance are dropped.",
                    "Ends within the tolerance merge into one node; arms are sorted by heading. Node type by number of arms: dead end, continuation, T, Y (no pair of arms within 35 degrees of straight), crossroads, multi-arm.",
                    "Priority (through) arms: the pair with the highest hierarchy, then the straightest; a junction_priority tag overrides.",
                    "Kerb returns: the kerb-face lines are offset lines of the arms; with corner angle θ and kerb-face offsets w_a and w_b, they meet at x = (w_b + w_a cos θ) / sin θ along the arm, and the return of radius r touches the arm at x + r / tan(θ / 2). Lines further out use concentric returns (radius reduced by their extra offset). Corners of 170 degrees or more run straight.",
                    "Corner radius by the lower hierarchy of the two roads (Corner Radii: local, secondary, main), or junction_radius.",
                    "Mouth = farthest kerb-face tangent point + 1 m; corridors start and end at the mouths. Edges too short for both junctions get shorter mouths (warning).",
                    "Roundabouts and cul-de-sacs: an outer kerb circle of radius R round the node; each arm's kerb face joins it with an entry fillet of radius r (at least 10 m on roundabouts) touching the arm at x = √((R + r)² − (w + r)²); circle arcs between arms; boundary lines concentric; central island R - 5.5 m (mini: 2 m).",
                    "Plan lines are the template's part edges offset from each edge and sampled every 1 m; attributes come from User Text (road_template, road_hierarchy, road_name), override lists, then the defaults.",
                },
                Assumptions = new[]
                {
                    "Hierarchy is main, secondary or local; other values count as local (warning).",
                    "Arms are straight near the junction (their direction at the node).",
                    "Centrelines do not run on top of each other; overlapping stretches are ignored (warning).",
                    "A junction point applies to the nearest node within max(5 m, 3 × Snap Tolerance).",
                    "Default radii (6 / 9 / 12 m) and roundabout sizes (R 15 m, mini R 8 m, cul-de-sac R 9 m) are typical values: check them against local guidance and the design vehicle.",
                },
                Simplifications = new[]
                {
                    "2D only: levels are ignored here (Auto-Grade Road and Road Corridor add them).",
                    "Arms that curve tightly at a junction can leave small kinks where returns meet the offset lines.",
                    "When the two templates at a corner differ, only the kerb face, kerb back and boundary wrap round it; other lines stop at the mouth.",
                    "Footway and verge hatches stop at the mouths; only the carriageway is filled inside junctions.",
                    "Single-lane roundabouts without splitter islands; turning heads are drawn as bulbs; radii are not checked against vehicle swept paths.",
                    "Junction density uses the bounding-box area of the network.",
                },
                Workflow = new[]
                {
                    "Tag centrelines in Rhino (UserText DB): road_template, road_hierarchy, road_name; reference them into Centrelines. Optional junction points with junction_type, junction_radius, junction_priority, junction_name.",
                    "2D plan: Kerb Lines, Edge Lines, Mouth Lines, Junction Areas, Road Areas with Area Types for hatching, Labels.",
                    "3D: Corridor Edges -> Auto-Grade Road -> Road Corridor with Corridor Templates; Node JSON for the junctions.",
                },
                References = new[]
                {
                    "Kerb return geometry: offset-line intersection and tangent fillet (plane geometry).",
                    "Roundabout sizes in the range of single-lane guidance, for example CROW 126 (NL) and DMRB CD 116 (UK).",
                },
            },

            ["AD_ARCHICAD"] = new Note
            {
                Title = "Adapter: Archicad Slabs (Tapir)",
                Method = new[]
                {
                    "All input trees are flattened and matched by index: floor contour i takes FloorHeights i, Programs i, TowerIDs i and BuildingNames i. Missing names default to Mixed_Use, Main_Mass and Building_01. Null items in FloorContours or FloorHeights keep their index: that floor is skipped and later floors keep their own heights.",
                    "Each floor's base level is the lowest Z of its contour's bounding box; its top is base + FloorHeights.",
                    "Floors are grouped by BuildingNames, then by TowerIDs, and sorted by base level within each tower.",
                    "Healing: if the gap between a floor's base and the top of the floor below in the same tower is between 0.001 and SnapTolerance (default 0.15, either above or below), the base is snapped to that top. The top stays where it was, so the floor height grows or shrinks by the gap.",
                    "Each building's true_base_elevation is its lowest healed base; every floor is written as a one-floor block with base_z relative to it (rounded to 0.001) and floor_height = healed height.",
                    "Outlines are written as Polyline points if the curve is a polyline; otherwise the curve is converted to arcs and lines (tolerance 0.05, angle 0.1 rad) and each segment is written as Line, Arc (start, mid, end) or a Polyline approximation (tolerance 0.01). Coordinates are rounded to 4 decimals.",
                },
                Assumptions = new[]
                {
                    "Contours are flat, closed slab outlines in model units (metres), one per floor.",
                    "The flattened lists of contours, heights and names are in the same order.",
                    "Floors of one tower stack vertically; small gaps or overlaps up to SnapTolerance are modelling errors, not design intent.",
                },
                Simplifications = new[]
                {
                    "Data tree structure is ignored: everything is matched by flat index.",
                    "Every floor becomes its own block (floors = 1); repeated floors are not merged.",
                    "Free-form curves are approximated by arcs, lines and polylines.",
                    "Block names use the relative level rounded to whole units, so they can repeat; they are labels only.",
                },
                Workflow = new[]
                {
                    "Archicad slab outlines and heights (through Tapir) -> FloorContours and FloorHeights. JSON_Payload -> JSON_Payload of JSON MP Engine V2.",
                },
            },

            ["AD_CAKE"] = new Note
            {
                Title = "Adapter: The Sliced Cake",
                Method = new[]
                {
                    "Each FloorBreps item is one floor volume. Its bounding box gives the bottom (MinZ) and top (MaxZ); the Brep is cut by a horizontal plane 0.01 m above MinZ and the largest closed section curve is taken as the floor outline (moved back down to MinZ).",
                    "Floors are grouped by BuildingNames, then by TowerIDs, and each tower is sorted bottom to top.",
                    "Gap healing: when the base of a floor is within SnapTolerance (default 0.15 m) of the top of the floor below, above or below it, but more than 1 mm off, its base is snapped to that top. Floor height = MaxZ minus the healed base.",
                    "The building base (true_base_elevation) is the lowest healed base in the building; every floor gets base_z = healed base minus building base, rounded to 1 mm.",
                    "The outline is written as line, arc or polyline segments: the curve is first converted to arcs and lines (0.05 tolerance), then segments that are linear or arcs within 0.001 are stored exactly and anything else is polylined (0.01 tolerance). Coordinates are rounded to 4 decimals.",
                    "Output: one JSON block per floor (floors = 1, floor_height, base_z, program, tower_id, boundary_segments), grouped per building. Block names are Floor_relative Z rounded to 1 m_TowerID; when two floors of a building would get the same name, the later one gets a suffix _2, _3 and so on.",
                },
                Assumptions = new[]
                {
                    "Every Brep is exactly one storey and the list is flat; Programs, TowerIDs and BuildingNames match it item by item (missing items default to Mixed_Use, Main_Mass and Building_01).",
                    "Floors of a tower are vertical extrusions, so the outline 0.01 m above the base represents the whole floor.",
                    "Model units are metres (the 0.01 slice offset and the 0.15 m default tolerance are absolute).",
                },
                Simplifications = new[]
                {
                    "Only the largest closed section is kept: courtyards, holes and second pieces of the same floor are dropped.",
                    "Floors whose slice gives no curve are skipped and listed by item index in a warning.",
                    "Healing changes base_z and floor_height only; the outline geometry keeps its original absolute coordinates, including Z.",
                },
                Workflow = new[]
                {
                    "Floor-by-floor massing Breps from Rhino -> AD_CAKE -> JSON payload for the Masterplan Engine.",
                    "Use AD_LEGO instead when each Brep is a multi-storey block.",
                },
            },

            ["AD_LEGO"] = new Note
            {
                Title = "Adapter: The Lego Builder",
                Method = new[]
                {
                    "For each MassingBlocks Brep, the bounding box gives the bottom and top and the total height; the Brep is cut 0.01 m above its bottom and the largest closed section is taken as the footprint.",
                    "Floor count = whole number of FloorHeights that fit in the block height, with a 1 mm tolerance (a block of 11.9999 m with 4 m floors counts 3 floors); FloorHeights per block, default 4.0 m, zero or negative also falls back to 4.0 m.",
                    "HeightResolution 0 (Strict Cutoff): one block with that many floors; the leftover height is discarded.",
                    "HeightResolution 1 (Stretch Top Floor): if the leftover is more than 0.1 m, the block is split into the standard floors plus one top floor whose height is FloorHeight plus the leftover (program tagged (Top)).",
                    "base_z of each block = block bottom minus the lowest bottom of its building (true_base_elevation), rounded to 1 mm. Footprints are written as line, arc or polyline segments, as in AD_CAKE.",
                    "The JSON payload groups blocks per building name.",
                },
                Assumptions = new[]
                {
                    "Each Brep is a vertical extrusion of one footprint, so the section 0.01 m above its base is valid for all its floors.",
                    "Programs, TowerIDs, BuildingNames and FloorHeights match MassingBlocks item by item; missing items default to Mixed_Use, Main_Mass, Building_01 and 4.0 m.",
                    "Model units are metres.",
                },
                Simplifications = new[]
                {
                    "Blocks lower than one FloorHeight produce no floors and are left out without a warning.",
                    "In Stretch mode a leftover of 0.1 m or less is dropped; in Strict mode all leftover height is dropped.",
                    "Only the largest closed section is kept: holes and extra pieces are lost; setbacks inside a block are ignored.",
                    "boundary_segments keep their absolute world Z while base_z is relative to the building base.",
                },
                Workflow = new[]
                {
                    "Multi-storey massing Breps -> AD_LEGO -> JSON payload for the Masterplan Engine.",
                    "Use AD_CAKE instead when the model is already sliced floor by floor.",
                },
            },

            ["AD_SCULPT"] = new Note
            {
                Title = "Adapter: The Sculptor",
                Method = new[]
                {
                    "Massing Breps are grouped by BuildingNames (default Building_01); each building takes its base level from the lowest bounding-box Z of its Breps and its top from the highest.",
                    "RecipeJSON is a list of objects with floors, program and height (defaults 1, Office, 4.0 m); each object is expanded into that many floors, stacked upwards from the building base. If RecipeJSON cannot be read, a warning is shown and one 4.0 m Mixed_Use floor is used.",
                    "Floor levels are added while the level is more than 0.1 m below the building top; when the recipe runs out the stack stops, unless RepeatLast is True, in which case the last floor type repeats to the top.",
                    "Each Brep is cut by a horizontal plane 0.01 m above every floor level that lies inside its own bounding box (0.05 m tolerance); the section curves are dropped back by 0.01 m.",
                    "Section curves are rationalised to arcs and lines (tolerance 0.05 m, angle 0.1 rad, segments 0.1 to 1000 m) and each segment is written as Line, Arc (start, mid, end) or Polyline (0.01 m tolerance), with coordinates rounded to 4 decimals. A segment that cannot be written as an arc is written as a polyline; if a segment cannot be written at all a warning says the boundary is open.",
                    "Every section becomes one block named Floor_relative level_TowerID (level with up to 2 decimals; repeated names within a building get the suffix _2, _3 and so on), with tower_id, program, floor_height, floors = 1 and base_z relative to the building base; the JSON_Payload lists buildings with true_base_elevation and their blocks.",
                },
                Assumptions = new[]
                {
                    "Model units are metres; the default floor height is 4.0 m.",
                    "TowerIDs and BuildingNames are parallel to Massing; missing entries fall back to Main_Mass and Building_01.",
                    "All Breps of one building share one floor grid measured from the lowest Brep of that building.",
                },
                Simplifications = new[]
                {
                    "One block per section curve and per floor: floors are not merged into multi-floor blocks.",
                    "Free-form section curves are approximated by arcs, lines and polylines.",
                    "Breps that do not reach a grid level inside their own height get no section at that level, so a Brep that starts between grid levels loses its lowest part.",
                },
                Workflow = new[]
                {
                    "Massing Breps (with tower and building names) -> AD_SCULPT -> Masterplan Engine (JSON payload).",
                },
            },

            ["ActivateViewSettings"] = new Note
            {
                Title = "Activate View Settings",
                Method = new[]
                {
                    "When Run is True, the active Rhino viewport restores the Named View called View, switches to the display mode whose English name matches Display Style (case-insensitive) and restores the Layer State with all layer properties.",
                    "Empty inputs leave that setting unchanged; names not found raise a warning and show NOT FOUND in the status.",
                },
                Assumptions = new[]
                {
                    "The names exist in the active Rhino document; right-click menu items can build Value Lists of the available names.",
                },
                Simplifications = new[]
                {
                    "Acts on the active viewport only. Run is not edge-triggered: a Toggle left on re-applies the settings on every solve, so wire a Button.",
                },
            },

            ["AdaptiveTerrainGrader"] = new Note
            {
                Title = "Terrain Grader",
                Method = new[]
                {
                    "Each closed BoundaryCurves pad gets a best-fit plane through its polyline vertices (or 20 division points if it is not a polyline); open curves are ignored. With no pads the input Mesh is passed through unchanged with zero volumes.",
                    "The terrain is sampled by vertical rays on a square grid of MeshResolution over the mesh bounding box. Where the local face is steeper than 30°, two extra samples are added at half a cell in X and in Y. Points every MeshResolution × 0.25 along each pad boundary are added too.",
                    "A sample inside a pad (plan containment test in World XY) is moved onto that pad plane. A sample outside all pads is clamped into a slope envelope: for each pad, Z must lie within padZ ± d × tan(BlendAngle), where d is the plan distance to the nearest point on the pad boundary; the tightest limits over all pads apply.",
                    "ModMesh is a 2D Delaunay triangulation (in World XY) of all the moved samples.",
                    "CutVolume and FillVolume are summed on the base grid: at each cell centre the original terrain is ray-cast and the grading rule (pad plane or slope envelope) is applied at that point; the height difference × MeshResolution² is counted as fill if above +0.01 m or as cut if below −0.01 m. Both are reported as positive volumes.",
                    "ColoredMesh is an unwelded copy of ModMesh. Each face takes FillColor if the mean vertex height difference to the original terrain is above +0.05 m, CutColor if below −0.05 m, and white otherwise.",
                    "With ShowContours on, ModMesh is contoured at every whole model unit between the lowest and highest Z of the original and graded terrains; levels divisible by 5 go to MainContours, the rest to Contours.",
                },
                Assumptions = new[]
                {
                    "Model units are metres: thresholds of 0.01 m and 0.05 m, the 30° refinement angle and the 1 m and 5 m contour intervals are hard-coded.",
                    "Pads are closed curves that are roughly planar; a sloping pad is allowed and follows its fitted plane.",
                    "The terrain is a single-valued height field seen from above (no overhangs); faces wound downwards are treated as upward for the steepness test.",
                    "BlendAngle is the steepest permitted embankment slope for both cut and fill (same value either side).",
                },
                Simplifications = new[]
                {
                    "Embankments are a geometric slope envelope (a cone of slope BlendAngle around the pads), not a designed batter, retaining wall or drainage profile; no soil type, bulking or shrinkage factor is applied.",
                    "The graded surface (ModMesh) is a re-triangulation of grid samples, so away from the pads it is a linear resampling of the original terrain. Volumes are not affected (they apply the grading rule directly), but on rough terrain ColoredMesh can show small cut or fill where nothing was graded.",
                    "Volumes use one sample per grid cell (midpoint rule), so accuracy depends on MeshResolution.",
                    "Overlapping pads are resolved by input order (the first containing pad wins); conflicting envelopes from several pads are not reconciled.",
                },
                Workflow = new[]
                {
                    "Site terrain mesh (for example from Site Fetch) -> Mesh; building pads or platforms as closed curves -> BoundaryCurves.",
                    "ColoredMesh for cut and fill diagrams; CutVolume and FillVolume for an early earthworks balance; Contours and MainContours for drawings.",
                },
            },

            ["AdvancedExportViews"] = new Note
            {
                Title = "Advanced Export Views",
                Method = new[]
                {
                    "On a rising edge of Run it stores all settings and starts a state machine. For each name in GH States (or once, if empty) it sets Current GH State (which holds the state being captured while a capture is pending and keeps the last exported state after the export ends), waits 500 ms, then captures every view in Views (all Named Views if empty; when Views is connected but empty a warning says so) from the active viewport with Rhino ViewCapture at Width × Height.",
                    "Before capturing it optionally switches the active viewport to Display Style and restores the Layer State; afterwards it restores the original camera, display mode and layers.",
                    "Files are named Prefix_State_View_Suffix.ext in Directory (Desktop if empty; created if missing); the DPI value is written as image metadata only.",
                },
                Assumptions = new[]
                {
                    "The 500 ms delay is long enough for the Canvas State Manager and the downstream definition to finish recomputing.",
                    "Named views, display modes and layer states are matched by name (case-insensitive for display modes and layer states, Rhino lookup for views).",
                },
                Simplifications = new[]
                {
                    "Unknown view names are skipped silently. DPI does not change pixel size; Transparent applies to PNG only.",
                    "Output aspect is Width × Height; if it differs from the viewport aspect, ViewCapture shows more or less of the scene than the named view framed.",
                },
                Workflow = new[]
                {
                    "Create Named Views -> Views. GH States list -> GH States; Current GH State -> Load Name of a Canvas State Manager.",
                    "Paper Size To Pixels (Add Default Controls menu) -> Width, Height, DPI. Saved Files -> GIF Compiler or Report Metrics Maps.",
                },
            },

            ["AnalysisDashboard"] = new Note
            {
                Title = "Analysis Dashboard",
                Method = new[]
                {
                    "Parses the Dashboard Data JSON: Type (Discrete, Gradient or Blocks, default Discrete), Title, Colors (R, G, B objects), Labels and Metrics (Name, Value pairs).",
                    "While Run is True it draws a screen-space HUD in every viewport: a dark box (opacity = Transparency) anchored to a corner with Offset X and Offset Y margins in pixels, the title in capitals, one line per metric and a colour legend with the last colour at the top.",
                    "Legend labels: Discrete shows one label per colour; Blocks with one more label than colours shows ranges between consecutive labels; Gradient shows only the minimum and maximum labels.",
                    "If the upstream component that supplies the data is locked (disabled), the payload is ignored and nothing is drawn.",
                },
                Assumptions = new[]
                {
                    "The JSON follows the schema written by the Enzyme analysis components (keys are case-sensitive).",
                },
                Simplifications = new[]
                {
                    "Box width is estimated from character count × 0.65 × Size, so long labels or wide fonts can overflow the box.",
                    "Nothing is drawn when the payload has neither Colors nor Metrics; a payload with Metrics only shows the title and metrics without a legend.",
                },
                Workflow = new[]
                {
                    "Any analysis component Dashboard Data output -> Dashboard Data. Share one Run toggle with the analysis components.",
                },
            },

            ["AnalysisDifference"] = new Note
            {
                Title = "Analysis Difference",
                Method = new[]
                {
                    "Difference = ValuesA minus ValuesB for every vertex or face (detected from the count; A and B must have the same count).",
                    "Area weights: per-face values use the face area; per-vertex values get an equal share (1/3 or 1/4) of the area of every unmasked face they belong to. Masked faces are excluded.",
                    "Stats (area-weighted): mean change, % of area where the change is above +Threshold, below minus Threshold or within it, the largest increase and decrease, and with Level set the % of area above Level in A and in B (strictly greater).",
                    "Colours: Palette (default Balance) sampled at 0.5 + 0.5 × Difference / Range, clamped, so zero sits at the centre. Range defaults to the largest absolute difference; masked cells are grey.",
                    "BetterWhen (Lower or Higher) only renames the increased / decreased lines as improvement or worsening.",
                    "Dashboard Data is a legend payload (gradient, labels minus Range and plus Range, stats as metrics).",
                },
                Assumptions = new[]
                {
                    "A and B were computed on the same mesh with the same vertex or face order.",
                    "Face areas are in model units² and the model is planar enough for triangle area sums to be meaningful.",
                },
                Simplifications = new[]
                {
                    "No statistical significance: every cell counts by area only.",
                    "The Difference list still contains values for vertices touched only by masked faces; they are just excluded from the stats and coloured grey.",
                },
                Workflow = new[]
                {
                    "Two runs of Fast CFD, Sun Hours, Noise or Analysis Heatmap inputs on the same mesh -> ValuesA, ValuesB.",
                    "Difference -> Analysis Heatmap with Min = minus Range and Max = Range for contour bands. Dashboard Data -> Report Metrics or Analysis Dashboard.",
                },
            },

            ["AnalysisHeatmap"] = new Note
            {
                Title = "Analysis Heatmap",
                Method = new[]
                {
                    "Values are matched to the mesh per vertex or per face from their count. Per-face meshes are welded so neighbouring cells share vertices; face values are averaged onto vertices (and vertex values averaged onto faces where needed).",
                    "Optional smoothing: Smooth passes of Laplacian averaging along mesh edges (each vertex = mean of itself and its neighbours); masked faces do not contribute, so values do not bleed across buildings.",
                    "Band edges: without Min and Max, the data range is split into round steps (1, 2, 2.5 or 5 × 10ⁿ) aiming at Levels bands; with Min or Max set, the range is split into exactly Levels equal bands. Classes mode uses the fixed edges of the Scheme.",
                    "Cells: one flat colour per face from the gradient. Smooth: vertex colours blended across faces. Contour and Classes: faces are clipped exactly at the band edges into filled band polygons, and isolines are traced at each inner edge.",
                    "Class schemes: UTCI (−40, −27, −13, 0, 9, 26, 32, 38, 46 °C), Lawson LDDC (4, 6, 8, 10 m/s: sitting to uncomfortable), Noise (45 to 75 dB(A) in 5 dB steps), Slope (5, 15, 30 %), Flood depth (0.1, 0.3, 0.5 m).",
                    "Band areas: in Cells mode each whole face counts in its own band; in the other modes the clipped band polygons are measured. BandStats gives each band as a % of the unmasked area.",
                    "Labels: values are placed along isolines every LabelSpacing (default 15% of the bounding-box diagonal), turned to read left to right and kept apart; Peaks labels local maxima (or minima if negative). Text size defaults to 1.2% of the diagonal.",
                    "Geometry is cached: changing only label inputs or Title does not rebuild the bands.",
                },
                Assumptions = new[]
                {
                    "Mask has one item per face; True means building or no data.",
                    "Values outside a set Min / Max are clamped into the first or last band.",
                    "BandAreas are in model units²; Report Metrics converts them to ha assuming metres.",
                },
                Simplifications = new[]
                {
                    "Per-vertex data shown in Cells mode is the mean of the face's vertices, not an exact cell value.",
                    "Smoothing is a plain graph Laplacian: it is not area-weighted and lowers peaks.",
                    "Isolines and bands assume values vary linearly along each triangle edge.",
                },
                Workflow = new[]
                {
                    "Fast CFD, Sun Hours, Noise, slope or flood results -> Mesh, Values, Mask.",
                    "LabelPlanes, LabelText, LabelSize -> Text Tag 3D. Dashboard Data -> Legend Geometry, Analysis Dashboard or Report Metrics.",
                },
                References = new[]
                {
                    "Blazejczyk K. et al. (2012), An introduction to the Universal Thermal Climate Index (UTCI), Geographia Polonica 85(1): UTCI stress categories.",
                    "Lawson T.V., Lawson (LDDC) pedestrian wind comfort criteria.",
                },
            },

            ["AreaConv"] = new Note
            {
                Title = "Area Converter",
                Method = new[]
                {
                    "Every numeric item of Area is multiplied by 10.7639104 (Conv_Type True, m² to ft²) or divided by it (False, ft² to m²).",
                    "The data tree structure is kept; nulls stay null and items that cannot be read as numbers are passed through unchanged.",
                },
                Assumptions = new[]
                {
                    "Input values are areas in m² or ft²; the factor is 1 m² = 10.7639104 ft² (1 ft = 0.3048 m).",
                },
                Simplifications = new[]
                {
                    "None beyond the fixed conversion factor; no other units (hectares, acres) are handled.",
                },
            },

            ["AutoGrade"] = new Note
            {
                Title = "Auto-Grade Road",
                Method = new[]
                {
                    "The Centerline is divided into equal-length steps of about Resolution (number of steps = floor(length / Resolution), minimum 2).",
                    "Each division point is draped onto the Terrain by a vertical ray cast down from 10 000 m above it; the first hit gives the existing ground level (EG). Points off the mesh keep the Z of the centerline.",
                    "A forward pass then a backward pass clamp each level so the rise or fall to the previous (then next) point is at most Max Slope × horizontal distance. This gives the profile closest to EG that respects the grade limit.",
                    "The profile is smoothed by Smoothing passes of a moving average over ±(steps / 20) neighbours (minimum ±1), with the first and last levels pinned after every pass. A forward and a backward pass then clamp the interior levels again to Max Slope (end levels stay pinned), so the point profile never exceeds Max Slope; smoothing alone could steepen the steps next to the ends.",
                    "3D Road is a degree-3 curve interpolated through the smoothed levels (polyline if that fails); Existing Ground is a polyline through the draped levels.",
                    "The HUD reports the maximum grade of the smoothed point profile and the maximum cut and fill against EG at the division points.",
                },
                Assumptions = new[]
                {
                    "Model units are metres; Max Slope is a ratio (0.08 = 8 %), default 0.08; Resolution default 2 m; Smoothing default 5. Resolution must be positive and Max Slope zero or positive, otherwise the component stops with an error.",
                    "The Terrain is a single surface seen from above (the topmost hit is used) and lies below Centerline Z + 10 000.",
                    "The road ends are meant to meet fixed levels, so the end levels are kept from the slope-clamped profile.",
                },
                Simplifications = new[]
                {
                    "Longitudinal profile only: no cross-fall, road width, earthwork volumes or batters.",
                    "No proper vertical curves (K values, sight distance): smoothing is a plain moving average.",
                    "Cut and fill are checked only at the division points, not along the interpolated curve, which can overshoot slightly between them.",
                },
                Workflow = new[]
                {
                    "Road centerlines (for example from Road Network tools) and a terrain mesh (for example Site Fetch) -> Centerline and Terrain.",
                    "3D Road -> sweep or loft a road section, or compare with Existing Ground to see cut and fill.",
                },
            },

            ["AutoListItem"] = new Note
            {
                Title = "Auto List Item",
                Method = new[]
                {
                    "Returns the item of List at Index. With Wrap Indices on (default, right-click menu) the index wraps around with a modulo, so negative and too-large values cycle; with it off the index is clamped to the first or last item.",
                    "If a Number Slider feeds Index, its maximum is reset to the longest list length over all branches minus 1 after each solution (and its value clamped). Auto-wire Index Slider adds an integer slider 0 to 10 when Index is empty.",
                },
                Assumptions = new[]
                {
                    "List is a flat list per solution; on data trees the component runs once per branch like any list-access component.",
                },
                Simplifications = new[]
                {
                    "None beyond the wrap or clamp rule; an empty List gives no output.",
                },
            },

            ["AutoPhase"] = new Note
            {
                Title = "Auto-Phase Assigner by Z",
                Method = new[]
                {
                    "When Run is True, each referenced Rhino object is read with its User Text keys BuildingID (default Building_01), TowerID (default Main) and Type; objects with Type = core are skipped.",
                    "Blocks are grouped by BuildingID. A block whose TowerID contains podium (any case) is a podium block; the others are grouped by TowerID.",
                    "Podium blocks of a building are sorted by the lowest Z of their bounding box and numbered 0, 1, 2...",
                    "Each tower's blocks are sorted by lowest Z and numbered from the last podium phase + 1 upwards. All towers of a building start from the same number, so towers rise in parallel.",
                    "The number is written to the object's Phase User Text; the run is recorded as one Rhino undo step (Auto-Phase Assigner), so Undo restores the previous Phase values. The Log lists every assignment.",
                },
                Assumptions = new[]
                {
                    "Each object is one block (one stacking step) and its bounding-box bottom is its level.",
                    "Podium blocks are recognised only by the word podium in TowerID.",
                },
                Simplifications = new[]
                {
                    "Blocks at the same Z in one group get consecutive phases in input order, not the same phase.",
                    "All podium blocks of a building form one sequence, whatever their TowerID, so separate podiums are not phased independently.",
                    "Phase is a simple vertical sequence; no construction logic, areas or dates.",
                },
                Workflow = new[]
                {
                    "Referenced massing blocks with User Text -> Guids; Button -> Run.",
                    "Then BIM Attribute Serializer reads the Phase key to stack blocks.",
                },
            },

            ["BimJson"] = new Note
            {
                Title = "BIM Attribute Serializer",
                Method = new[]
                {
                    "Each referenced curve is read with its User Text: BuildingID (default Building_01), Program (Default), TowerID (Main_Tower), FloorHeight (3.5), Floors (1), Phase (0). Numbers are read with invariant culture (dot decimal); values that cannot be read fall back to these defaults with a warning. Non-curves and Type = core objects are skipped.",
                    "Each curve is projected flat to Z = 0, made anticlockwise if closed, and filleted when a Fillet_Config rule applies. Rules are checked in order (type Tower, Program or Building; match text or *; exact default true, otherwise contains; case-insensitive); the first match gives the radius, else default_radius.",
                    "The fillet radius is capped at 0.49 × the shortest segment of the curve so corners do not overlap.",
                    "Per building, blocks are sorted by Phase. A block sits on the top of the previous block of the same tower; the first block of a tower sits on the highest top reached by any earlier phase (0 if none). Top = base + FloorHeight × Floors.",
                    "The JSON lists buildings (name, true_base_elevation = lowest curve Z of the building) and blocks (name Program_index, tower_id, program, floor_height, floors, base_z relative to the building base, boundary_segments).",
                    "Segments are written as Line (start, end), Arc (start, mid, end) or Polyline (points from a 0.01 tolerance conversion), coordinates rounded to 4 decimals. Arc segments that cannot be read as an exact arc are written as a Polyline approximation.",
                    "When PushToRhino is True, each source curve is moved vertically so its lowest point sits at building base + base_z.",
                },
                Assumptions = new[]
                {
                    "Footprints are curves, one per block, with stacking data in Attribute User Text.",
                    "Phase order defines construction and stacking order; blocks of one tower are listed in stacking order within a phase.",
                    "Model units are metres.",
                },
                Simplifications = new[]
                {
                    "Heights are uniform per block (FloorHeight × Floors); no slab thickness or transfer levels.",
                    "Boundary coordinates are flat (Z = 0); elevation is carried only by base_z and true_base_elevation.",
                    "Free-form segments are approximated by polylines.",
                },
                Workflow = new[]
                {
                    "Auto-Phase Assigner by Z first, to write Phase keys.",
                    "JSON_Payload -> a file writer or the masterplan JSON tools downstream.",
                },
            },

            ["BranchConcat"] = new Note
            {
                Title = "Branch-Concat",
                Method = new[]
                {
                    "For each branch of tree, converts every item to text (null items become None), joins them with sep and adds prefix and suffix; the result keeps the branch path.",
                },
                Assumptions = new[]
                {
                    "Items have a meaningful text form (the Grasshopper ToString of the item).",
                },
                Simplifications = new[]
                {
                    "None beyond text conversion: numbers use the default Grasshopper formatting; empty branches give prefix + suffix.",
                },
            },

            ["BranchSizeSplit"] = new Note
            {
                Title = "BranchSizeSplit",
                Method = new[]
                {
                    "MinItems and MaxItems are truncated to integers.",
                    "Each branch of Tree goes to Tree A if its item count is below MinItems, to Tree B if it is between MinItems and MaxItems (both inclusive), otherwise to Tree C. Paths are kept.",
                },
                Assumptions = new[]
                {
                    "Item count includes null items in a branch.",
                },
                Simplifications = new[]
                {
                    "None beyond truncating the thresholds to whole numbers.",
                },
            },

            ["CWP"] = new Note
            {
                Title = "CWProfile",
                Method = new[]
                {
                    "Builds a closed A × B rectangle in the world XY plane centred on the origin (defaults A = 0.3, B = 0.5).",
                    "If R > 0 the corners are filleted with Rhino CreateFilletCornersCurve and the result is converted back to a polyline; arcs are approximated by segments using the document tolerance and angle tolerance (minimum segment 0.01).",
                    "The profile is then moved by (dA, dB, 0).",
                },
                Assumptions = new[]
                {
                    "The profile is drawn on world XY at the origin, ready to be placed by a sweep (for example as DetailVertical or DetailHorizontal in CurtainWallSystem).",
                },
                Simplifications = new[]
                {
                    "Output is always a polyline, so filleted corners are faceted.",
                    "If R is larger than half the smaller side, or filleting fails, sharp corners are used with a warning.",
                },
                Workflow = new[]
                {
                    "CWP -> DetailVertical, DetailHorizontal or DetailCorner of CurtainWallSystem.",
                },
            },

            ["CWS"] = new Note
            {
                Title = "CurtainWallSystem",
                Method = new[]
                {
                    "Each planar base curve is extruded straight up by its height to give one uncapped glass surface. Heights are matched to curves by branch path and by index; the last height repeats; 3.0 is used when no height is given. Base curves with a height of 0 or less are skipped with a warning.",
                    "The curve is split into its segments. Along each segment the GridVertical pattern is repeated from the segment start (Align Start), centred (Middle) or pushed to the end (End); mullion positions strictly between the segment ends become vertical mullions. Curved segments use CurvedGridVertical instead, when given.",
                    "Transom heights come from the GridHorizontal pattern repeated from the base (always Start alignment); heights at 0 or at the top are skipped. Each transom is a copy of the segment moved up to that height.",
                    "Corner mullions are placed at every C2 discontinuity of the base curve, at its ends, and at extra points found per segment: every NURBS span end and every place where the sampled curvature (100 samples) changes by more than 10 % or switches between straight and curved.",
                    "Each member is built from a closed profile curve when one is supplied (DetailVertical, DetailHorizontal, DetailCorner): the profile, drawn on world XY at the origin, is oriented to the rail start and swept along the rail (Sweep1). Otherwise the rail is extruded along the facade normal by VertExt (mullions and corners) or HorExt (transoms).",
                    "The facade normal is tangent × world Z. Transoms use the average of 11 normals along the segment; corners use the average of the adjacent segment normals.",
                },
                Assumptions = new[]
                {
                    "Base curves are planar and horizontal; extrusion is always along world Z.",
                    "Curve direction sets the normal: tangent × Z points to the right of the curve direction, which is outwards for a counter-clockwise closed curve.",
                    "Model units are metres (defaults 1.5 m grids, 3.0 m height, 0.5 m and 0.3 m fin depths).",
                    "Profile curves are closed and drawn on world XY around the origin.",
                },
                Simplifications = new[]
                {
                    "Without profiles, mullions and transoms are single zero-thickness surfaces (fins), not solids.",
                    "Glass is one surface per base curve, not split into panels by the grid, and has no thickness.",
                    "Transom ribbons on curved segments are extruded along one averaged normal, so their depth varies along the curve.",
                    "Mullions at segment ends are not generated by the grid; those positions rely on the corner mullions.",
                    "On free-form curves the span-end and 10 % curvature rules can add many corner mullions that are not real corners.",
                },
                Workflow = new[]
                {
                    "Floor outlines and floor-to-floor heights (for example from a massing tool) -> BaseCurves and Heights.",
                    "CWProfile -> DetailVertical, DetailHorizontal, DetailCorner for detailed sections.",
                },
            },

            ["CWT"] = new Note
            {
                Title = "CWT",
                Method = new[]
                {
                    "Only closed, planar curves are used. Each one is divided into n equal-length parts, n = max(4, ceil(length / modSize)), so the real spacing is length / n (at most modSize).",
                    "At each division point the horizontal direction perpendicular to the curve is found (curve-plane normal × tangent) and turned outwards using the outline orientation (counter-clockwise = interior on the left), so concave L, U and courtyard plans are handled; the area centroid is used only if the orientation cannot be found.",
                    "A divLine runs from the point outwards by tDepth.",
                    "A transom is a flat rectangular surface on that line, rising tHeight along the curve-plane normal (turned to point up).",
                    "modSize, tDepth and tHeight are matched by branch path and item index; a single value applies to all; missing values use defaults 1.0 m, 0.2 m and 0.1 m; curves with a non-positive modSize are skipped with a warning. Outputs keep the input path plus the curve index.",
                },
                Assumptions = new[]
                {
                    "Curves are closed floor or facade outlines drawn in a horizontal plane; units are metres.",
                },
                Simplifications = new[]
                {
                    "Transoms are zero-thickness surfaces, not solid profiles.",
                    "No corner handling: fins sit only at equal divisions, not at the curve's corners.",
                },
                Workflow = new[]
                {
                    "Floor outlines (for example Facade JSON Parser BoundsClosed) -> curves; transoms -> Custom Preview or bake.",
                },
            },

            ["CanvasAuditor"] = new Note
            {
                Title = "Canvas Auditor",
                Method = new[]
                {
                    "Counts all objects on the canvas: Enzyme components (category Enzyme), objects with an Error message, outdated ones (Obsolete or placeholder for a missing plug-in), disabled ones (Locked) and preview-on ones (preview-capable and not hidden).",
                    "When any input is True, the selection is cleared and every object in the chosen groups is selected; the Report lists the counts.",
                },
                Assumptions = new[]
                {
                    "Buttons are wired to the inputs, so a selection is made once per click.",
                },
                Simplifications = new[]
                {
                    "Warnings are not counted as errors; groups, panels and other objects are included in Total Objects.",
                },
            },

            ["CanvasStateManager"] = new Note
            {
                Title = "Canvas State Manager",
                Method = new[]
                {
                    "On a rising edge of Save, the Locked (disabled) and Hidden (preview off) flags of every other object on the canvas are stored under Save Name, keyed by object instance GUID.",
                    "On a rising edge of Load, or when Load Name changes while Load is held true, a solution is scheduled that restores the stored flags. With Turn Off New Nodes, objects not in the stored state get their preview turned off (they are not disabled).",
                    "States are saved inside the Grasshopper file and can be copied or pasted as JSON from the component menu; Export JSON outputs them. States can also be merged from another State Manager through the Import JSON input; they are merged when the input text changes, replacing states of the same name.",
                },
                Assumptions = new[]
                {
                    "Objects keep their instance GUIDs; copied or re-created components count as new nodes.",
                },
                Simplifications = new[]
                {
                    "Only Locked and Hidden are stored; wires, values and positions are not.",
                },
            },

            ["CanvasStyle"] = new Note
            {
                Title = "Canvas Style",
                Method = new[]
                {
                    "Sets the global Grasshopper skin colours for panel background, canvas background and canvas grid from the three colour inputs and refreshes the canvas.",
                },
                Assumptions = new[]
                {
                    "The colours apply to the whole Grasshopper application, not only this document.",
                },
                Simplifications = new[]
                {
                    "None beyond setting three skin colours; nothing is restored when the component is deleted.",
                },
            },

            ["CanvasUsageInfo"] = new Note
            {
                Title = "Canvas Usage Info",
                Method = new[]
                {
                    "When Run is True, lists every non-core plug-in library used by objects on the canvas (name and version).",
                    "Counts every canvas object except groups, by category, subcategory and name, and writes the lists to two panels named PlugIns Used and All Components Used (created at fixed canvas positions if missing, otherwise updated). The report header shows the date, time and Windows user of the report run (not of the last save).",
                },
                Assumptions = new[]
                {
                    "Panels are found by their nickname; renaming them makes the tool create new ones.",
                },
                Simplifications = new[]
                {
                    "Counts include parameters, panels and sliders, not only components; the two report panels the component creates are not counted.",
                },
            },

            ["ChangePulse"] = new Note
            {
                Title = "Change Pulse",
                Method = new[]
                {
                    "Compares the text form (ToString) of Data with the value stored at the previous solution; with a list input, each item is compared with the item at the same index in the previous solution.",
                    "If it differs from the stored value, or Data becomes empty or returns after being empty, Pulse is True and a new solution is scheduled 5 ms later, in which Pulse returns to False.",
                    "The first value after the file opens or the component is placed only initialises the memory and does not pulse.",
                },
                Assumptions = new[]
                {
                    "The text form of the data changes when the data changes; true for numbers, text and booleans.",
                },
                Simplifications = new[]
                {
                    "Geometry is compared by its text form, which is usually just the type name, so geometry edits are not detected.",
                },
                Workflow = new[]
                {
                    "Number Slider or Value List -> Data; Pulse -> a Save or Export button input.",
                },
            },

            ["Clearance"] = new Note
            {
                Title = "Masterplan Clearance Engine",
                Method = new[]
                {
                    "Each outline is represented by the centre of its bounding box.",
                    "Method 0 (Delaunay): the centres are triangulated in plan (Grasshopper Delaunay, needs at least 3 outlines) and each triangle edge is a candidate neighbour pair.",
                    "Method 1 (Proximity): pairs whose bounding boxes, grown by SearchRadius, overlap are candidates; a pair is skipped when any other centre lies closer to its mid-point than 0.85 × half the centre distance (a view-blocking test).",
                    "For each pair the clearance is the shortest distance between the two curves (curve closest points); pairs farther than SearchRadius are dropped.",
                    "Category = 0 if distance < L1, 1 if L1 <= distance <= L2, 2 if greater; LabelPlanes sit at the mid-point of each clearance line, rotated to read left to right; touching outlines (0 m clearance) get an unrotated World XY label plane.",
                    "Each pair is listed once, in the branch of the first outline of the pair.",
                },
                Assumptions = new[]
                {
                    "Outlines are closed plan curves at the same level; distances are 3D, so curves at different heights include the vertical offset.",
                    "Model units are metres (defaults SearchRadius 200, L1 50, L2 100).",
                },
                Simplifications = new[]
                {
                    "Neighbours are chosen from bounding-box centres, not true centroids or the outline shapes, so long or L-shaped towers can miss a near neighbour.",
                    "Overlapping or touching outlines give 0 m clearance; no check for containment.",
                    "With fewer than 3 outlines the Delaunay method returns nothing without a warning.",
                },
                Workflow = new[]
                {
                    "Tower footprints -> Outlines. Lines and Categories -> custom preview or Analysis Dashboard; LabelPlanes and Distances -> text tags.",
                },
                References = new[]
                {
                    "Delaunay B. (1934), Sur la sphère vide: Delaunay triangulation.",
                },
            },

            ["ColorMeshTiles"] = new Note
            {
                Title = "Pixel Gradient",
                Method = new[]
                {
                    "For every cell curve in Grid Cells, the bounding-box centre is read on the chosen axis (0 = X, 1 = Y, 2 = Z). These values are normalised between the minimum and maximum over all cells in all branches to a parameter t from 0 to 1.",
                    "If jitter_pct is above 0, a random offset uniformly in ± jitter_pct / 200 is added to t; t is clamped and the colour is picked by stepping: index = floor(t × number of gradient_colors). There is no blending between colours.",
                    "If accent_color is set and accent_pct is above 0, each cell is replaced by the accent colour with probability accent_pct / 100.",
                    "Each cell polyline is scaled about its bounding-box centre by inset_factor (clamped 0 to 1) and meshed as a triangle fan from that centre; if that centre would give flipped triangles (concave cells) the closed outline is triangulated instead. Cells of the same colour in a branch are merged into one mesh, tagged Tile n (n = position of the colour in gradient_colors) or Accent Tile.",
                    "When bake_trigger is true, meshes are baked to Rhino on layers Tiles::HexFacade_TileN with a matching layer material, grouped per branch, with user strings ElefrontBakeName, Surface_Path, Surface_ID and Material_Tag. With bake_name set, objects previously baked with the same bake_name are deleted first and every solution re-bakes; with bake_name empty it bakes once each time bake_trigger turns on.",
                },
                Assumptions = new[]
                {
                    "Grid cells are closed polylines (at least 3 distinct vertices); other curves are skipped.",
                    "Cells are simple (non self-intersecting) closed outlines; convex cells use the fan, concave ones the outline triangulation.",
                    "The random generator uses a fixed seed (42), so the same inputs always give the same pattern.",
                },
                Simplifications = new[]
                {
                    "Colour depends only on the cell centre position along one world axis, not on distance along the facade or on the surface parametrisation.",
                    "The random pattern changes if cells are added, removed or reordered, because each cell draws from one shared random sequence in input order.",
                },
                Workflow = new[]
                {
                    "Facade grid cells (for example from a panelling component) -> Grid Cells; a list of swatches -> gradient_colors.",
                    "panel_tags and the HUD counts give a tile schedule per colour.",
                },
            },

            ["ColorPalette"] = new Note
            {
                Title = "Color Palette",
                Method = new[]
                {
                    "Looks up Name in a fixed table of palettes, each stored as 7 to 11 RGB stops (Viridis, Cividis, Magma, Turbo, Ladybug, ColorBrewer and cmocean sets, among others). Matching ignores case, spaces and hyphens; a trailing _r or reversed flips it; a number picks a palette by index.",
                    "Reverse flips the stops; with Count >= 2 (max 1024) the gradient is resampled to Count evenly spaced colours by linear interpolation in RGB, otherwise the palette's own stops are returned.",
                },
                Assumptions = new[]
                {
                    "Colours are used low to high, matching the order of the analysis values.",
                },
                Simplifications = new[]
                {
                    "Stops are samples of the published colour maps; resampling is linear in RGB, not in a perceptual colour space, so very fine resampling is only approximately perceptually even.",
                },
                Workflow = new[]
                {
                    "Color Palette Colors -> Colors or CustomColors input of any analysis component.",
                },
                References = new[]
                {
                    "Nuñez J.R., Anderton C.R. and Renslow R.S. (2018), Optimizing colormaps with consideration for color vision deficiency, PLOS ONE: Cividis.",
                    "Thyng K.M. et al. (2016), True colors of oceanography, Oceanography 29(3): cmocean palettes.",
                    "Harrower M. and Brewer C.A. (2003), ColorBrewer.org, The Cartographic Journal 40(1).",
                    "Moreland K. (2009), Diverging color maps for scientific visualization: Coolwarm.",
                },
            },

            ["CondFillet"] = new Note
            {
                Title = "Conditional Fillet",
                Method = new[]
                {
                    "Each curve is split into its segments; at each corner the turning angle θ between the incoming and outgoing tangents is measured. Corners with θ < 0.05 rad (about 3°) and the two ends of open curves get radius 0.",
                    "The allowed tangent length is L = threshold_pct × the shorter adjacent segment length (threshold_pct is a fraction, capped at 0.49). The radius limit is min(L, L / tan(θ/2)).",
                    "If radius exceeds the limit, the corner is skipped (skip True, radius 0) or the radius is clamped to the limit (skip False). With round_dec the radius is rounded down to that many decimals.",
                    "Fillet arcs are made with Rhino CreateFilletCurves (tolerance 0.001), the segments are trimmed at the arc ends and everything is joined (join tolerance 0.01). If a fillet cannot be created at a corner, the corner stays sharp and its radius is reported as 0 with a warning; if the parts cannot be joined into one continuous curve, the input curve is returned unchanged with a warning and all its radii are 0.",
                    "radii lists the radius chosen for every corner, one branch per input curve.",
                },
                Assumptions = new[]
                {
                    "threshold_pct is a fraction of the segment (0.25 = 25 %), not a percentage.",
                    "Curves are made of segments (polylines, polycurves); a single smooth curve has no corners and is returned unchanged.",
                },
                Simplifications = new[]
                {
                    "The radius is also capped at L itself, which is more conservative than the geometric limit for corners turning less than 90°.",
                    "Fixed absolute tolerances (0.001 and 0.01) are used regardless of model units.",
                },
            },

            ["ConfigJson"] = new Note
            {
                Title = "Master JSON Config Builder",
                Method = new[]
                {
                    "Reads three parallel lists: Programs, Targets and Colors, matched by index.",
                    "JSON_Targets maps each program name (trimmed) to its target area; targets of zero or less, and programs without a target, are left out.",
                    "JSON_Palette maps each program name to its colour as an R, G, B array (alpha dropped).",
                },
                Assumptions = new[]
                {
                    "Program names are unique and match the program names used in the masterplan payload; targets are in m².",
                },
                Simplifications = new[]
                {
                    "A repeated program name keeps the last value. Lists of different lengths are allowed (a warning shows in the status) and unmatched items are skipped.",
                },
                Workflow = new[]
                {
                    "JSON_Targets -> TargetJSON of Masterplan Dashboard. JSON_Palette -> ColorPalette of JSON MP Engine V2.",
                },
            },

            ["ContactEnzyme"] = new Note
            {
                Title = "Contact Enzyme",
                Method = new[]
                {
                    "Draws two buttons on the component. Visit Website opens https://www.weareenzyme.com/ in the default browser; Send Email opens a mailto link to hello@weareenzyme.com with the subject Enzyme Grasshopper Plugin and the Message text as the body.",
                },
                Assumptions = new[]
                {
                    "A default browser and mail client are configured on the machine.",
                },
                Simplifications = new[]
                {
                    "None beyond opening the links; nothing is sent automatically.",
                },
            },

            ["CreateNamedViews"] = new Note
            {
                Title = "Create Named Views",
                Method = new[]
                {
                    "When Run is True, for each Locations point it places the active viewport camera there, aims it at the matching Targets point (one target for all, or targets cycled by index) and sets the 35 mm lens length (default 50 mm).",
                    "Each camera is saved as a Rhino Named View called Prefix + a 4-digit index (Shot_0000, Shot_0001...); an existing view with the same name is replaced. The original viewport camera is restored afterwards. View Names lists Prefix + 4-digit index for every location whether or not Run is True; views are only written while Run is True.",
                },
                Assumptions = new[]
                {
                    "Views are saved from the active viewport, so its projection (perspective) and size are used.",
                    "When camera and target coincide, the camera looks along +Y.",
                },
                Simplifications = new[]
                {
                    "Named view changes are not added to Rhino's undo.",
                },
                Workflow = new[]
                {
                    "Camera path points (e.g. Divide Curve) -> Locations; a focus point -> Targets.",
                    "View Names -> Views of Advanced Export Views.",
                },
            },

            ["CustomTextTag"] = new Note
            {
                Title = "Custom Text Tag",
                Method = new[]
                {
                    "Builds a 3D text on Plane with Text, Size and Justification (0 to 8: bottom, middle, top rows; left, centre, right columns).",
                    "The plane origin is first moved by Move along the plane's X, Y or Z axis (chosen from the right-click menu, Z by default).",
                    "Tags are drawn in the viewport in Colour; there are no outputs.",
                },
                Assumptions = new[]
                {
                    "Size is in model units.",
                },
                Simplifications = new[]
                {
                    "Preview only: the text cannot be baked or passed on from this component.",
                },
            },

            ["Dash"] = new Note
            {
                Title = "Masterplan Dashboard",
                Method = new[]
                {
                    "Parses JSON Payload (keys programs, total_area and buildings) and draws a text table in the foreground of every Rhino viewport while Run is True.",
                    "Global mode lists each program area; Group By Bldg lists program areas and a subtotal per building, then a target tracking section. Areas are formatted with two decimals and comma thousands separators in invariant culture, whatever the Windows locale, plus Suffix.",
                    "Transparency is the background opacity: 0 = fully transparent, 1 = opaque (default 0.8).",
                    "Target JSON maps program names (case-insensitive) to target areas; each program shows value / target and %, in red when it exceeds the target. Target Global does the same for the total.",
                },
                Assumptions = new[]
                {
                    "The payload follows the Masterplan Engine summary schema: programs as name to area, buildings as name to an object with programs and total_area.",
                },
                Simplifications = new[]
                {
                    "Box width is estimated from character counts, so long labels or proportional fonts can overflow.",
                    "Display only: there are no outputs.",
                },
                Workflow = new[]
                {
                    "Masterplan Engine summary JSON -> JSON Payload; a panel with program targets -> Target JSON.",
                },
            },

            ["DataVisualizer"] = new Note
            {
                Title = "Data Visualizer",
                Method = new[]
                {
                    "Values are normalised with the minimum and maximum of the drawn values (the first N values, N = number of points): t = (v - min) / (max - min); if all values are equal the range is set to 1.",
                    "Size = Domain start + t × Domain length; colour = linear RGB interpolation between Colors spaced evenly over t (white if no colours).",
                    "Type 0 draws a hexagonal prism of height Size and circumradius Bar Thickness (cycled per point, so a bar is about twice that wide), standing upright in world Z. Type 1 draws a flat hexagon of radius Size in a world XY plane. Type 2 (and any other value) draws an icosahedron of circumradius Size.",
                    "All shapes are joined into one vertex-coloured mesh with computed vertex normals; Values and the interpolated Colors are also output per point.",
                },
                Assumptions = new[]
                {
                    "Points and Values are in the same order; extra points or values are ignored.",
                },
                Simplifications = new[]
                {
                    "Spheres are 20-face icosahedra and bars are hexagonal prisms, for speed.",
                    "Shapes are aligned to world axes, not to any surface.",
                },
                Workflow = new[]
                {
                    "Any analysis that outputs points and values -> Points and Values.",
                },
            },

            ["DivideTargetSegment"] = new Note
            {
                Title = "Divide Target Length by Segment",
                Method = new[]
                {
                    "Each curve is split into its segments with DuplicateSegments (polycurve segments, polyline edges), and every segment is further split at its interior kinks (G1 discontinuities); otherwise the whole curve is one segment.",
                    "Each segment is divided into n = round(length / Target Length) equal-length parts, at least 1, so parts are between about 0.67 and 1.5 × Target Length for short segments and closer to it for long ones.",
                    "Outputs per curve (one branch per input curve): division points without repeats at segment joints, the sub-curves and their lengths.",
                },
                Assumptions = new[]
                {
                    "Segments join end to start; Target Length is in model units and greater than 0.001.",
                },
                Simplifications = new[]
                {
                    "Parts are equal within a segment but differ between segments. On a closed curve the last point repeats the first.",
                },
            },

            ["ElevationLabel"] = new Note
            {
                Title = "Elevation Label",
                Method = new[]
                {
                    "For each input point it draws a vertical leader line of Length above the point and places a text entity at Length + Gap above it.",
                    "The text is the LabelText for that item (or the last text of the matching branch), otherwise the point Z formatted to 2 decimals.",
                    "The text plane is World XY, XZ (default) or YZ, rotated by Orientation degrees about its own normal; Anchor 1 to 9 sets the justification (default 2, bottom centre).",
                    "Text height comes from the Rhino dimension style named in Style (text height × dimension scale), otherwise 2.0 model units.",
                    "When Bake is True it deletes earlier objects carrying the same BakeName user string, then bakes every leader and text to BakeLayer. BakeLayer may be a nested path such as Parent::Child; missing levels are created. Without a BakeName the component uses its own id, so each bake replaces its previous bake instead of adding duplicates.",
                },
                Assumptions = new[]
                {
                    "Z values are read in model units relative to the model origin; no datum or sea-level offset is applied.",
                    "Length, Gap, Style, TextPlane, Orientation and Anchor are read per point: the matching branch and item, or the last one available (a single value applies to all points). Bake, BakeLayer and BakeName apply to all points.",
                },
                Simplifications = new[]
                {
                    "Leaders are always vertical; labels do not avoid each other.",
                },
                Workflow = new[]
                {
                    "Points on terrain or levels -> Points. Optional custom text -> LabelText.",
                },
            },

            ["ExportViews"] = new Note
            {
                Title = "Export Named Views",
                Method = new[]
                {
                    "When Run is True it collects the Named Views given in Views (all named views if empty), optionally sets the active viewport to Display Style and restores Layer State.",
                    "For each view it pushes the view onto the active viewport, captures it with Rhino ViewCapture at Width × Height pixels (grid, world axes and CPlane axes on or off, optional transparent background for PNG and TIFF only, ignored for JPG and BMP which have no alpha channel) and saves it as png, jpg, bmp or tif with DPI written as metadata.",
                    "Files are named Prefix_view name_Suffix in Directory (Desktop if empty, created if missing); afterwards the original view, display mode and layer state are restored.",
                },
                Assumptions = new[]
                {
                    "The active viewport is the one to use; Display Style and Layer State names match existing ones (case-insensitive).",
                },
                Simplifications = new[]
                {
                    "The capture uses the active viewport projection with the requested pixel size, so a view with a different aspect ratio is cropped or extended. Exports run on every solution while Run stays True.",
                },
                Workflow = new[]
                {
                    "Paper Size To Pixels -> Width, Height, DPI.",
                },
            },

            ["FacadeDist"] = new Note
            {
                Title = "Facade Module Distributor",
                Method = new[]
                {
                    "WITHDRAWN (2026-11-22): the node is hidden from the toolbar and no longer downloads or runs any script; placed copies show an error. The notes below describe the old behaviour.",
                    "Closed curves from Base_Curves are collected into one flat list; open curves are ignored.",
                    "In production mode the Python file facade_modules.py is downloaded from the ENZYME-APD/Grasshopper-GitHub-test repository (main branch) to the user folder .rhinocode/gh_distributed_scripts when missing or when Force_Update is True. In Dev_Mode it is read from Local_Repo_Dir.",
                    "The script text is cached in memory and re-read only when its file modification time changes.",
                    "The script is run in Rhino's Python engine, calling generate_horizontal_bands with band_thickness 0.30 and division_count 3, and generate_storefront with mullion_spacing 1.5 and glass_inset 0.05; Floor_Heights defaults to a single 4.0.",
                    "Its results are output as Horizontal_Bands, Storefront_Mullions and Storefront_Glass.",
                },
                Assumptions = new[]
                {
                    "The geometry logic lives entirely in the external Python script; this component only loads and runs it.",
                    "Model units are metres (fixed 0.30, 1.5, 0.05 and 4.0 values).",
                },
                Simplifications = new[]
                {
                    "Band thickness, division count, mullion spacing and glass inset are hard-coded and cannot be changed from Grasshopper.",
                    "Data trees are flattened; outputs are flat lists.",
                },
            },

            ["FacadeParser"] = new Note
            {
                Title = "Facade JSON Parser",
                Method = new[]
                {
                    "Expects JSON of the form building -> program -> list of floors, each floor with tower_id, floor_index, true_z, height, BoundsClosed and BoundsExt (segment lists of Line, Arc or Polyline).",
                    "Buildings, programs, towers and levels are filtered. Text filters match if any entry matches: ExactMatch True needs equal text (case-insensitive); otherwise the entry is contained in the name, or used as a wildcard pattern with * and ?.",
                    "Segments are rebuilt and joined (0.01 tolerance); BoundsClosed takes the first joined curve and closes it if open; floors without a BoundsClosed polygon give a null item (kept for index alignment) and a warning. All curves are moved up by true_z.",
                    "For every exterior curve one item is output in each tree: the curve, the closed outline, the floor height and a label building | tower | program - Lvl n. Branch paths are (building index; program index).",
                },
                Assumptions = new[]
                {
                    "The JSON is a facade export with the keys above (not the BIM Attribute Serializer payload).",
                    "Segment coordinates are relative to the floor level; true_z gives the absolute level.",
                },
                Simplifications = new[]
                {
                    "Missing true_z or height read as 0; missing floor_index reads as -1.",
                    "Branch indices count only the buildings and programs that pass the filters, so paths shift when filters change.",
                },
                Workflow = new[]
                {
                    "Facade JSON text -> Facade_JSON; BoundsExt and Heights -> facade modules such as Facade Module: Vertical Fins or Rhythmic Balconies.",
                },
            },

            ["FalseStartToggle"] = new Note
            {
                Title = "False Start Toggle",
                Method = new[]
                {
                    "A standard Boolean Toggle whose value is forced to False whenever the document is opened (on read).",
                },
                Assumptions = new[]
                {
                    "Used as the Run input of heavy components so they do not run on file open.",
                },
                Simplifications = new[]
                {
                    "None beyond a normal toggle: the saved state is ignored on open.",
                },
                Workflow = new[]
                {
                    "False Start Toggle -> Run of analysis components.",
                },
            },

            ["FastCFD"] = new Note
            {
                Title = "Fast CFD",
                Method = new[]
                {
                    "A regular plan grid of CellSize cells (minimum 0.5 m, at most 100,000 cells) covers BoundaryMask or the terrain. Each cell centre is draped onto TerrainMesh at AnalysisHeight; cells whose draped point is inside a closed ContextMeshes mesh are solid.",
                    "Inflow speed = WindSpeed × (AnalysisHeight / RefHeight)^0.25 (power law, only when AnalysisHeight < RefHeight), in the direction of WindVector or of Direction and North.",
                    "The initial field is the inflow scaled by a terrain factor 1 + 0.5 × slope along the wind, clamped to 0.7 to 1.4.",
                    "Each time step solves 2D incompressible flow in the style of Stam's Stable Fluids: semi-Lagrangian advection (Courant number 0.5), implicit Jacobi diffusion with Viscosity (10 passes) using the same physical time step as advection (Δt = 0.5 × CellSize / speed, at most 0.5 s), so Viscosity (m2/s) and Friction act independently of CellSize and wind speed, and pressure projection by red-black SOR (ω = 1.6, 12 to 40 passes) with zero velocity in solid cells and zero-gradient pressure at their faces.",
                    "Inflow is imposed on the two upwind domain edges and zero-gradient outflow with p = 0 on the downwind edges; ground drag multiplies speeds by exp(−Friction / max(0.5, AnalysisHeight) × Δt) each step. A per-cell Friction list is read column by column: columns from minimum X, and within each column from minimum Y; the last value repeats.",
                    "Steps = max(Iterations, min(250, 3 × cells along the longest side)); after that many steps the run stops early once the largest change between two consecutive full steps, on a sample of cells, is below 0.005 m/s.",
                    "Turbulence intensity per cell comes from a Smagorinsky estimate (Cs = 0.12): k = (0.12 × CellSize × strain rate)², σ = √(2k/3), TI = σ / speed; a gust speed = speed × (1 + 1.2 × TI) is classed with Lawson-style limits 4, 6, 8 and 10 m/s. ComfortThreshold is compared with the mean speed, so the two percentages are not directly comparable.",
                    "Outputs: coloured WindMesh (0 to 1.5 × inflow speed), points, vectors and speeds outside buildings and inside the mask, VertexSpeeds (NaN in buildings), BuildingMask, Dashboard Data and Info.",
                },
                Assumptions = new[]
                {
                    "Model units are metres and the wind speed is a mean speed measured at RefHeight (default 10 m).",
                    "The power-law exponent 0.25 fits suburban or wooded terrain; open or dense urban terrain would need another exponent.",
                    "Buildings are closed meshes; anything shorter than AnalysisHeight above the terrain is not an obstacle.",
                    "One steady wind direction and speed are analysed.",
                },
                Simplifications = new[]
                {
                    "2.5D: one horizontal layer at pedestrian height. No vertical flow, so downwash from tall buildings, roof overflow and thermal effects are missing.",
                    "Not a validated CFD: no turbulence model in the momentum equations, coarse grids, and comfort classes from one wind scenario instead of an annual exceedance analysis.",
                    "Gust factor speed × (1 + 1.2 × TI) is a heuristic and is not the gust-equivalent mean used by Lawson LDDC criteria.",
                    "The terrain slope factor only sets the initial field; inflow and advection replace it as the domain is flushed.",
                    "Cells where the ray misses the terrain are kept as open flow cells at the domain floor level.",
                },
                Workflow = new[]
                {
                    "Terrain mesh -> TerrainMesh; building meshes -> ContextMeshes; EPW or Wind Rose direction -> Direction.",
                    "WindMesh and VertexSpeeds -> Analysis Heatmap or Analysis Difference; Dashboard Data -> Analysis Dashboard.",
                },
                References = new[]
                {
                    "Stam J. (1999), Stable Fluids, SIGGRAPH 99: semi-Lagrangian advection and pressure projection.",
                    "Smagorinsky J. (1963), General circulation experiments with the primitive equations, Monthly Weather Review 91(3).",
                    "Lawson T.V. (2001), Building Aerodynamics, Imperial College Press: pedestrian comfort criteria.",
                },
            },

            ["FastNoiseAnalyzer"] = new Note
            {
                Title = "Fast Noise Analyzer",
                Method = new[]
                {
                    "For each receiver (every vertex of AnalysisMeshes, moved Offset along its normal, plus the AnalysisPoints) the level from each emitter is Lp = Power - 20 log10(d) - 0.0028 × d, with d the straight distance (min 1 m).",
                    "Emitters more than 45 dB below the loudest free-field contribution at that receiver are added without screening.",
                    "Ground effect (only with Ground): A_gr = G × max(0, 4.8 - (2 h_m / d)(17 + 300 / d)), with h_m the mean height of source and receiver above the Ground mesh.",
                    "If the straight path hits HardCtx, the barrier term comes from a height raster of the obstacle tops (about 500 × 500 cells, at least 0.5 m): the path over the tops is the upper convex hull of the profile, δ = its extra length, N = 2δ / λ with λ = 0.69 m (500 Hz), Dz = 10 log10(3 + 10 N) as in ISO 9613-2 with C2 = 20, C3 = 1 and Kmet = 1, capped at 20 dB (one edge) or 25 dB (several). The larger of Dz and A_gr is used. If no top is found, 5 dB.",
                    "If the path is not screened and crosses SoftCtx, the length through foliage gives 0 dB below 10 m, 1 dB for 10-20 m and 0.05 dB/m up to 200 m.",
                    "Contributions are added as energy: L = 10 log10(Σ 10^(Li/10)).",
                    "Mesh colours span the min to max of the run; Info and Dashboard report max, min, energetic and arithmetic averages over vertices.",
                },
                Assumptions = new[]
                {
                    "Power is the A-weighted level at 1 m of each point (default 85 dB(A)); roads come as point sets from Noise Emitter From Curves.",
                    "One 500 Hz band stands for broadband traffic noise; air absorption 2.8 dB/km (20 °C, 70 % RH).",
                    "Model units are metres; terrain screens only if it is also in HardCtx.",
                },
                Simplifications = new[]
                {
                    "No reflections, reverberation or weather effects (wind and temperature gradients).",
                    "Diffraction only over the top of obstacles, never around their sides.",
                    "Ground effect uses one G for the whole site and the 3D distance; foliage is ignored on paths already screened.",
                },
                Workflow = new[]
                {
                    "Noise Emitter From Curves -> Emitters and Power; buildings -> HardCtx; tree crowns -> SoftCtx; terrain -> Ground.",
                    "Mesh_dB or Pts_dB -> Point Probe or Analysis Dashboard.",
                },
                References = new[]
                {
                    "ISO 9613-2:1996, Attenuation of sound during propagation outdoors, Part 2: general method of calculation.",
                    "ISO 9613-1:1993, calculation of the absorption of sound by the atmosphere.",
                    "ISO 9613-2:1996, Acoustics: attenuation of sound during propagation outdoors, Part 2 (barrier term, C2 = 20).",
                    "Maekawa Z. (1968), Noise reduction by screens, Applied Acoustics 1(3).",
                },
            },

            ["FilletRules"] = new Note
            {
                Title = "Fillet Rule Configurator",
                Method = new[]
                {
                    "Pairs RuleTypes, RuleMatches and RuleRadii item by item (up to the shortest list), with ExactMatch per rule (True if missing), and writes JSON with default_radius and the ordered rules list.",
                    "Lists of different lengths raise a warning (only the shortest count of rules is written); rule types are normalised to Tower, Program or Building ignoring case, and unknown types raise a warning.",
                },
                Assumptions = new[]
                {
                    "RuleTypes are Tower, Program or Building (written with that capitalisation, as BIM_JSON compares them case-sensitively); radii are in metres.",
                },
                Simplifications = new[]
                {
                    "Unknown types are written as given and ignored downstream. BIM_JSON applies the first matching rule and * matches everything.",
                },
                Workflow = new[]
                {
                    "Fillet Rule Configurator -> BIM_JSON fillet rules input.",
                },
            },

            ["FlowHeat"] = new Note
            {
                Title = "Flow Accumulation Heatmap",
                Method = new[]
                {
                    "Every vertex of each FlowPaths polyline is snapped to the nearest TerrainMesh vertex, and that vertex's hit count goes up by one.",
                    "Intensity = min(1, √(hits / max hits) × VisualScale); vertex colours blend from light grey (220, 220, 220) to blue (10, 50, 255).",
                    "VertexCounts gives the raw hit count per mesh vertex, in vertex order.",
                },
                Assumptions = new[]
                {
                    "Flow paths are polylines (for example from the Raindrop Engine) that lie on or near the terrain mesh.",
                },
                Simplifications = new[]
                {
                    "Counts polyline vertices, not path length or upstream area: densely sampled paths weigh more, and the result is a visual proxy, not a hydrological flow accumulation.",
                    "Curves that are not polylines are skipped with a warning; each point counts at its nearest vertex however far away it is.",
                },
                Workflow = new[]
                {
                    "Raindrop Engine flow lines -> FlowPaths; same terrain mesh -> TerrainMesh.",
                },
            },

            ["GifCompiler"] = new Note
            {
                Title = "GIF Compiler",
                Method = new[]
                {
                    "When Run is True it collects every PNG in Directory whose name starts with Prefix, sorts them by file name and writes them as frames of a looping animated GIF named Prefix + Animation.gif in the same folder.",
                    "Frame delay = 1000 / FPS ms (integer), 8-bit colour quantisation.",
                },
                Assumptions = new[]
                {
                    "Frame order follows file names, so frame numbers must be zero-padded (e.g. 0001).",
                },
                Simplifications = new[]
                {
                    "Only PNG files are read. GIF timing is stored in 1/100 s, so the real frame rate is rounded (24 fps plays at about 25 fps).",
                    "Colours are reduced to 256 per frame.",
                },
                Workflow = new[]
                {
                    "Advanced Export Views or a turntable capture -> Directory and Prefix.",
                },
            },

            ["GlobalFloodEngine"] = new Note
            {
                Title = "Global Flood Engine",
                Method = new[]
                {
                    "Every mesh vertex starts with a water depth d = Rainfall × Duration / 1000 × Runoff Coefficient (in m; Rainfall in mm/h, Duration in h). Coefficients are a single value or one per mesh vertex.",
                    "Each iteration, every wet vertex (d > 0.1 mm) finds the connected neighbour with the lowest water level (ground Z + depth). If it is lower, half the level difference is moved to it, limited to the water the vertex holds.",
                    "With Boundary Condition 1 (open), the water at naked-edge vertices is set to 0 after every iteration, so it drains off the model. With 0 (closed) it stays.",
                    "The loop stops after Iterations steps or when no water moved.",
                    "WaterDepths are the final depths per mesh vertex; FloodMesh is coloured light grey below 0.01 m and from cyan to dark blue by depth relative to the maximum.",
                },
                Assumptions = new[]
                {
                    "Every vertex stands for the same plan area, so moving depth between vertices conserves volume only on an evenly spaced mesh.",
                    "All rain falls at once at the start; Duration only scales the total depth.",
                    "Z is in metres and the mesh is a terrain (one height per plan location).",
                },
                Simplifications = new[]
                {
                    "No infiltration over time, no flow velocity, roughness or travel time; Iterations are not time steps.",
                    "Water moves at most one mesh edge per iteration, to one neighbour only, so on large or fine meshes the result depends on Iterations and may not reach equilibrium.",
                    "No drainage network, buildings or culverts.",
                },
                Workflow = new[]
                {
                    "Terrain Mesher or a site mesh -> TerrainMesh.",
                    "WaterDepths with AnalysisPoints -> legend, dashboard or threshold filters.",
                },
            },

            ["GradientGenerator"] = new Note
            {
                Title = "Gradient Generator",
                Method = new[]
                {
                    "Outputs Steps colours spread evenly from the first to the last input colour, interpolating linearly in RGB between neighbouring input colours.",
                    "With no Colors, uses blue to red; Steps below 1 become 1.",
                },
                Assumptions = new[]
                {
                    "Input colours are listed from low to high.",
                },
                Simplifications = new[]
                {
                    "Alpha is dropped (opaque output); RGB interpolation, not perceptual.",
                },
            },

            ["GridTypeValueList"] = new Note
            {
                Title = "Grid Types",
                Method = new[]
                {
                    "A preset value list with four grid types: Rectangular, Offset Rectangular, Hexagonal and Triangular, output as the strings rectangular, offset_rectangular, hexagonal and triangular.",
                },
                Assumptions = new[]
                {
                    "The receiving facade component accepts these exact lower-case strings.",
                },
                Simplifications = new[]
                {
                    "None beyond the fixed list.",
                },
                Workflow = new[]
                {
                    "Grid Types -> the grid type input of Enzyme facade grid components.",
                },
            },

            ["GroupByKey"] = new Note
            {
                Title = "GroupByKey",
                Method = new[]
                {
                    "Walks Keys and List together; each new key (compared by its plain value, so text A and A match) opens a new group in order of first appearance. A single list gives paths 0, 1, 2...; with several input branches, groups are written to (input path; group index). Null keys form their own group (null in Unique Keys).",
                    "Each value goes into its key's branch; Unique Keys lists the keys in the same order.",
                },
                Assumptions = new[]
                {
                    "Keys and List have the same length (otherwise error).",
                },
                Simplifications = new[]
                {
                    "Numbers are compared exactly (1.0 and 1.0000001 are different keys); geometry keys compare by reference.",
                },
            },

            ["GroupColours"] = new Note
            {
                Title = "Group Colours",
                Method = new[]
                {
                    "For every group on the canvas, the key is the group nickname in lower case, cut at the first -, _, : or | and trimmed.",
                    "If the key equals a Group Names entry (case-insensitive), the group gets the colour with the same index (the last colour if the list is shorter); otherwise it gets Default Color.",
                    "Fully transparent colours are replaced by grey (100, 100, 100) for matches and by 214, 206, 206 for the default.",
                },
                Assumptions = new[]
                {
                    "Group nicknames start with one of the names, for example Analysis - Sun.",
                },
                Simplifications = new[]
                {
                    "Groups are recoloured only when the component solves, so new or renamed groups change colour at the next solve.",
                },
            },

            ["Heliodon"] = new Note
            {
                Title = "Enzyme Heliodon",
                Method = new[]
                {
                    "For the 21st of each month in Months (day of year 21, 52, 80 ... 355) it computes sun positions every 0.5 h on the half-hour grid, from the first half hour at or after the start of Hours to its end, in local standard time. Months is read from start to end; a start after the end wraps over the new year (11 to 2 = November to February).",
                    "Sun position uses the NOAA fractional-year formulas: equation of time and declination, true solar time = clock time + equation of time + 4 × Longitude − 60 × Time Zone (minutes), hour angle, then altitude and azimuth.",
                    "Positions with Z > 0.01 (altitude above about 0.6°) are kept; whole-hour positions become Sun Vectors (unit vectors pointing to the sun) and Sun Points at Radius around Center.",
                    "Daily Arcs are degree-3 curves through the half-hourly positions of each day; Base Circle is the horizontal circle of Radius.",
                },
                Assumptions = new[]
                {
                    "Model +Y is true north and +X east unless North is set: North (degrees, counter-clockwise from +Y, Ladybug convention) rotates the vectors, arcs and points about Z.",
                    "Time Zone is the standard offset from GMT; daylight saving is not applied.",
                    "Each vector stands for one hour on one representative day per month.",
                },
                Simplifications = new[]
                {
                    "Twelve representative days, not every day of the year: summing vectors gives hours on those days, not annual sun hours.",
                    "No atmospheric refraction; position accuracy is that of the NOAA approximation (around a few tenths of a degree).",
                },
                Workflow = new[]
                {
                    "Heliodon Sun Vectors -> Sun Hours Analysis or other shading tools.",
                },
                References = new[]
                {
                    "NOAA Global Monitoring Division, General Solar Position Calculations (fractional-year equations).",
                },
            },

            ["InitKeys"] = new Note
            {
                Title = "BIM Key Initializer",
                Method = new[]
                {
                    "Reads the referenced Rhino object IDs (Guid, Guid text or referenced geometry) and, when Run is True, adds default user text keys to each object: BuildingID = Building_01, TowerID = Main_Tower, Program = Residential, Phase = 0, Floors = 1, FloorHeight = 4.0.",
                    "Only keys that do not exist yet are written; existing values are never overwritten. The IDs are passed through for the serializer.",
                },
                Assumptions = new[]
                {
                    "The inputs are Rhino-referenced objects (Type Hint Guid); objects not found in the active document are skipped and counted.",
                },
                Simplifications = new[]
                {
                    "Defaults are fixed in code; edit the values afterwards in Rhino or with the UserText tools.",
                    "Changes are committed directly and are not grouped as one undo step.",
                },
                Workflow = new[]
                {
                    "Referenced curves or Breps (Guid hint) -> Guids. Guids -> the JSON Masterplan serializer.",
                },
            },

            ["JsonInspect"] = new Note
            {
                Title = "JSON Metadata Inspector",
                Method = new[]
                {
                    "Parses MP_JSON as a JSON object whose top-level keys are building IDs.",
                    "If a building value is an object, its keys are read as program names and each array item is scanned for tower_id, floor_index and type. If it is an array, each item is scanned for tower_id, program, programs_above, type and floor_index.",
                    "Unique values are returned sorted: Buildings, Towers, Programs, Types and Levels (integers); floor_index values that are not whole numbers are skipped with a warning.",
                },
                Assumptions = new[]
                {
                    "The JSON follows the MP Engine masterplan schema (building, then program, then floor items).",
                },
                Simplifications = new[]
                {
                    "Only two nesting levels are read; other keys are ignored.",
                },
                Workflow = new[]
                {
                    "MP Engine JSON text -> MP_JSON; outputs -> value lists for filtering the JSON Masterplan components.",
                },
            },

            ["KeylinePattern"] = new Note
            {
                Title = "Keyline Engine",
                Method = new[]
                {
                    "Each Guide Curve is projected vertically onto Terrain and output.",
                    "For i = 1 to Count, the guide is offset in the World XY plane by ± Spacing × i (sharp corners, tolerance 0.01) and each offset is projected vertically (along Z) onto Terrain.",
                    "All projected curves form Keylines; the HUD and Dashboard Data report the number of curves and their total 3D length.",
                },
                Assumptions = new[]
                {
                    "Guide Curves are planar in plan (for example contours or a drawn keyline); offsets are computed in World XY.",
                    "Spacing is a horizontal distance in model units (metres assumed in the dashboard text).",
                },
                Simplifications = new[]
                {
                    "Lines are equal plan offsets of the guide, not true keyline geometry: no fall or grade is computed or controlled along each line, and they do not follow the contours.",
                    "Offsets that self-intersect on tight curves are kept as returned by Rhino; offsets that fall off the terrain are dropped.",
                },
                Workflow = new[]
                {
                    "Terrain mesh -> Terrain; a contour or keyline through the keypoint (for example from a contour component) -> Guide Curves.",
                    "Keylines -> drawings or swale layout; Dashboard Data -> Analysis Dashboard.",
                },
                References = new[]
                {
                    "Yeomans P.A. (1958), The Challenge of Landscape: the keyline system.",
                },
            },

            ["KeypointFinder"] = new Note
            {
                Title = "Keypoint Engine",
                Method = new[]
                {
                    "Each stream curve is used as a polyline (curves are converted at 0.1 tolerance); streams with fewer than 4 vertices are skipped. Streams drawn uphill (start lower than end) are reversed first, so slopes are measured downstream.",
                    "The slope of each segment is (Z start - Z end) / horizontal length, positive downhill.",
                    "Slopes are smoothed with a moving average over ±Smoothing segments (default 2).",
                    "The keypoint is the vertex where the smoothed slope drops most from one segment to the next (largest steep-to-flat change). One keypoint per stream; if the smoothed slope never decreases downstream, no keypoint is returned for that stream (remark).",
                    "The terrain is cut by a horizontal plane at the keypoint level; the contour closest to the keypoint is the Master Keyline if it lies within 5 model units.",
                },
                Assumptions = new[]
                {
                    "Stream curves are 3D and lie on the terrain, as from Hydro-DEM.",
                    "Model units are metres (the 5 unit search distance).",
                },
                Simplifications = new[]
                {
                    "The keypoint is the largest single drop in slope, not a true inflection (sign change of curvature); one per stream curve.",
                    "The keyline is the whole contour at that level, not trimmed to the valley and shoulders.",
                },
                Workflow = new[]
                {
                    "Hydro-DEM streams + the same terrain mesh -> Streams and Terrain; Master Keylines -> offset for keyline ploughing patterns.",
                },
                References = new[]
                {
                    "Yeomans P.A. (1958), The Challenge of Landscape: the development and practice of keyline.",
                },
            },

            ["LayerMat"] = new Note
            {
                Title = "Assign Layer Materials",
                Method = new[]
                {
                    "When RunScript is True, each layer in LayerNames (found by full path, else by short name; all layers if empty) that has no render material gets one.",
                    "The material is named after the layer full path (UseFullPath True) or short name; an existing material with that name is reused.",
                    "A new material takes the layer colour as diffuse colour, transparency = 1 - alpha / 255, white specular and shine 30.",
                    "Before each solve, a Value List wired to LayerNames is refilled with all document layers (name shown, full path as value) when the layer list changes.",
                },
                Assumptions = new[]
                {
                    "Layer colours carry the intended material colour.",
                },
                Simplifications = new[]
                {
                    "Layers that already have a material are skipped and never updated, even if the layer colour changed.",
                    "Creates basic legacy Rhino materials, not physically based ones. In short-name mode, layers with the same name share one material.",
                },
            },

            ["LegendGeometry"] = new Note
            {
                Title = "Legend Geometry",
                Method = new[]
                {
                    "Reads a legend payload from Color Legend: the standard JSON string (Title, Type, Colors, Labels, SubLabels) from analysis components, or two older object formats (slope threshold and height range).",
                    "Gradient legends: a strip 1 × Scale wide and 0.5 × Scale high is split into 10 to 40 swatches (4 per colour stop) coloured by linear interpolation between all stops; the first and last Labels go at the left and right ends and the first SubLabel below the centre.",
                    "Blocks, Binary and Discrete legends: one swatch per colour. With one label more than colours, labels sit on the band edges; with one label per colour (more than 2 colours for Blocks, any number for Binary and Discrete) they sit under each block, staggered on two rows; otherwise the first and last labels go at the ends. Binary and Discrete legends also show their first SubLabel below.",
                    "Outputs the swatch rectangles, labels, label points and colours, and previews filled swatches with Arial text 0.2 × Scale high. The title sits above the strip at Base Point.",
                },
                Assumptions = new[]
                {
                    "Legend is drawn in the World XY plane at Base Point; Scale is in model units.",
                    "Other legend types are not drawn: only the title appears.",
                },
                Simplifications = new[]
                {
                    "Text is a viewport preview only; the Labels and Label Positions outputs are needed to bake text.",
                    "If the JSON cannot be parsed a single error label is returned.",
                },
                Workflow = new[]
                {
                    "Dashboard Data or Color Legend output of an analysis component (Terrain Slope, wind, streamlines, etc.) -> Color Legend.",
                },
            },

            ["MakePalette"] = new Note
            {
                Title = "JSON Palette Builder",
                Method = new[]
                {
                    "Pairs Programs and Colors by index up to the shorter list and writes an indented JSON object mapping each trimmed program name to its RGB triple (0 to 255). Empty names are skipped; a repeated name keeps the last colour.",
                },
                Assumptions = new[]
                {
                    "Programs and Colors are in matching order; alpha is dropped.",
                },
                Simplifications = new[]
                {
                    "A length mismatch is only flagged in the HUD; the extra items are ignored. The HUD count shows the shorter list length, including skipped empty names.",
                },
                Workflow = new[]
                {
                    "Program names and swatches -> JSON_Palette -> JSON masterplan engine components (for example Masses JSON Parser block colours).",
                },
            },

            ["MapToGradient"] = new Note
            {
                Title = "Map to Gradient",
                Method = new[]
                {
                    "Each value is normalised to t = (value - T0) / (T1 - T0) of the Source Domain, from its start to its end, and clamped to 0-1; a reversed domain (for example 10 to 0) flips the gradient. Without a domain the minimum and maximum of the values are used, ignoring NaN and infinite values.",
                    "NaN values get the first colour and a warning.",
                    "t picks a position along the colour list and the colour is linearly interpolated in RGB between the two neighbouring colours (default blue, cyan, lime, yellow, red).",
                },
                Assumptions = new[]
                {
                    "Colours are listed from low to high values.",
                },
                Simplifications = new[]
                {
                    "Linear RGB blend; values outside the domain take the end colours.",
                },
            },

            ["MassParser"] = new Note
            {
                Title = "Masses JSON Parser",
                Method = new[]
                {
                    "Parses Masses_JSON as an object of buildings, each holding a list of blocks with program, tower_id, color [r,g,b], total_height, true_z and boundary.",
                    "Buildings, towers and programs are filtered by the Filter inputs: case-insensitive, exact when ExactMatch is true, otherwise substring match or wildcard match with * and ?. Empty filters keep everything.",
                    "The boundary is rebuilt from Line, Arc (start, mid, end) and Polyline segments, joined with tolerance 0.01 and closed if needed (within tolerance, otherwise with a straight closing line). Blocks without a usable boundary are reported in a warning. Its area is computed before it is moved up by true_z.",
                    "Each block is extruded upwards by total_height from true_z with caps into a Brep, whatever the boundary orientation. Colours get alpha = (1 − Transparency) × 255. Each block goes to branch path building index ; block index, with a label building | tower | program.",
                },
                Assumptions = new[]
                {
                    "Boundaries are closed planar curves in plan, drawn at Z = 0 before the true_z shift.",
                    "Heights and coordinates are in model units; missing colour defaults to grey 200, missing height to 0.",
                },
                Simplifications = new[]
                {
                    "Blocks are straight vertical extrusions of one outline; no setbacks, floors or roof shapes.",
                    "If the joined segments form several pieces, only the first is used.",
                    "Block paths use the original block index (gaps where blocks are filtered out), while the building index counts only matching buildings.",
                },
                Workflow = new[]
                {
                    "Masses JSON from the JSON masterplan engine (with colours from JSON Palette Builder) -> Masses_JSON.",
                    "Volumes and Colors -> Custom Preview; Areas and Heights -> area schedules.",
                },
            },

            ["MasterplanEngine"] = new Note
            {
                Title = "JSON MP Engine V2",
                Method = new[]
                {
                    "Reads the buildings payload: each block has tower_id, program, floor_height (default 4.0), floors (default 1), base_z and a boundary; the boundary is rebuilt from Line, Arc and Polyline segments and moved down to Z = 0.",
                    "Floor indices: within each tower, all floor levels (base_z + j × floor_height, rounded to 0.001) are sorted and numbered from 0.",
                    "Slabs: for each level of each tower, the outlines of the blocks below that end there and of the floors inside blocks are unioned (tolerance 0.01). A block's own base level is only used at ground level or in towers whose id contains podium. Slab area comes from planar Breps.",
                    "Roofs: the slab is intersected with the union of all blocks of the building that continue above that level. If that overlap is under 0.1 m² the slab is a Roof Top; if slab area minus overlap exceeds 1.0 m² it is a Setback Roof (Podium Roof in podium towers). roof_area = slab area - overlap.",
                    "Railings: the exposed roof outline (slab minus overlap) is split into segments and a segment is kept when its midpoint is more than 0.05 from the outlines above.",
                    "Facades: for each floor of a block, a segment of the block outline is exterior when its midpoint lies within 0.05 of the slab outline at that level; these are output as BoundsExt with the closed outline as BoundsClosed.",
                    "Masses and Dashboard: gross area = block outline area × floors, summed per program and per building. Colours come from ColorPalette, else a pseudo-random colour (70 to 199 per channel) seeded with a stable FNV-1a hash of the program name, so it is the same in every session. Blocks whose boundary is not a closed planar curve count 0 m² and raise a warning.",
                },
                Assumptions = new[]
                {
                    "Model units are metres: the fixed tolerances 0.001, 0.01 and 0.05 and the thresholds 0.1 m² and 1.0 m² are absolute.",
                    "Block outlines are flat, closed and non-self-intersecting; true Z = true_base_elevation + relative level.",
                    "Gross area is the full outline area of every floor (no deductions for cores, voids or double-height spaces).",
                },
                Simplifications = new[]
                {
                    "If a curve boolean fails at 0.001 it is retried at 0.05; if the difference still fails the whole slab is treated as exposed, so railings run round the full outline.",
                    "Only the first joined curve of a boundary is kept, so multi-loop boundaries lose their extra loops.",
                    "Exterior and railing tests use segment midpoints only, so partly shared segments are all-or-nothing.",
                    "In non-podium towers a block's own base level is not in the slab outlines, so facade edges of setbacks (or one-floor blocks) at that level can be missing from BoundsExt.",
                    "Palette or payload errors are caught: an invalid palette is ignored silently; an invalid payload gives one error message and empty outputs.",
                },
                Workflow = new[]
                {
                    "AD_ARCHICAD or AD_SCULPT -> JSON_Payload. Master JSON Config Builder JSON_Palette -> ColorPalette.",
                    "Masses, Slab, Roof, Facade and Railings JSON -> the matching Masses, Slab, Roof, Facade and Railing JSON Parsers. Dashboard_JSON -> Masterplan Dashboard.",
                },
            },

            ["MeshColorReset"] = new Note
            {
                Title = "Reset Mesh Colors",
                Method = new[]
                {
                    "Duplicates each mesh and clears its vertex colours, so Custom Preview can apply its own colour.",
                },
                Assumptions = new[]
                {
                    "None beyond valid meshes.",
                },
                Simplifications = new[]
                {
                    "None: geometry, normals and texture coordinates are kept.",
                },
                Workflow = new[]
                {
                    "Coloured analysis mesh -> Reset Mesh Colors -> Custom Preview.",
                },
            },

            ["MeshHeightAnalysis"] = new Note
            {
                Title = "Terrain Height",
                Method = new[]
                {
                    "Works on mesh topology vertices, branch by branch of TargetMeshes.",
                    "A vertex is a candidate peak if no direct neighbour is higher by more than 0.0001, and a candidate valley if none is lower.",
                    "Candidates are then checked against all vertices within SearchRings topological rings (default 5): a peak must still be the highest and stand at least ProminenceLimit (default 0.5) above the lowest vertex in that neighbourhood; a valley the reverse.",
                    "The mesh's absolute highest and lowest vertices are always added to peaks and valleys unless CullGlobals is on; AvoidBoundaries skips naked-edge vertices.",
                    "The heatmap colours each mesh vertex by (z - zMin) / (zMax - zMin), with zMin and zMax taken over all input meshes (the same range as the legend), either along CustomColors or with a built-in cyan-yellow-red ramp; a single custom colour paints the whole mesh.",
                    "The status shows total 3D mesh area, relief, mean vertex elevation (unweighted), extremes and counts; elevations are rounded to 0.01.",
                },
                Assumptions = new[]
                {
                    "Meshes are terrain-like and welded, so topology neighbours reflect real adjacency.",
                    "ProminenceLimit is in model units; ring count stands in for a search radius, so its real size depends on mesh density.",
                },
                Simplifications = new[]
                {
                    "Prominence is local relief within the ring neighbourhood, not true topographic prominence.",
                    "Flat plateaus produce no peak or valley unless they stand above or below their surroundings by ProminenceLimit.",
                    "Area is surface area, not plan area.",
                },
                Workflow = new[]
                {
                    "Site Fetch terrain or any terrain mesh -> TargetMeshes. Dashboard Data -> Analysis Dashboard.",
                },
            },

            ["MicroclimateComfort"] = new Note
            {
                Title = "Microclimate Comfort",
                Method = new[]
                {
                    "Each point takes Air Temp, Humidity, Wind Speed and Solar Factor from the lists by index (the last value is reused if a list is shorter; defaults 25 °C, 50 %, 1 m/s, 1.0).",
                    "Method 0: Apparent Temperature AT = T + 0.33 e - 0.70 v - 4.0, e = RH/100 × 6.105 × exp(17.27 T / (237.7 + T)) hPa, plus 4 °C × Solar Factor.",
                    "Method 1: mean radiant temperature is not computed: it is approximated as T + 15 °C × Solar Factor, which dominates the uncertainty of the UTCI result.",
                    "UTCI uses the full 210-term operational polynomial of Bröde et al. (2012) in T, v, Tmrt - T and vapour pressure, ported from pythermalcomfort (MIT), with saturation vapour pressure from Hardy (1998) as in the official UTCI program; results match the pythermalcomfort reference values within 0.1 °C.",
                    "Inputs outside the UTCI validity range (T -50 to 50 °C, Tmrt - T -30 to 70 °C, wind 0.5 to 17 m/s, vapour pressure 0 to 5 kPa) are clamped to it and a warning gives the number of points affected; those results are indicative only.",
                    "Categories: AT uses fixed bands (40, 32, 27, 20, 10, 0 °C); UTCI follows the official 10-class scale, from Extreme Heat Stress above 46 °C to Extreme Cold Stress below -40 °C.",
                    "Colours run from dark blue at -10 °C to dark red at 40 °C through 5 fixed stops.",
                },
                Assumptions = new[]
                {
                    "Inputs are point values for the same moment (one hour or design condition); the points only carry the results, the tool does not look at geometry.",
                    "Wind Speed is at 10 m height; Solar Factor 0-1 comes from a sun or shade study.",
                },
                Simplifications = new[]
                {
                    "Radiation is a fixed add-on (+4 °C AT, +15 °C Tmrt in full sun), not a computed mean radiant temperature.",
                    "No clothing, activity or local wind reduction; AT categories are not from a standard.",
                },
                Workflow = new[]
                {
                    "Sun Hours Analysis exposure -> Solar Factor; wind results (Fast CFD) -> Wind Speed; EPW hour values -> Air Temp and Humidity.",
                },
                References = new[]
                {
                    "Steadman R.G. (1994), Norms of apparent temperature in Australia, Australian Meteorological Magazine 43; the Australian Bureau of Meteorology apparent temperature formula.",
                    "Bröde P. et al. (2012), Deriving the operational procedure for the Universal Thermal Climate Index (UTCI), International Journal of Biometeorology 56.",
                    "Tartarini F. and Schiavon S. (2020), pythermalcomfort: a Python package for thermal comfort research, SoftwareX 12 (MIT licence): source of the ported UTCI polynomial.",
                    "Hardy B. (1998), ITS-90 formulations for vapor pressure, frostpoint temperature, dewpoint temperature and enhancement factors.",
                },
            },

            ["ModBalcony"] = new Note
            {
                Title = "Facade Module: Balcony",
                Method = new[]
                {
                    "Each curve in a branch is one floor outline; its floor height comes from the matching item of Heights (4.0 m if missing).",
                    "Balconies: BalconyPattern (0 and 1) picks which floors get one, repeating; the first floor of each branch is skipped unless FirstBalcony is True. A balcony is a flat ruled surface between the outline and its offset by Depth (FlipDir reverses the side), plus a RailHeight vertical surface on the offset line. If the offset splits into several pieces that do not join, only the first is used and a warning is shown.",
                    "Walls: each outline is split into segments. With SegLength on, segments shorter than 2 × BayWidth become corner pieces; otherwise CornerOffset trims that length off both ends as corner pieces.",
                    "Remaining segments are divided into round(length / BayWidth) equal bays; VertPattern digits pick, per floor, which HorizPattern string to use, and its 1 and 0 characters set glass or solid bays in turn.",
                    "A glass bay is split into a solid sill (SillHeight), glass, and a header (HeaderDrop) at the top; a solid bay is one full-height panel. Corner pieces are glass with sill and header when CornerWindow is True, else full-height panels on CornerPanels.",
                    "All wall, rail and corner surfaces are extruded straight up along world Z.",
                    "The HUD reports areas as length × height for glass, solid, header and corner panels, and outline length × Depth for terrace.",
                },
                Assumptions = new[]
                {
                    "Outlines are planar, horizontal and placed at each floor level; units are metres.",
                    "Outline direction decides the offset side for balconies and so the use of FlipDir.",
                },
                Simplifications = new[]
                {
                    "All elements are zero-thickness surfaces: no slab or railing thickness, no frames or mullions.",
                    "Bay widths are rounded to fit each segment, so they differ from BayWidth.",
                    "Terrace area is length × Depth, which overstates convex corners and understates concave ones.",
                },
                Workflow = new[]
                {
                    "Masterplan or Facade Parser floor outlines and heights -> Bounds and Heights; outputs -> custom previews or BIM export.",
                },
            },

            ["ModFins"] = new Note
            {
                Title = "Facade Module: Vertical Fins",
                Method = new[]
                {
                    "Each boundary curve (one per floor, height from Heights by path and index, default 4.0 m) is extruded straight up along world Z, whatever its orientation, into glass up to height - HeaderDrop, and a spandrel band of HeaderDrop above it.",
                    "The curve is split into its segments; each segment is divided into round(length / Spacing) equal parts (at least 1), so fins sit at both ends and the real spacing is close to Spacing. Points repeated at shared corners (within 1 mm) are used once.",
                    "Fin positions are numbered along the whole curve; a fin is placed where the active horizontal pattern has 1 (the pattern repeats).",
                    "The active pattern for the i-th curve in a branch is HorizPattern[VertPattern digit i] (both wrap round), so VertPattern picks a pattern per floor.",
                    "Each fin is a flat surface Depth deep, perpendicular to the segment on its left side (FlipDir reverses), and the full floor height tall.",
                    "The HUD reports fin count and glass and spandrel areas (curve length × height).",
                },
                Assumptions = new[]
                {
                    "Bounds are horizontal facade lines or outlines at each floor level; units are metres.",
                    "Defaults: Spacing 1.5 m, Depth 0.3 m, HeaderDrop 0 m; non-positive Spacing or Depth fall back to the defaults.",
                },
                Simplifications = new[]
                {
                    "All parts are zero-thickness surfaces; no mullions, frames or slab edges.",
                    "Fins are not adjusted at corners; patterns ignore segment breaks.",
                    "VertPattern is indexed by the item index within a branch, so each branch must hold one building's floors in order.",
                },
                Workflow = new[]
                {
                    "Facade JSON Parser BoundsExt and Heights -> Bounds and Heights; outputs -> Custom Preview or bake.",
                },
            },

            ["ModFrames"] = new Note
            {
                Title = "Facade Module: Frames",
                Method = new[]
                {
                    "Each Bounds curve is one storey; all solids and surfaces are extruded upwards (world +Z), whatever the curve orientation. Its height comes from Heights at the same index in the same branch (default 4.0 if missing or 0.1 or less). Non-positive sizes fall back to the defaults.",
                    "Offset side (area-offset method): a closed curve is offset by BeamDepth both ways and the larger result is taken as outside (inside if FlipDir). An open curve uses the positive offset side unless FlipDir.",
                    "Beams: a closed solid between the curve and its offset, BeamWidth high, at the top of the storey (from h - BeamWidth to h). The lowest curve of each branch (by Z) also gets a base beam at the storey base unless RemoveBaseBeam.",
                    "Columns: each segment is divided into round(length / BayWidth) equal bays; at every division point a ColWidth × ColDepth box, pointing towards the offset curve, rises the full storey height. Points repeated at the same position and tangent are placed once.",
                    "With HasPanel, each bay gets surfaces on the boundary line: a sill of SillHeight, glass for the vision height, and a header of HeaderDrop below the top beam. Vision height = h - BeamWidth - sill - header (each clipped to fit).",
                    "Fins: each bay with vision height is divided into round(bay length / SubdivWidth) parts; at the inner division points a vertical fin surface FinDepth deep, outward, covers the vision height.",
                    "Status: glass area and solid (sill + header) area = bay length × height, plus column and fin counts.",
                },
                Assumptions = new[]
                {
                    "Bounds are flat storey outlines at their floor level, in metres; Heights are floor-to-floor heights.",
                    "Bounds within a branch belong to one building.",
                },
                Simplifications = new[]
                {
                    "Glass, sill, header and fin elements are zero-thickness surfaces; only beams and columns are solids.",
                    "At corners two columns are placed, one per segment, because their tangents differ.",
                    "Columns run the full storey height and overlap the beams; the base beam overlaps the sill zone.",
                    "Bays are equal within each segment, so the actual bay width differs from BayWidth.",
                },
                Workflow = new[]
                {
                    "Facade JSON Parser BoundsExt and Heights -> Bounds and Heights.",
                },
            },

            ["ModRetail"] = new Note
            {
                Title = "Facade Module: Storefront",
                Method = new[]
                {
                    "Each Bounds curve is split into segments and each segment into bays: count = round(segment length / BayWidth), at least 1, so bays are equal within a segment. Heights are matched by branch path and index; 4.5 is used when missing.",
                    "Each bay is extruded straight up world Z into up to three bands: kickplate from 0 to BaseHeight (solid), middle band from BaseHeight to TransomHeight (glass or solid), header from TransomHeight to the full height. Heights are clamped so the bands never go negative.",
                    "The glass or solid choice per bay comes from a 0 and 1 string in HorizPattern (1 = glass), repeated along the curve. VertPattern digits choose which HorizPattern line each curve uses (curve index modulo).",
                    "Mullions are vertical fins extruded straight up world Z, of depth MullionDepth, perpendicular to the bay, at both ends of every bay (duplicates removed by position rounded to 3 decimals), spanning the middle band. FlipMullions reverses their side.",
                    "Where CanopyPattern has 1 for that curve index and CanopyDepth > 0, a flat canopy is lofted between the curve at transom height and its plan offset by CanopyDepth (sharp corners, offset tolerance 0.1). FlipCanopy offsets to the other side.",
                    "With ShowColumns, a column line is drawn at each mullion position, set ColumnOffset in from the offset canopy edge, from the ground to transom height.",
                },
                Assumptions = new[]
                {
                    "Bounds are planar curves in world XY; heights are measured from the curve's Z.",
                    "Model units are metres; areas in the HUD are bay length × band height in m².",
                    "BayWidth, MullionDepth and ColumnOffset of 0 or less are replaced by 2.5, 0.2 and 0.2; BaseHeight, TransomHeight and CanopyDepth may be 0.",
                },
                Simplifications = new[]
                {
                    "All elements are zero-thickness surfaces; columns are lines only.",
                    "An unwired TransomHeight is 0, so the whole height becomes header, with no kickplate, glass or mullions (a remark is shown).",
                    "Canopies are flat surfaces at transom height; there is no structural logic beyond spacing columns at mullions.",
                    "Bay widths are rounded per segment, so they differ from BayWidth and from segment to segment.",
                },
                Workflow = new[]
                {
                    "Masterplan facade outlines and floor heights -> Bounds and Heights.",
                    "Glass, SolidPanels, HeaderPanels -> previews or area take-offs.",
                },
            },

            ["ModRhythmBalc"] = new Note
            {
                Title = "Facade Module: Rhythmic Balconies",
                Method = new[]
                {
                    "Each boundary curve is one floor (height from Heights by path and index, default 4.0 m); all walls, rails and partitions are extruded straight up along world Z. Its segments are sorted into corner pieces and middle pieces.",
                    "With SegLength, segments shorter than 2 × BayWidth become corner pieces. With CornerOffset > 0, each longer segment loses CornerOffset at both ends to corner pieces.",
                    "Corner pieces become a full-height solid panel, or with CornerWindow glass to height - HeaderDrop plus a header panel.",
                    "Middle pieces are divided into bays of about BayWidth (round(length / BayWidth) equal parts). Bays are numbered along the floor and the active horizontal pattern (chosen by the VertPattern digit for that floor) says which bays get a balcony (1).",
                    "A balcony bay gets glass to height - HeaderDrop, a header above, a slab (straight loft between the bay line and its copy offset by Depth, FlipDir for the other side), a railing (the offset line extruded RailHeight) and side partitions of RailHeight. With MergeAdjacent, the partition between two neighbouring balconies is left out.",
                    "Other bays get a full-height solid panel. With FirstBalcony False, the first curve of each branch (ground floor) has no balconies.",
                    "The HUD reports glass, solid, header and corner areas (length × height), slab area (bay length × Depth, approximate for curved bays) and the balcony count.",
                },
                Assumptions = new[]
                {
                    "Bounds are horizontal lines or outlines at floor levels, in metres, listed from the ground floor up within each branch.",
                    "Defaults: BayWidth 3.0 m, Depth 1.5 m, RailHeight 1.1 m, HeaderDrop 0, HorizPattern 10, VertPattern 0; non-positive sizes fall back to defaults.",
                },
                Simplifications = new[]
                {
                    "All parts are zero-thickness surfaces; slabs have no thickness and railings are solid surfaces.",
                    "Offsets for curved bays use a coarse 0.1 tolerance; areas are length × size, not measured on the surfaces.",
                },
                Workflow = new[]
                {
                    "Facade JSON Parser BoundsExt and Heights -> Bounds and Heights; outputs -> Custom Preview or bake.",
                },
            },

            ["ModSpandrel"] = new Note
            {
                Title = "Facade Module: Spandrels",
                Method = new[]
                {
                    "Each curve in a branch is one floor outline, with floor height from the matching item of Heights; floors without a matching Heights value use 4.0 m and raise a warning.",
                    "The whole outline is extruded into a sill band of SillHeight, a glass band from SillHeight to height − HeaderDrop, and a header band of HeaderDrop; sill and header go to Spandrels.",
                    "Each outline segment is divided into round(length / BayWidth) equal bays; at every division point (duplicates removed, 1 mm rounding) a mullion fin of MullionDepth is placed perpendicular to the segment in plan.",
                    "Fins span the glass height, or the full floor height when FullHeightMullions is True; FlipDir turns them to the other side.",
                    "The HUD reports glass and solid areas as outline length × band height and the mullion count.",
                },
                Assumptions = new[]
                {
                    "Outlines are planar, horizontal and placed at each floor level; units are metres.",
                    "Fin side depends on segment direction; FlipDir reverses it.",
                },
                Simplifications = new[]
                {
                    "All elements are zero-thickness surfaces; mullions are single fins without width.",
                    "Glass is one continuous band per floor; bays only position the mullions.",
                },
                Workflow = new[]
                {
                    "Masterplan or Facade Parser floor outlines and heights -> Bounds and Heights; outputs -> custom previews or BIM export.",
                },
            },

            ["NoiseEmitterFromCurves"] = new Note
            {
                Title = "Noise Emitter From Curves",
                Method = new[]
                {
                    "Each curve is divided into N = round(length / Divide Length) equal parts (at least 1); the actual spacing D = length / N is used.",
                    "Each point gets a point-source level at 1 m, Lp1 = L_ref + 10 log10(D × RefDist / π), where L_ref is the Average Noises value for that curve (the last value is reused if the list is shorter).",
                    "This comes from treating the road as a long straight line of incoherent point sources: summed intensity at perpendicular distance d is I1 × π / (D × d), so the field equals L_ref at RefDist and falls 3 dB per doubling of distance.",
                    "The two end points of an open curve carry half a segment and get Lp1 − 3.01 dB; closed curves have no end correction.",
                },
                Assumptions = new[]
                {
                    "Average Noises is LAeq in dB(A) at RefDist from the road (default 10 m, minimum 1 m).",
                    "Free-field spherical spreading (Lp1 − 20 log10 r) with energetic summation of incoherent sources, as in the Fast Noise Analyzer; its air absorption (2.8 dB/km) lowers the level at RefDist only slightly, and its ground effect (Ground supplied, G above 0) and barriers are not compensated, so L_ref at RefDist is reproduced only for hard ground and free view.",
                    "Model units are metres.",
                },
                Simplifications = new[]
                {
                    "Calibration assumes a long straight road; near the ends of short roads and on tight curves levels are slightly off.",
                    "No ground absorption, barriers, traffic composition, speed or road surface: those must be built into the input level.",
                    "Receivers closer than about one spacing to the road can see local hot spots near individual points.",
                },
                Workflow = new[]
                {
                    "Road or rail centrelines -> Curve Paths; typical LAeq at 10 m from the value list (Highway 78, Arterial 70, Local 60, Pedestrian 50, Railway 80 dB(A)).",
                    "Points and Noise Values -> Fast Noise Analyzer (Power(dB) input).",
                },
            },

            ["OptimizedRemap"] = new Note
            {
                Title = "Optimized Remap",
                Method = new[]
                {
                    "Finds the minimum and maximum of Values and maps each value linearly: Target start + (value − min) / (max − min) × (Target end − Target start). If all values are equal they all map to Target start.",
                },
                Assumptions = new[]
                {
                    "Values are finite numbers.",
                },
                Simplifications = new[]
                {
                    "None beyond linear min-max scaling; outliers set the range.",
                },
            },

            ["PHeal"] = new Note
            {
                Title = "Polyline Healer",
                Method = new[]
                {
                    "Each input curve that is a polyline is split into segments; each segment longer than 0.001 is extended at both ends by Tolerance (an absolute length in model units, default 1.5).",
                    "The extended lines are intersected into closed regions with Rhino Boolean regions in the polyline best-fit plane (World XY if fitting fails), the regions are unioned, and the curve with the largest area is returned as the healed outline.",
                    "If anything fails (not a polyline, no regions), the original curve is returned unchanged.",
                },
                Assumptions = new[]
                {
                    "Input is a nearly closed planar outline with small gaps or overshoots, smaller than Tolerance.",
                },
                Simplifications = new[]
                {
                    "Only the outer boundary is kept: holes and separate islands are dropped. Tolerance also lets short neighbouring segments extend past each other, which can add spurious regions if it is too large.",
                },
                Workflow = new[]
                {
                    "Imported CAD or GIS outlines (plots, building footprints) -> Polyline; healed curves -> region, offset or extrusion components.",
                },
            },

            ["PaperSizeToPixels"] = new Note
            {
                Title = "Document Size to Pixels",
                Method = new[]
                {
                    "A-series formats (A0 to A5, ISO 216 sizes in mm) are oriented by Landscape and converted with pixels = round(mm / 25.4 × DPI).",
                    "Screen formats (FHD 1920 × 1080, QHD 2560 × 1440, 4K 3840 × 2160, Instagram 1080 × 1080) keep their native pixel size (DPI does not apply) and are swapped to portrait when Landscape is False.",
                    "Unknown formats fall back to A4 with a warning; DPI is passed through.",
                },
                Assumptions = new[]
                {
                    "Screen formats are exact pixel sizes; DPI only applies to paper formats and is passed through.",
                },
                Simplifications = new[]
                {
                    "Only the listed formats are recognised; no custom sizes or US paper sizes.",
                },
                Workflow = new[]
                {
                    "Doc2Px Width, Height, DPI -> Export Named Views.",
                },
                References = new[]
                {
                    "ISO 216, paper sizes (A series).",
                },
            },

            ["ParseTable"] = new Note
            {
                Title = "Table to DataTree",
                Method = new[]
                {
                    "Splits TableData into lines (empty lines dropped) and each line into cells by tab if the text contains any tab, otherwise by comma.",
                    "Each trimmed cell is parsed as a number with invariant culture (dot decimal point, no thousands separators): whole numbers that fit a 32-bit integer become integers, others decimals, the rest text.",
                    "ByColumn True puts each column in its own branch (one branch per column index); False puts each row in its own branch.",
                },
                Assumptions = new[]
                {
                    "Text pasted from Excel (tab separated) or simple CSV with no quoted fields.",
                },
                Simplifications = new[]
                {
                    "Quoted CSV fields, commas inside cells and empty rows are not handled; ragged rows give branches of different lengths.",
                },
                Workflow = new[]
                {
                    "Panel with a pasted table -> TableData -> Recipe JSON Builder or Master JSON Config Builder inputs.",
                },
            },

            ["PixelatedSurface"] = new Note
            {
                Title = "Pixel Image",
                Method = new[]
                {
                    "Loads the image from Image Path (cached until the path changes). Only cells that are polylines are used.",
                    "Mapping plane: Mapping Plane if given, else the plane of the first planar cell. The image is stretched over the bounding rectangle of all cell vertices in that plane.",
                    "Each cell is sampled at one pixel under the average of its vertices (nearest pixel); Rotation turns the sample position about the image centre in normalised image coordinates.",
                    "Brightness is the HSL lightness of that pixel (0 to 1); Jitter Pct adds uniform noise of ± Jitter Pct / 100; the colour index is round(brightness × (number of Colors - 1)), from dark to light.",
                    "With an Accent Color, each cell is replaced by the accent with probability Accent Pct / 100. The random generator uses a fixed seed (42), so results repeat.",
                    "Inset Factor (0.01 to 1) scales each cell about its vertex average; cells are triangulated as a fan from that point and joined into one mesh per colour.",
                    "Tile counts in panel_tags and the status count cells per colour.",
                    "Bake writes the meshes to sublayers of Tiles named HexFacade_tag_colour hex, one per palette colour, with one material per colour. Without a Bake Name, baking happens once each time Bake turns True; with a Bake Name, earlier objects with that name are replaced on every solve while Bake is True.",
                },
                Assumptions = new[]
                {
                    "Cells are planar, convex polylines lying roughly in one plane (a flat or gently curved facade).",
                },
                Simplifications = new[]
                {
                    "One pixel per cell, no averaging over the cell area; aspect ratio of the image is not kept.",
                    "Brightness is HSL lightness, not perceived luminance.",
                    "End colours of the palette get half-width brightness bands because of rounding.",
                },
                Workflow = new[]
                {
                    "Grid cells (for example from a hexagon or square grid on a facade) -> Grid Cells.",
                },
            },

            ["PlaneFinder"] = new Note
            {
                Title = "Surface Plane Finder",
                Method = new[]
                {
                    "Takes the first item of x. A Surface is used directly; a single-face Brep uses its face; polysurfaces are rejected.",
                    "If the surface is planar within the document absolute tolerance, the normal is evaluated at the middle of its UV domain and follows the Brep face orientation (reversed faces are taken into account). A non-planar input raises a warning and gives no outputs.",
                    "For a single-face Brep the origin is the area centroid of the trimmed face; for a Surface it is the bounding-box centre (plane surfaces) or the area centroid of the surface. Outputs the normal, its reverse and a plane at that origin.",
                },
                Assumptions = new[]
                {
                    "The input is a single planar face.",
                },
                Simplifications = new[]
                {
                    "Only the first list item is processed.",
                    "The plane X axis is chosen by Rhino from the normal; it is not aligned with the face edges.",
                },
                Workflow = new[]
                {
                    "Facade panel faces -> x; output_plane -> orientation of panels, attractors or text.",
                },
            },

            ["PluginInfo"] = new Note
            {
                Title = "Plugin info",
                Method = new[]
                {
                    "Outputs the Enzyme assembly version number and the last-write date and time of the plugin file on disk (local time, yyyy-MM-dd HH:mm:ss).",
                },
                Assumptions = new[]
                {
                    "The file date reflects the install or copy time, which normally matches the build.",
                },
                Simplifications = new[]
                {
                    "Build Date is the file modification time, not a build stamp embedded in the assembly; it reads Unknown if the file cannot be found.",
                },
            },

            ["PointProbe"] = new Note
            {
                Title = "Point Probe",
                Method = new[]
                {
                    "Each probe point is moved to the closest point on the Mesh (ignored if further than MaxDistance; 0 = no limit).",
                    "For per-vertex values the result is interpolated with the barycentric weights of that point inside the hit face; vertices with no value are left out and the weights rescaled. For per-face values the face value is used. If the mesh has as many vertices as faces, values are read as per-vertex and a warning flags the ambiguity.",
                    "Each branch of Values is one result set and one CSV column; branches must have one value per vertex or per face.",
                    "Labels show name: value(s) unit; the CSV has Name, X, Y, Z (closest mesh point, 2 decimals) and one column per set.",
                },
                Assumptions = new[]
                {
                    "Values are in the same order as the mesh vertices or faces they came from.",
                },
                Simplifications = new[]
                {
                    "No search for the best face when several are equally close; values are only as fine as the mesh.",
                },
                Workflow = new[]
                {
                    "Any analysis mesh and its values (Sun Hours, Fast Noise, wind) -> Mesh and Values; Labels and MeshPoints -> Text Tag 3D.",
                },
            },

            ["RailParser"] = new Note
            {
                Title = "Railing JSON Parser",
                Method = new[]
                {
                    "Parses Railings_JSON as an object of building name to a list of railings; each railing has floor_index, tower_id, true_z and curves (each a list of Line, Arc or Polyline segments).",
                    "Railings are kept when they pass Filter_Building, Filter_Tower and Filter_Level. Text filters are case-insensitive: ExactMatch requires equality; otherwise * and ? wildcards are used if present, else a substring match.",
                    "Each curve's segments are rebuilt, joined (0.01 tolerance), closed if the gap is within 0.01, and moved up by true_z. Output branches are building index; railing index, with a label Building | Tower - Lvl n Railing.",
                },
                Assumptions = new[]
                {
                    "Segment coordinates in the JSON are relative to true_z (the component adds true_z to Z).",
                },
                Simplifications = new[]
                {
                    "When the segments of one curve do not join into one piece, every joined piece is output in the railing's branch.",
                    "Branch numbers are not stable when filters change: building indices count matched buildings only.",
                },
                Workflow = new[]
                {
                    "Masterplan Engine railings JSON -> Railings_JSON; RailingCurves -> sweep or Archicad railing export.",
                },
            },

            ["RandomTreeSplit"] = new Note
            {
                Title = "RandomTreeSplit",
                Method = new[]
                {
                    "The branch order is shuffled with a Fisher-Yates shuffle using .NET Random seeded with the integer part of seed.",
                    "The first floor(pA × branch count) shuffled branches go to A, the next floor(pB × branch count) to B, the rest to C. Paths and items are kept.",
                },
                Assumptions = new[]
                {
                    "pA and pB are fractions with pA + pB ≤ 1.",
                },
                Simplifications = new[]
                {
                    "Counts are rounded down, so C receives any remainder.",
                },
            },

            ["RecipeBuilder"] = new Note
            {
                Title = "Recipe JSON Builder",
                Method = new[]
                {
                    "Matches Program, FloorHeight and NumFloors by index (up to the shortest list) and writes a JSON array of objects with program, height (floor-to-floor) and floors.",
                    "The status shows the number of blocks, total floors and total height = sum of floors × height.",
                },
                Assumptions = new[]
                {
                    "Heights are floor-to-floor heights in metres; the list order is the stacking order from the bottom.",
                },
                Simplifications = new[]
                {
                    "Extra items in longer lists are ignored without a warning.",
                },
                Workflow = new[]
                {
                    "Recipe JSON Builder -> RecipeJSON of Adapter: The Sculptor (AD_SCULPT).",
                },
            },

            ["ReportMetricsExport"] = new Note
            {
                Title = "Report Metrics",
                Method = new[]
                {
                    "Reads every Dashboard Data JSON item: each Metrics entry whose Value starts with a number becomes a metric (key = Prefix + snake_case name, %, > and < spelled out; _pct added for %), and each BandAreas entry becomes an area metric in ha (area / 10 000).",
                    "Adds the hand-entered Keys, Values, Units and Scopes (scope defaults to site). Without Prefixes, several Dashboard items get d1, d2... prefixes.",
                    "Each flag rates one metric: if Red ≥ Amber higher is worse (red when value ≥ Red, amber when ≥ Amber), otherwise lower is worse (red when ≤ Red, amber when ≤ Amber); n/a when the metric is missing.",
                    "Builds the module JSON (module, plugin version, run date, inputs from Sources split on |, metrics, flags, maps with captions, confidence). With Merge on, metrics, flags, maps and inputs already in the existing file are kept and only this run's keys are replaced.",
                    "Writes Folder/04_metrics/Module.json when Write is True.",
                },
                Assumptions = new[]
                {
                    "The model is in metres, so areas in m² convert to ha.",
                    "Metric keys are unique per key and scope; the last value wins.",
                },
                Simplifications = new[]
                {
                    "Flags inherited from an existing file are not re-rated against new values; only this run's flag rules are evaluated.",
                    "Metric values are parsed from display text, so precision is limited to what the source component printed.",
                },
                Workflow = new[]
                {
                    "Dashboard Data from Analysis Heatmap, Analysis Difference, Fast CFD, Noise... -> Dashboard Data. Saved Files of Advanced Export Views -> Maps.",
                    "The 04_metrics files feed the Deep Site Analysis report and scorecard.",
                },
            },

            ["RoadGenerator"] = new Note
            {
                Title = "Procedural Road Generator",
                Method = new[]
                {
                    "Each centreline is sampled every Subdivide metres. At each station a horizontal normal (tangent × Z) gives the deck edges at ± (Directions × Lanes/Dir × Lane Width / 2 + Shoulder) and the lane centrelines. The deck is flat across at the centreline Z.",
                    "The terrain height under the centre and both edges is found by vertical ray casting. If the centreline is more than Threshold above the terrain the station is a bridge: no earthworks, and a pillar line is drawn down to the terrain at least every Pillar Sep, measured along the centreline arc length.",
                    "Otherwise, at each edge differing from the ground by more than 0.1 m, a ray is cast outwards at Blend Angle from horizontal, up for cut and down for fill; where it hits the terrain is the daylight point. If it misses, or the edge is within 0.1 m, the slope toe is put 2 m out at ground level.",
                    "Cut and fill volumes are built between consecutive station profiles (daylight, edge, centre, edge, daylight versus the original ground under the same points). Each strip is split into triangles, split again where road and ground cross, and the volume is plan area × mean depth of each prism.",
                    "The terrain is rebuilt: original vertices within the daylight width + 1.5 × Subdivide of any station are removed, the station points are added (except those near other roads), and the set is re-triangulated with 2D Delaunay; triangles with an edge over 150 m are removed.",
                    "Outputs: modified terrain, deck meshes, lane and edge polylines, pillar lines, closed cut and fill meshes, and contours of the output terrain every 1 m (Contours, levels not on a 5 m multiple) and every 5 m (Main Contours); total cut and fill in m³ are shown on the component only.",
                },
                Assumptions = new[]
                {
                    "Centrelines are 3D and already carry the design levels; the tool does not design a vertical alignment.",
                    "Model units are metres (fixed 0.1 m, 2 m, 150 m and 10 000 m ray offset); other units raise a warning.",
                    "The terrain is a single-valued surface (one height per plan point).",
                },
                Simplifications = new[]
                {
                    "No crossfall, camber or superelevation; the deck is level across.",
                    "Deep cuts (tunnels) are not detected; only high fills become bridges.",
                    "Volumes use the prismoid approximation between stations spaced Subdivide apart; stations more than 3.5 × Subdivide apart (gaps, bridges) are skipped.",
                    "Junctions are handled only by removing terrain points near other roads; there is no junction geometry.",
                    "A tangent that turns more than 90° between stations is treated as flipped and reversed, so hairpins with a coarse Subdivide can get wrong edge sides.",
                },
                Workflow = new[]
                {
                    "Road centrelines with levels (for example from a road network tool) and a terrain mesh -> Centerlines and Terrain.",
                    "Modified Terrain -> slope, flood or visibility analyses.",
                },
                References = new[]
                {
                    "Grasshopper Delaunay solver (Grasshopper.Kernel.Geometry.Delaunay) for re-triangulation.",
                },
            },

            ["RoadProfileUnroller"] = new Note
            {
                Title = "Road Profile & Slope Unroller",
                Method = new[]
                {
                    "Profile: each curve is sampled at 201 points; each point is plotted on Base Plane at x = 3D length along the curve from its start and y = Z × Z Exaggeration, and a degree-3 interpolated curve is fitted through them.",
                    "Analysis: the curve is cut into N = ceil(length / Segment Size) equal-length pieces. Slope per segment uses the straight chord between the segment end points: horizontal distance H in plan and absolute height change V; degrees = atan(V / H), percent = V / H × 100, ratio = H / V (1:X).",
                    "A segment is Compliant if slope ≤ Threshold (degrees or percent) or ratio ≥ Threshold (ratio mode, flat segments always compliant). A vertical segment (no plan length) reads 90 degrees, infinite percent or 1:0 and is Non-Compliant. Compliance % is the share of compliant segments over all curves.",
                    "In ratio mode a flat segment is infinity (1:infinity) in Slope Values; HUD statistics use finite values only.",
                    "Segments, centre points and status are drawn on the same profile graph.",
                    "Alignment: the curve is split into its own segments (polyline spans, arcs, splines); a vertical line drops from each break point to Ref Y, and each span is labelled Line L, Arc R or Spline L (rounded to 0.1) at Ref Y − 2 × Z Exaggeration.",
                },
                Assumptions = new[]
                {
                    "Curves are 3D road centrelines drawn in the direction of chainage, in metres.",
                    "Ref Y is in graph units, i.e. already scaled (an elevation × Z Exaggeration).",
                    "Threshold is in the units chosen by Mode (default 8 %).",
                },
                Simplifications = new[]
                {
                    "The horizontal axis is 3D length along the curve, not plan chainage, so steep roads plot slightly longer than their plan length.",
                    "Slope is an average over each segment; peaks shorter than Segment Size are smoothed out. Uphill and downhill are not distinguished.",
                    "Elevations are plotted as absolute Z (no datum subtraction), so Base Plane origin must be set accordingly.",
                },
                Workflow = new[]
                {
                    "3D road centrelines (for example from Road Corridor or a graded alignment) -> Curves.",
                    "Segments and Status -> colour by status for long sections; Dashboard Data -> legend.",
                },
            },

            ["RoadSlope3DAnalyzer"] = new Note
            {
                Title = "3D Road Slope Analysis",
                Method = new[]
                {
                    "Each curve is cut into N = ceil(length / Segment Size) equal-length pieces (trimmed sub-curves, so actual piece length is at most Segment Size).",
                    "Slope per segment uses the straight chord between the segment end points: horizontal distance H in plan and absolute height change V; degrees = atan(V / H), percent = V / H × 100, ratio = H / V (1:X).",
                    "A segment is Compliant if slope ≤ Threshold (degrees or percent) or ratio ≥ Threshold (ratio mode, flat segments always compliant). A vertical segment reads 90 degrees, infinite percent or 1:0 and is Non-Compliant. In ratio mode a flat segment is infinity in Slope Values and the HUD statistics use finite values only. Compliance % is the share of compliant segments over all curves.",
                    "Outputs the 3D segments, slopes, mid-length points and status per curve branch, plus a Binary legend payload using Colors (default lime green and red).",
                },
                Assumptions = new[]
                {
                    "Curves are 3D road centrelines already at their design levels, in metres.",
                    "Threshold is in the units chosen by Mode (default 8 %).",
                },
                Simplifications = new[]
                {
                    "Slope is the chord slope over each segment, so short steep pieces inside a segment are averaged out; uphill and downhill are not distinguished.",
                    "Only longitudinal slope; no cross-fall, curvature or vertical curve checks. Colors only feed the legend; segments are not coloured by the component.",
                },
                Workflow = new[]
                {
                    "3D centrelines -> Curves; Status -> colour the Segments with a Custom Preview; Dashboard Data -> Legend Geometry or Analysis Dashboard.",
                },
            },

            ["RoadSlopeAnalyzer"] = new Note
            {
                Title = "Road Slope Analyzer",
                Method = new[]
                {
                    "Each curve is divided into ceil(length / Segment Size) pieces by equal parameter steps.",
                    "Both ends of each piece are dropped vertically onto the Terrain (ray from above the mesh; if that misses, from below).",
                    "The slope of the straight chord between the two projected points is computed as degrees atan(Δz / Δxy), percentage Δz / Δxy × 100, or ratio Δxy / Δz (Threshold Mode 0, 1, 2).",
                    "A piece is compliant if its slope is at or below Threshold (for the ratio mode, at or above Threshold). Results go to branch 0 (compliant) or 1 (non-compliant); Compliance Percentage is the share of compliant pieces by count.",
                },
                Assumptions = new[]
                {
                    "Curves lie roughly in plan over the terrain; their own Z is ignored.",
                    "The top-most terrain surface is the road surface.",
                },
                Simplifications = new[]
                {
                    "A vertical piece reads 90 degrees, infinite percent or 1:0 and is never compliant; in ratio mode a flat piece is infinite and compliant.",
                    "Slope is a straight chord between piece ends; bumps inside a piece are not seen.",
                    "Compliance is by number of pieces, not by length.",
                    "No cross slope, no vertical curve design; this checks existing ground along the alignment only.",
                },
                Workflow = new[]
                {
                    "Road centrelines and Terrain Mesher output -> Curves and Terrain.",
                    "Dashboard -> Analysis Dashboard legend.",
                },
            },

            ["RoofParser"] = new Note
            {
                Title = "Roof JSON Parser",
                Method = new[]
                {
                    "Reads Roof_JSON (an object of buildings, each with a list of roofs) and keeps a roof only if it passes every filter that is not empty: Filter_Building, Filter_Tower, Filter_Type, Filter_Program (any program above) and Filter_Level (floor_index).",
                    "Text filters are case-insensitive. Flexible mode matches when the name contains the filter text, or with * and ? wildcards; ExactMatch needs the whole name (still case-insensitive).",
                    "For each roof it outputs slab and tower outlines rebuilt from Line, Arc and Polyline segments and moved up to true_z, plus roof_area, true_z and a label (building, tower, type, level). Branch paths are building index and roof index in the JSON, so they stay stable when filtering.",
                },
                Assumptions = new[]
                {
                    "The JSON comes from JSON MP Engine V2 (Roof_JSON): outlines at Z = 0 and true_z in metres.",
                },
                Simplifications = new[]
                {
                    "Only the first joined curve of each outline is kept.",
                },
                Workflow = new[]
                {
                    "JSON MP Engine V2 Roof_JSON -> Roof_JSON. SlabBounds and TowerBounds -> roof landscape, PV or amenity studies.",
                },
            },

            ["SaveNamedView"] = new Note
            {
                Title = "Save Named View",
                Method = new[]
                {
                    "When Save is True and Name is not empty, the active viewport of the active Rhino document is saved as a Named View. If the name exists it is deleted and re-added when Overwrite is True, otherwise skipped with a warning.",
                    "Available Views lists all named views in the document.",
                },
                Assumptions = new[]
                {
                    "Save is a button; a toggle left True saves again on every solution.",
                },
                Simplifications = new[]
                {
                    "None beyond saving the active viewport's camera as Rhino does.",
                },
                Workflow = new[]
                {
                    "Save Named View, then Document Size to Pixels -> Export Named Views.",
                },
            },

            ["SegDisp"] = new Note
            {
                Title = "Curve Segment Dispatcher",
                Method = new[]
                {
                    "Splits each curve into segments (DuplicateSegments), keeping the branch structure.",
                    "Linear segments go to lines. Other segments that are arcs within 0.001 go to arcs, with their radius and centre.",
                    "With dim_toggle, each arc gets a radial leader line of length offset (default 10) outwards from the arc midpoint and a text dot R radius, rounded to num_dec decimals.",
                },
                Assumptions = new[]
                {
                    "Curves are made of lines and arcs; offset is in model units.",
                },
                Simplifications = new[]
                {
                    "Segments that are neither lines nor arcs are not output; a warning gives their count.",
                },
            },

            ["SlabParser"] = new Note
            {
                Title = "Slab JSON Parser",
                Method = new[]
                {
                    "Reads the slab JSON (building name -> list of slabs with tower_id, floor_index, true_z, area and boundary) and keeps slabs whose building, tower and level pass the filters.",
                    "Filters are case-insensitive; with ExactMatch False a filter matches if contained in the name, or as a wildcard pattern with * and ?; empty filters accept everything.",
                    "Each boundary is rebuilt from Line, Arc and Polyline segments, joined (0.01 tolerance), closed if needed and moved up by true_z. Output paths are building index; slab index.",
                    "Areas are read from the JSON, not recomputed; Labels read building | tower - Lvl level.",
                },
                Assumptions = new[]
                {
                    "The JSON comes from the Masterplan Engine Slab_JSON output, with boundary curves at Z = 0.",
                },
                Simplifications = new[]
                {
                    "If a boundary does not join into one curve only the first joined piece is kept and a warning is shown; missing point coordinates default to the origin.",
                },
                Workflow = new[]
                {
                    "Masterplan Engine Slab_JSON -> Slab_JSON -> facade modules or area schedules.",
                },
            },

            ["SlopeTerrainPlus"] = new Note
            {
                Title = "Terrain Slope",
                Method = new[]
                {
                    "Each mesh is unwelded and its face normals computed; face slope θ = acos(|n.Z|), so faces wound downwards read the same as upward ones.",
                    "ThresholdValue is converted to an angle from the chosen ThresholdMode: degrees, percent (θ = atan(p / 100)) or ratio 1:X (θ = atan(1 / X)).",
                    "Binary mode (default): faces with θ ≤ threshold take the first Custom Colors entry, steeper faces the last (defaults light green and red).",
                    "Gradient mode: θ is normalised between the minimum and maximum slope over all input meshes and coloured by linear interpolation through all Custom Colors; legend labels give the slope at each colour stop in the ThresholdMode unit.",
                    "Dashboard Data reports the share of mesh surface area over the threshold over all meshes, and the average of the per-mesh area shares.",
                },
                Assumptions = new[]
                {
                    "Meshes are terrain with +Z up; units do not matter because slope is an angle.",
                },
                Simplifications = new[]
                {
                    "Slope is per face, with no smoothing across neighbouring faces, so mesh noise shows as steep faces.",
                    "Gradient colours are relative to the slope range of the run, so they are comparable between the meshes of one run but not between runs.",
                },
                Workflow = new[]
                {
                    "Terrain mesh (for example from Site Fetch) -> TargetMeshes; Dashboard Data -> Legend Geometry or Analysis Dashboard.",
                },
            },

            ["SortCurvesByAxis"] = new Note
            {
                Title = "Sort Curves By Axis",
                Method = new[]
                {
                    "In each branch, sorts the curves by the X, Y or Z coordinate (Axis 0, 1, else 2) of their bounding-box centre, ascending, or descending with Reverse.",
                },
                Assumptions = new[]
                {
                    "The bounding-box centre is a good proxy for the curve position.",
                },
                Simplifications = new[]
                {
                    "Null curves are removed, so the output can be shorter than the input.",
                },
            },

            ["Streamlines"] = new Note
            {
                Title = "Streamlines",
                Method = new[]
                {
                    "Field: if FieldPoints lie on a regular XY lattice (holes allowed, spacing = most common gap between coordinates, 5 % tolerance), vectors are read with bilinear interpolation from the 4 surrounding cells, needing at least 3 of them. Otherwise inverse distance weighting with weights 1 / d² over points within SearchRadius in plan is used.",
                    "Only the X and Y components of FieldVectors are used; the ground height Z is interpolated the same way so lines drape over the field points.",
                    "From each SeedPoint a line is integrated with 4th-order Runge-Kutta on the unit direction field, so each step advances StepSize along the line; Direction 1 traces upstream and downstream and joins them, 0 downstream only, −1 upstream only.",
                    "A line stops when it leaves the field (or reaches a missing grid cell such as a building), local speed drops below MinSpeed, it stalls, it returns within StepSize / 2 of its seed after more than 8 steps, or after MaxSteps.",
                    "Points are lifted by MoveUp. Speeds are the local vector magnitudes at each vertex; with TubeRadius above 0 a 6-sided tube mesh is built, coloured by speed between the overall minimum and maximum using Palette (default Wind).",
                },
                Assumptions = new[]
                {
                    "FieldVectors are velocities in m/s (MinSpeed and the legend use m/s).",
                    "The flow is essentially horizontal (2.5D).",
                },
                Simplifications = new[]
                {
                    "Steady field: lines are streamlines of a single snapshot, not particle paths through time-varying flow.",
                    "Inverse distance weighting smooths the field and does not respect obstacles; with scattered points lines stop at buildings only if no field point lies within SearchRadius.",
                    "Tube colours are relative to the speed range of this run.",
                },
                Workflow = new[]
                {
                    "Fast CFD or a wind engine grid points and vectors -> FieldPoints and FieldVectors; seed points along the upwind edge -> SeedPoints.",
                    "Tubes -> preview; Dashboard Data -> Legend Geometry.",
                },
                References = new[]
                {
                    "Press W.H. et al., Numerical Recipes: classical 4th-order Runge-Kutta integration.",
                    "Shepard D. (1968), A two-dimensional interpolation function for irregularly-spaced data (inverse distance weighting).",
                },
            },

            ["SurveyOutlierCleaner"] = new Note
            {
                Title = "Survey Outlier Cleaner",
                Method = new[]
                {
                    "For each point, the Neighbours (K, default 12, minimum 4) nearest points in plan (XY) are found with an R-tree.",
                    "A local ground plane z = a + b·dx + c·dy is fitted to the neighbours by least squares (falling back to a line fit for collinear neighbours, e.g. a road string, and to the median for a single cluster).",
                    "Neighbours whose residual exceeds max(3 × MAD, Min Deviation) are dropped and the plane is refitted; MAD is the median absolute deviation × 1.4826 (a robust standard deviation).",
                    "Deviation = point Z minus the plane at the point; Score = |Deviation| / max(MAD, Min Deviation / Threshold). A point is flagged when Score > Threshold (default 3) and |Deviation| > Min Deviation (default 0.3).",
                    "Passes (default 2, maximum 5) repeat the test with flagged points removed from the neighbour pool, so clusters of bad shots cannot support each other; flags come from the last pass.",
                    "Deviation, Score, Flags and Colors follow the input list item by item; invalid points get empty values and Flag False, and a warning gives their count.",
                },
                Assumptions = new[]
                {
                    "Ground varies smoothly over the K-neighbour scale, so a local plane represents it.",
                    "Min Deviation is in model units (metres).",
                },
                Simplifications = new[]
                {
                    "Breaklines (kerbs, walls, retaining edges) can be flagged because a plane cannot follow a step.",
                    "Nothing is deleted: Clean and Outliers split the input; review Outliers before meshing.",
                },
                Workflow = new[]
                {
                    "Survey points (DWG import) -> Points; Clean -> Delaunay mesh or terrain components; Flags -> Cull Pattern.",
                },
                References = new[]
                {
                    "Rousseeuw P.J. and Croux C. (1993), Alternatives to the median absolute deviation, Journal of the American Statistical Association 88: MAD × 1.4826 as a robust standard deviation.",
                },
            },

            ["TShuffle"] = new Note
            {
                Title = "Tree Shuffler",
                Method = new[]
                {
                    "Shuffles the Choices list once with a Fisher-Yates shuffle seeded by Seed (default 42).",
                    "Walks the Tree branch by branch and replaces each item with the next item of the shuffled list, cycling back to the start when the list runs out. The output keeps the tree's paths and branch lengths.",
                },
                Assumptions = new[]
                {
                    "Only the structure of Tree matters; its item values are ignored.",
                },
                Simplifications = new[]
                {
                    "Not independent random draws: when the tree has more items than Choices, the same shuffled sequence repeats, so every choice appears evenly.",
                },
                Workflow = new[]
                {
                    "Grid cells or panel tree -> Tree; material or colour options -> Choices.",
                },
                References = new[]
                {
                    "Knuth D.E., The Art of Computer Programming, Vol. 2: Fisher-Yates shuffle.",
                },
            },

            ["TerrainColorizer"] = new Note
            {
                Title = "Terrain Colorizer",
                Method = new[]
                {
                    "Each vertex gets a colour from its normalised height t = (z - zMin) / (zMax - zMin) of the mesh bounding box, interpolated linearly in RGB along Colors (default green, yellow-green, goldenrod, brown, white).",
                    "With Use Slope Color, the vertex normal's vertical component nz is compared with cos(Slope Angle): where |nz| is smaller (steeper than Slope Angle), the colour blends towards Slope Color by min((cos(Slope Angle) - |nz|) / 0.20, 1).",
                    "Contours are cut every Contour Step from the first multiple of the step below the lowest point; a contour whose level is a multiple of Main Step (measured from Z = 0) goes to Main Contours, the rest to Contours.",
                },
                Assumptions = new[]
                {
                    "Terrain is a mesh in metres with Z up; vertex normals (recomputed here) represent the local slope.",
                },
                Simplifications = new[]
                {
                    "The slope blend runs over a fixed 0.20 range of the normal's vertical component, not a fixed number of degrees.",
                    "Main contours only appear when Main Step is a multiple of Contour Step.",
                },
                Workflow = new[]
                {
                    "Site Fetch terrain or any terrain mesh -> Terrain.",
                },
            },

            ["TerrainGeneratorPro"] = new Note
            {
                Title = "Terrain Generator Pro",
                Method = new[]
                {
                    "The Boundary is flattened and tessellated: a grid of Resolution columns across its width (rows in proportion) inside the boundary, plus points along the boundary at about one grid step, are triangulated with Rhino CreateFromTessellation.",
                    "If MinHeight is greater than MaxHeight the two are swapped and a warning is shown. Each vertex height = MinHeight + t × (MaxHeight − MinHeight), where t is fractal gradient (Perlin-style) noise: one octave per PatternSizeXY / PatternHeightZ pair (defaults when the inputs are empty 500, 150, 30 m and 1, 0.3, 0.05; the auto-wired panels give 150, 50, 20 and 1.0, 0.3, 0.1), octave i seeded with Seed + i and each octave's coordinates rotated about 37°.",
                    "TerrainStyle 0 (soft hills): weighted mean of the octaves mapped to 0 to 1 and clamped. TerrainStyle 1 (ridged): weighted mean of (1 − |n|)².",
                    "Vertex colours follow the Colors gradient by relative height. With UseSlopeColor, vertices whose normal Z is below cos(SlopeAngle) blend towards SlopeColor over 0.2 of normal Z.",
                    "Contours are cut every ContourStep from the mesh; those at multiples of MainStep go to MainContours.",
                    "Trees: on triangles with normal Z ≥ 0.7 and relative height between TreeZMin and TreeZMax, a forest mask noise (150 m scale, TreeSeed) below TreeMsk gives an expected count of (1 − mask / TreeMsk) × TreeDns × 3 trees per triangle at random positions.",
                    "Solid adds a flat base at min height − max(1 m, 10% of the height range) and side walls from the naked edges.",
                },
                Assumptions = new[]
                {
                    "Units are metres; the noise uses absolute world X and Y, so moving the Boundary changes the terrain.",
                    "The result is synthetic test terrain, not a model of a real site.",
                },
                Simplifications = new[]
                {
                    "Heights stay inside MinHeight to MaxHeight but rarely reach them: the noise is not stretched to the full range.",
                    "Tree count is per triangle, not per area, so density per hectare changes with Resolution.",
                    "No erosion, drainage or geological process is modelled.",
                    "The sine-based hash loses precision at large (UTM-size) coordinates, so the noise degrades far from the origin.",
                    "With Solid on, the mesh is welded at any angle, so wall and base colours can bleed into the terrain edge.",
                },
                Workflow = new[]
                {
                    "Closed curve -> Boundary. Mesh -> Raindrop Flow Engine, Hydro-DEM, slope or wind tools to test them on varied topography.",
                },
                References = new[]
                {
                    "Perlin K. (2002), Improving noise, ACM SIGGRAPH: quintic fade curve used in the noise.",
                },
            },

            ["TerrainMesher"] = new Note
            {
                Title = "Terrain Mesher",
                Method = new[]
                {
                    "Breaklines are sampled at about Spacing along each segment, keeping corners. Survey points inside closed breaklines (platforms) are dropped, and so are points within Clear Radius in plan of an open breakline.",
                    "Breakline samples come first, then the kept survey points; points closer than max(tolerance, 0.02 × Spacing) in plan are merged, the first one winning.",
                    "If Boundary or Plot Boundary is given, a first Delaunay mesh is built to give their sampled outline points a height (vertical drop; points outside snap to the nearest mesh point). Points outside Boundary are removed, points within 0.25 × Spacing of the plot edge are removed, and the outline points are added.",
                    "The final points are triangulated with 2D Delaunay in plan around a local origin and lifted to their Z in double precision. Triangles with a plan edge longer than Max Edge, or with their centroid outside Boundary, are removed. Faces are wound counter-clockwise so normals point up.",
                    "Plot Mesh is the set of triangles whose centroid is inside the plot outline; the plot outline and Trees are dropped onto the final mesh. Colors, when given, shade vertices by Z between the mesh minimum and maximum.",
                },
                Assumptions = new[]
                {
                    "Survey points and breaklines describe a single-valued ground surface (no overhangs).",
                    "Closed breaklines carry the platform level along their edge; inside them the surface is triangulated from the edge points only.",
                    "Boundaries are tested in plan; Z of Boundary and Plot Boundary is ignored.",
                },
                Simplifications = new[]
                {
                    "Linear interpolation on triangles (TIN); no smoothing or contour-based interpolation.",
                    "Breaklines are enforced by dense sampling, not as hard constrained edges, so a triangle can still cut across a breakline between samples.",
                    "Points outside the preliminary mesh snap to the nearest mesh point, which also moves them in plan.",
                },
                Workflow = new[]
                {
                    "Survey Outlier Cleaner -> Points; road edges and platform outlines -> Breaklines.",
                    "Mesh -> slope, flood, sun or visibility analyses; Plot Mesh -> plot-level quantities.",
                },
                References = new[]
                {
                    "Grasshopper Delaunay solver (Grasshopper.Kernel.Geometry.Delaunay).",
                },
            },

            ["TerrainSections"] = new Note
            {
                Title = "Terrain Sections",
                Method = new[]
                {
                    "For each mesh, its bounding box is taken in the RotationPlane orientation. SectionsX cut planes (spanned by the plane X axis and Z axis) are placed at equal steps of width / (SectionsX + 1) along the plane Y axis, so no cut sits on the edges; SectionsY works the same way along the X axis.",
                    "Each plane is intersected with the mesh (mesh-plane intersection polylines); every polyline piece goes to SectionOutlinesX or SectionOutlinesY, with X-SEC n or Y-SEC n labels 2 units beyond the first and last piece.",
                    "With LayoutFlat on, each section is rotated flat into the XY plane (X sections: along-section direction to X, height to Y; Y sections: along-section to Y, height to X) X sections stacked in +Y and Y sections in +X of the rotation plane, beside the bounding box of all meshes with a gap of 5 % of its diagonal, continuing across meshes so they do not overlap.",
                    "SectionMetadata gives, per section, a JSON object with id, plane_origin (x,y,z, invariant culture) and direction, whether or not LayoutFlat is on.",
                    "With Bake true, sections are baked to Site Sections::Terrain Sections and Site Sections::Unrolled Sections in a group named BakeName, replacing objects previously baked with the same BakeName and reusing its group; with an empty BakeName it bakes once each time Bake turns on.",
                },
                Assumptions = new[]
                {
                    "Meshes are terrain or site models; units are model units (label offset of 2 assumes metres).",
                    "RotationPlane Z axis is vertical for true vertical sections.",
                },
                Simplifications = new[]
                {
                    "Flat sections keep only shape: their position on the sheet is relative, so absolute elevations and chainage are not kept and must be annotated separately.",
                    "Section positions depend on each mesh own bounding box, so with several meshes the cuts do not line up between them.",
                },
                Workflow = new[]
                {
                    "Terrain mesh -> TargetMeshes; FlatSections and flat labels -> layout drawings; 3D outlines -> site model views.",
                },
            },

            ["ThermalComfortAnalyzer"] = new Note
            {
                Title = "Hygrothermal Comfort",
                Method = new[]
                {
                    "Apparent temperature (Steadman, shade version as used by the Australian Bureau of Meteorology): e = (RH / 100) × 6.105 × exp(17.27 T / (237.7 + T)) hPa, AT = T + 0.33 e − 0.70 v − 4.00 °C, with T = Temperature, RH = Humidity and v = each VelocityValues speed. When VelocityValues and TagPoints counts differ, a warning is shown and nothing is computed.",
                    "Best / Worst: for each TagPoint the deviation |AT − IdealTemperature| is computed; points within ComfortTolerance × (max − min deviation) of the smallest deviation are Best, those within it of the largest are Worst.",
                    "ComfortMesh: each TerrainMesh vertex takes the AT of its exact nearest TagPoint (hash-grid search widened until no closer point is possible, brute force as a fallback).",
                    "Vertex colours are the deviation scaled between the mesh minimum and maximum, on CustomColors or a default green to red ramp.",
                },
                Assumptions = new[]
                {
                    "Temperature and Humidity are single values for the whole site; only wind varies in space.",
                    "VelocityValues are mean wind speeds in m/s matched index by index to TagPoints.",
                },
                Simplifications = new[]
                {
                    "No solar or long-wave radiation, clothing or activity: shade AT only, so sunny and shaded spots read the same.",
                    "Steadman's AT uses wind speed at 10 m height; pedestrian-level CFD speeds are lower, so AT reads warmer than the published scale.",
                    "Colours are relative to the run (min to max deviation), not an absolute comfort scale.",
                    "The mesh mapping is nearest-sample, not interpolated, so the colouring shows steps at the sample spacing.",
                },
                Workflow = new[]
                {
                    "Fast CFD or wind engine TagPoints and VelocityValues -> TagPoints, VelocityValues; terrain -> TerrainMesh.",
                    "ComfortValues -> Analysis Heatmap for UTCI-style bands; Dashboard Data -> Analysis Dashboard.",
                },
                References = new[]
                {
                    "Steadman R.G. (1994), Norms of apparent temperature in Australia, Australian Meteorological Magazine 43.",
                    "Australian Bureau of Meteorology, Thermal comfort observations: apparent temperature formula.",
                },
            },

            ["TileGridGenerator"] = new Note
            {
                Title = "Tile Pattern",
                Method = new[]
                {
                    "A plane is taken from the base: the curve's own plane, or the surface or single planar Brep face normal at its mid parameters. Its origin is the area centroid, or Setout Point projected onto the plane. Direction (projected) sets the grid X axis and Rotation turns it in degrees.",
                    "Cells are generated over the boundary's bounding box in that plane. Rectangular and offset rectangular use pitch X Dim + Grout by Y Dim + Grout, offset rows shifted by half a pitch; each tile is X Dim × Y Dim.",
                    "Hexagonal uses flat-topped hexagons with circumradius X Dim / 2 at spacing 0.75 × X Dim by √3/2 × X Dim; triangular uses equilateral triangles of side X Dim. Grout shrinks each tile so the joint is Grout wide; Y Dim is ignored for these two.",
                    "A tile counts as full only when all its corners and centre are inside and the boundary does not cross it. Otherwise, if any of these points is inside or its outline crosses the boundary, it is intersected with the boundary (curve boolean, tolerance 0.001) and the pieces are output as trimmed.",
                    "Tile Status is 0 for full and 1 for each trimmed piece, in the same order as All Tiles.",
                },
                Assumptions = new[]
                {
                    "The base is planar and closed; for a Brep the outer loop of the face is the boundary.",
                    "Grid Type is one of rectangular, offset_rectangular, hexagonal, triangular (lower case after conversion).",
                },
                Simplifications = new[]
                {
                    "Tiles are outline curves only, without thickness.",
                    "Holes in a surface or Brep are ignored, with a warning.",
                    "Cell sizes must be positive, and grids estimated at more than 250,000 cells are refused with an error.",
                },
                Workflow = new[]
                {
                    "Facade panel or floor surface -> Base; Full Tiles and Trimmed Tiles -> counts, areas or extrusions.",
                },
            },

            ["TopologySplitEdgeClassifier"] = new Note
            {
                Title = "Topology Split Edge Classifier",
                Method = new[]
                {
                    "Room curves in each branch are grouped into clusters: two rooms join if their curves intersect or if one of 5 test points per segment of one lies within Tolerance of the other (R-Tree search with boxes grown by max(0.1 m, 2 × Tolerance), union-find).",
                    "Per cluster, the rooms are boolean-unioned into FloorSlabs and flattened to Z = 0.",
                    "All room curves are exploded into segments, and each segment is split at the point intersections it has with the others and at the ends of collinear overlaps, so a wall shared over part of its length is split into an internal shared part and an external remainder. Split points closer than Tolerance (distance) to a segment end are ignored.",
                    "Pieces are compared in pairs: if the midpoint of a piece lies within Tolerance of another unmatched piece, both are internal partitions; pieces without a match are external edges. All output curves are flattened to Z = 0.",
                    "MetadataOut gives one JSON text per output curve: midpoint (origin), its Z as true_z, and type internal or external. Output branch paths are the input path plus the cluster index.",
                },
                Assumptions = new[]
                {
                    "Rooms are closed planar outlines that share walls exactly (within Tolerance, default 0.001 model units).",
                    "Each shared wall is shared by exactly two rooms.",
                },
                Simplifications = new[]
                {
                    "Shared walls appear twice in InternalPartitions, once from each room.",
                    "MetadataOut mixes internal and external entries, so it is not index-aligned with either curve output.",
                },
                Workflow = new[]
                {
                    "Room or zone outlines per floor -> RoomCurves; ExternalEdges -> facade modules; FloorSlabs -> slab extrusion.",
                },
            },

            ["TurntableCamera"] = new Note
            {
                Title = "Turntable Camera",
                Method = new[]
                {
                    "Places Frames camera points evenly on a horizontal circle of Radius around Target, at Elevation above it, starting on the +X axis and turning anticlockwise; Orbit Path is the closed polyline through them.",
                    "When Run is True and Directory is set, it moves the active viewport camera to each point looking at Target, captures it with Rhino ViewCapture at Width × Height (no grid or axes, optional transparency and scaled screen items) and saves Prefix0000.png and so on with DPI metadata; the original view is then restored.",
                },
                Assumptions = new[]
                {
                    "The active viewport and its display mode are the ones to render.",
                },
                Simplifications = new[]
                {
                    "Lens and up vector are kept from the current viewport. Exports run on every solution while Run stays True.",
                },
            },

            ["VArrow"] = new Note
            {
                Title = "Vector Arrow Generator",
                Method = new[]
                {
                    "For each line, a rectangular shaft of BodyWidth and a triangular head of HeadWidth × HeadLength are drawn in the plan perpendicular of the line (XY), at the end (Mode 0), the start (1) or both (2). The shaft is shortened by 0.9 × HeadLength under each head so they overlap slightly.",
                    "Shaft and heads are unioned into one outline (Arrow2D) and meshed as a planar boundary (ArrowMesh). Lines shorter than 1.1 × HeadLength (2.2 × HeadLength in Double mode) are skipped and counted in a remark.",
                },
                Assumptions = new[]
                {
                    "Lines are horizontal or nearly so; widths and lengths are in model units.",
                },
                Simplifications = new[]
                {
                    "Vertical lines give a degenerate outline; any Mode other than 0 to 2 draws the shaft without heads.",
                },
                Workflow = new[]
                {
                    "Movement or wind direction lines -> Lines; ArrowMesh -> Custom Preview for diagrams.",
                },
            },

            ["VerticalProjection"] = new Note
            {
                Title = "Vertical Projection",
                Method = new[]
                {
                    "Intersects a vertical line through Point (±10,000 units along world Z) with Plane and returns that point; Is Contained is True when the point is within 1e-6 of the plane.",
                },
                Assumptions = new[]
                {
                    "The plane is not vertical; for a vertical plane there is no intersection and the original point is returned.",
                },
                Simplifications = new[]
                {
                    "None beyond a single line-plane intersection.",
                },
            },

            ["WaterFlow"] = new Note
            {
                Title = "Raindrop Flow Engine",
                Method = new[]
                {
                    "Seeds a regular XY grid of drops at GridSpacing over the mesh bounding box and projects them straight down onto TerrainMesh; each hit is a drop point. GridSpacing and StepSize must be greater than 0; more than 1,000,000 seeds is refused with an error.",
                    "From each drop, the downhill direction on the current face is the steepest-descent vector (n × g) × n, with n the face normal and g pointing down.",
                    "The drop moves StepSize along that direction, is pulled back to the closest mesh point, and the step is kept only if it is at least 1 mm lower; otherwise the path ends.",
                    "Paths also end on near-flat faces (normal Z ≥ 0.9999) or after MaxSteps. Each path is output as a polyline in its own branch.",
                },
                Assumptions = new[]
                {
                    "Rain falls evenly over the bounding box; the mesh is a single-valued terrain surface in metres.",
                    "StepSize is small compared with the mesh faces, so each step stays on the slope it started on.",
                },
                Simplifications = new[]
                {
                    "Drops do not interact: no flow accumulation, ponding, infiltration or merging of paths.",
                    "Paths stop in pits and on flats instead of filling or crossing them; a large StepSize can overshoot a valley and stop early on the opposite slope.",
                    "Path length is limited to MaxSteps × StepSize.",
                    "Branch numbers follow the projected hits, not the seeds: on overhangs one seed can give several drops.",
                },
                Workflow = new[]
                {
                    "Terrain mesh (Site Fetch, Terrain Generator Pro) -> TerrainMesh. FlowPaths -> Analysis Heatmap or drainage sketches; use Hydro-DEM for flow accumulation.",
                },
            },

            ["WindEngineHTVer"] = new Note
            {
                Title = "Urban Wind Vector Engine HT (Beta)",
                Method = new[]
                {
                    "A grid of GridSpacing is laid over the terrain bounding box; each cell is ray-cast down to the terrain and lifted by AnalysisHeight. The wind direction is projected onto the local face plane and speed is scaled by (1 + dirZ × SlopeSpeedup), so uphill flow speeds up and downhill flow slows down.",
                    "Points inside a closed building mesh get speed 0. Otherwise three factors multiply the baseline speed: wake, corner and gap.",
                    "Wake: a ray is cast upwind; if it hits a building at distance d below L = min(building height × WakeLengthRatio, 0.35 × site diagonal), the factor is max(0.12, (d / L)²).",
                    "Corner: for every building wall within 2.5 × GridSpacing whose normal is nearly parallel to the flow (|n · dir| < CornerTangency), the direction is bent towards the wall tangent with weight b = 1 − dist / (2.5 × GridSpacing) and speed is multiplied by (1 + CornerBoost × b). When several buildings qualify, the bend direction is taken from the nearest qualifying wall; their speed boosts still multiply.",
                    "Gap: if another building within the same radius has a wall facing the nearest one (normals dot product below −0.2), speed is multiplied by 1 + GapBoost × (1 − corridor width / (5 × GridSpacing)). GapBoost 0.45 caps the gap speed-up near the 1.45 to 1.5 times seen in wind-tunnel and CFD studies.",
                    "With Iterations above 1 each point re-evaluates the factors from its own previous direction and blends the result with Damping; no information is exchanged between points.",
                    "Outputs: tiles coloured by speed normalised between the run minimum and maximum (CustomColors or a default blue to pink ramp), vectors of length 0.5 × speed, and streamlines traced from seed cells by stepping 0.2 units along the nearest cell direction until they leave the grid, hit a solid cell or reach MaxSteps. Comfort % is the share of open points at or below 5 m/s.",
                },
                Assumptions = new[]
                {
                    "WindSpeed is the reference speed at pedestrian height in m/s; model units are metres.",
                    "ContextBuildings are closed meshes; their height is the bounding-box height.",
                    "The terrain is a height field seen from above; one wind direction per run.",
                },
                Simplifications = new[]
                {
                    "This is a rule-based screening model, not CFD: no pressure or continuity solve, no turbulence, no vertical wind profile, and no interaction between neighbouring points.",
                    "Wake, corner and gap factors are empirical rules of thumb with tunable coefficients; results show patterns, not reliable absolute speeds or comfort classes.",
                    "Colours are relative to the run speed range, and the comfort share uses a single 5 m/s threshold, not a Lawson or similar criterion with frequency of exceedance.",
                    "Streamlines use the nearest-cell direction (no interpolation) with a fixed 0.2 step; without SeedBox the default seeds follow an irregular stride over the grid, not a regular upwind row.",
                    "The gap rule uses one closest point per building, so corridors inside one U-shaped or courtyard building are not detected.",
                },
                Workflow = new[]
                {
                    "Terrain mesh -> TerrainMesh; context massing (closed meshes) -> ContextBuildings; prevailing wind direction and speed from a wind rose.",
                    "Dashboard Data -> Legend Geometry; for more reliable results use Fast CFD and Streamlines.",
                },
                References = new[]
                {
                    "Lawson T.V. (1978), The wind content of the built environment: pedestrian wind comfort criteria (for context on the 5 m/s comfort threshold).",
                },
            },

            ["WindRose"] = new Note
            {
                Title = "Wind Rose",
                Method = new[]
                {
                    "Reads wind direction (field 21) and speed (field 22) from every EPW hour, optionally filtered by Months and Hours (EPW hour 1-24 is taken as the hour starting one earlier); 999 codes are skipped. Or uses the Directions and Speeds lists.",
                    "Hours with speed below Calm (default 0.5 m/s) are counted as calm. The others are binned into Sectors (default 16, 4-72) centred on N and into speed bands (lower edges, default 0, 2, 4, 6, 8, 10 m/s).",
                    "Frequency = hours in a sector and band / all hours (calm included) × 100.",
                    "Petals start at a calm circle of 0.12 × Radius and grow linearly with frequency up to Radius at the top ring (rounded up to a nice step), stacked by speed band, with a 10 % gap between sectors. Bearings are turned into the model with North (degrees anticlockwise from +Y).",
                    "The Selector picks a sector; Speed is the mean, median, P90 (linear interpolation) or max of its non-calm hours, Frequency its share. Direction is the Selector angle; Speed and Frequency are statistics of the whole sector containing it (a remark names the sector centre when they differ).",
                },
                Assumptions = new[]
                {
                    "Directions are meteorological (where the wind comes FROM, 0 = N, clockwise); EPW speeds are 10 m station values.",
                    "The weather station represents the site.",
                },
                Simplifications = new[]
                {
                    "Counts hours only: no correction for height, terrain roughness or local obstacles.",
                    "All filtered hours weigh the same; calm hours have no direction.",
                },
                Workflow = new[]
                {
                    "EPW file path -> EPW; Direction and Speed -> Fast CFD Direction and WindSpeed; Dashboard Data -> Analysis Dashboard.",
                },
                References = new[]
                {
                    "EnergyPlus Auxiliary Programs documentation, EPW weather file format (fields 21 wind direction and 22 wind speed).",
                },
            },
        };
    }
}
