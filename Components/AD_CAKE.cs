using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Newtonsoft.Json;

namespace Enzyme.Components
{
    public class AD_CAKE : GH_Component
    {
        public AD_CAKE()
          : base("Adapter: The Sliced Cake", "AD_CAKE",
              "Audits pre-modeled 1-floor-high Breps. Extracts base contours and heights, heals small modeling gaps using a tolerance, and outputs the Universal Data Format for the MP Engine.",
              "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("FloorBreps", "FB", "Individual floor volumes. (MUST BE FLATTENED)", GH_ParamAccess.list);
            pManager.AddTextParameter("Programs", "P", "Program assigned to each Brep.", GH_ParamAccess.list);
            pManager.AddTextParameter("TowerIDs", "TID", "Tower tags for stacking logic.", GH_ParamAccess.list);
            pManager.AddTextParameter("BuildingNames", "BN", "Building grouping tags.", GH_ParamAccess.list);
            pManager.AddNumberParameter("SnapTolerance", "ST", "Distance to heal gaps (e.g., 0.15m).", GH_ParamAccess.item, 0.15);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        
            for (int i = 0; i < pManager.ParamCount; i++) { pManager[i].Optional = true; }
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Payload", "JSON", "Universal Data Format payload", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> floorBreps = new List<Brep>();
            if (!DA.GetDataList(0, floorBreps)) return;

            List<string> programs = new List<string>();
            DA.GetDataList(1, programs);

            List<string> towerIDs = new List<string>();
            DA.GetDataList(2, towerIDs);

            List<string> buildingNames = new List<string>();
            DA.GetDataList(3, buildingNames);

            double snapTolerance = 0.15;
            DA.GetData(4, ref snapTolerance);

            var rawFloors = new List<RawFloor>();

            for (int i = 0; i < floorBreps.Count; i++)
            {
                Brep brep = floorBreps[i];
                if (brep == null) continue;

                string prog = (programs != null && i < programs.Count) ? programs[i] : "Mixed_Use";
                string tid = (towerIDs != null && i < towerIDs.Count) ? towerIDs[i] : "Main_Mass";
                string bname = (buildingNames != null && i < buildingNames.Count) ? buildingNames[i] : "Building_01";

                BoundingBox bbox = brep.GetBoundingBox(true);
                double min_z = bbox.Min.Z;
                double max_z = bbox.Max.Z;

                Plane slice_plane = new Plane(new Point3d(0, 0, min_z + 0.01), Vector3d.ZAxis);
                Curve[] intersections;
                Point3d[] pts;
                bool rc = Rhino.Geometry.Intersect.Intersection.BrepPlane(brep, slice_plane, 0.01, out intersections, out pts);

                if (rc && intersections != null && intersections.Length > 0)
                {
                    var sortedIntersections = intersections.ToList();
                    sortedIntersections.Sort((c1, c2) =>
                    {
                        double a1 = 0, a2 = 0;
                        if (c1.IsClosed)
                        {
                            var mp1 = AreaMassProperties.Compute(c1);
                            if (mp1 != null) a1 = mp1.Area;
                        }
                        if (c2.IsClosed)
                        {
                            var mp2 = AreaMassProperties.Compute(c2);
                            if (mp2 != null) a2 = mp2.Area;
                        }
                        return a2.CompareTo(a1);
                    });

                    Curve base_crv = sortedIntersections[0];
                    base_crv.Translate(new Vector3d(0, 0, -0.01));

                    rawFloors.Add(new RawFloor
                    {
                        BName = bname,
                        TId = tid,
                        Prog = prog,
                        MinZ = min_z,
                        MaxZ = max_z,
                        Crv = base_crv
                    });
                }
            }

            var buildings = new Dictionary<string, BuildingData>();

            foreach (var f in rawFloors)
            {
                if (!buildings.ContainsKey(f.BName))
                {
                    buildings[f.BName] = new BuildingData { TrueBaseElevation = double.PositiveInfinity };
                }
                if (!buildings[f.BName].Towers.ContainsKey(f.TId))
                {
                    buildings[f.BName].Towers[f.TId] = new List<RawFloor>();
                }
                buildings[f.BName].Towers[f.TId].Add(f);
            }

            int healed_blocks = 0;
            int total_heals_applied = 0;

            foreach (var bkvp in buildings)
            {
                string bname = bkvp.Key;
                BuildingData bdata = bkvp.Value;

                foreach (var tkvp in bdata.Towers)
                {
                    string tid = tkvp.Key;
                    List<RawFloor> floors = tkvp.Value;
                    floors.Sort((a, b) => a.MinZ.CompareTo(b.MinZ));

                    double? current_top_z = null;

                    for (int i = 0; i < floors.Count; i++)
                    {
                        RawFloor f = floors[i];
                        double healed_min_z = f.MinZ;

                        if (current_top_z.HasValue)
                        {
                            double gap = f.MinZ - current_top_z.Value;

                            if (Math.Abs(gap) <= snapTolerance && Math.Abs(gap) > 0.001)
                            {
                                healed_min_z = current_top_z.Value;
                                total_heals_applied++;
                            }
                        }

                        double healed_height = f.MaxZ - healed_min_z;

                        if (healed_min_z < bdata.TrueBaseElevation)
                        {
                            bdata.TrueBaseElevation = healed_min_z;
                        }

                        f.HealedMinZ = healed_min_z;
                        f.HealedHeight = healed_height;

                        current_top_z = f.MaxZ;
                    }
                }
            }

            var outputBuildings = new List<object>();

            foreach (var bkvp in buildings)
            {
                string bname = bkvp.Key;
                BuildingData bdata = bkvp.Value;
                double true_base = bdata.TrueBaseElevation;

                var bldg_blocks = new List<object>();

                foreach (var tkvp in bdata.Towers)
                {
                    string tid = tkvp.Key;
                    List<RawFloor> floors = tkvp.Value;

                    foreach (var f in floors)
                    {
                        double relative_z = f.HealedMinZ - true_base;

                        var block_dict = new Dictionary<string, object>
                        {
                            { "name", $"Floor_{Math.Round(relative_z)}_{tid}" },
                            { "tower_id", tid },
                            { "program", f.Prog },
                            { "floor_height", f.HealedHeight },
                            { "floors", 1 },
                            { "base_z", Math.Round(relative_z, 3) },
                            { "boundary_segments", SerializeExactCurve(f.Crv) }
                        };
                        bldg_blocks.Add(block_dict);
                        healed_blocks++;
                    }
                }

                outputBuildings.Add(new
                {
                    name = bname,
                    true_base_elevation = Math.Round(true_base, 3),
                    blocks = bldg_blocks
                });
            }

            var payload_dict = new Dictionary<string, object>
            {
                { "buildings", outputBuildings }
            };

            string jsonPayload = JsonConvert.SerializeObject(payload_dict, Formatting.Indented);

            DA.SetData(0, jsonPayload);

            this.Message = $"SLICED CAKE ADAPTER\n---\nBuildings: {outputBuildings.Count}\nFloors: {healed_blocks}\nHeals Applied: {total_heals_applied}";
        }

