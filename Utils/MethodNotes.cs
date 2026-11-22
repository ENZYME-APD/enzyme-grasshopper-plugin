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
        };
    }
}