        private List<object> SerializeExactCurve(Curve crv)
        {
            var segmentsData = new List<object>();
            Curve rationalized = crv.ToArcsAndLines(0.05, 0.1, 0.1, 1000.0);
            if (rationalized != null)
            {
                crv = rationalized;
            }

            Curve[] segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0)
            {
                segments = new Curve[] { crv };
            }

            foreach (var seg in segments)
            {
                if (seg.IsLinear(0.001))
                {
                    segmentsData.Add(new
                    {
                        type = "Line",
                        start = new[] { Math.Round(seg.PointAtStart.X, 4), Math.Round(seg.PointAtStart.Y, 4), Math.Round(seg.PointAtStart.Z, 4) },
                        end = new[] { Math.Round(seg.PointAtEnd.X, 4), Math.Round(seg.PointAtEnd.Y, 4), Math.Round(seg.PointAtEnd.Z, 4) }
                    });
                }
                else if (seg.IsArc(0.001))
                {
                    if (seg.TryGetArc(out Arc arc))
                    {
                        segmentsData.Add(new
                        {
                            type = "Arc",
                            start = new[] { Math.Round(arc.StartPoint.X, 4), Math.Round(arc.StartPoint.Y, 4), Math.Round(arc.StartPoint.Z, 4) },
                            mid = new[] { Math.Round(arc.MidPoint.X, 4), Math.Round(arc.MidPoint.Y, 4), Math.Round(arc.MidPoint.Z, 4) },
                            end = new[] { Math.Round(arc.EndPoint.X, 4), Math.Round(arc.EndPoint.Y, 4), Math.Round(arc.EndPoint.Z, 4) }
                        });
                    }
                }
                else
                {
                    Curve polyCrv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0);
                    if (polyCrv != null && polyCrv.TryGetPolyline(out Polyline polyline))
                    {
                        var pts = new List<double[]>();
                        foreach (var pt in polyline)
                        {
                            pts.Add(new[] { Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4) });
                        }
                        segmentsData.Add(new
                        {
                            type = "Polyline",
                            points = pts
                        });
                    }
                }
            }
            return segmentsData;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AD_CAKE.png");

        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public override Guid ComponentGuid
        {
            get { return new Guid("11111111-2222-3333-4444-555555555555"); }
        }

        private class RawFloor
        {
            public string BName { get; set; }
            public string TId { get; set; }
            public string Prog { get; set; }
            public double MinZ { get; set; }
            public double MaxZ { get; set; }
            public Curve Crv { get; set; }
            public double HealedMinZ { get; set; }
            public double HealedHeight { get; set; }
        }

        private class BuildingData
        {
            public double TrueBaseElevation { get; set; }
            public Dictionary<string, List<RawFloor>> Towers { get; set; } = new Dictionary<string, List<RawFloor>>();
        }
    }
}
