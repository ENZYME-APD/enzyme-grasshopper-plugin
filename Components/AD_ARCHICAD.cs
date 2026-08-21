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
    public class AD_ARCHICAD : GH_Component
    {
        public AD_ARCHICAD()
          : base("Adapter: Archicad Slabs (Tapir)", "AD_ARCHICAD",
              "Adapter for Archicad Slabs using Tapir workflow",
              "Enzyme", "Masterplan (Beta)")
        {
        }

                public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            if (this.Attributes == null) this.CreateAttributes();

            bool hasSources = false;
            foreach (var param in this.Params.Input)
                if (param.SourceCount > 0) { hasSources = true; break; }

            if (!hasSources)
            {
                int ix = 220, ox = 250;
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 10.0, 0.15, ix, -150);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("FloorContours", "FC", "List of floor contours", GH_ParamAccess.tree);
            pManager.AddNumberParameter("FloorHeights", "FH", "List of floor heights", GH_ParamAccess.tree);
            pManager.AddNumberParameter("SnapTolerance", "ST", "Tolerance for healing floor heights", GH_ParamAccess.item, 0.15);
            pManager.AddTextParameter("Programs", "P", "List of program names", GH_ParamAccess.tree);
            pManager.AddTextParameter("TowerIDs", "TID", "List of tower IDs", GH_ParamAccess.tree);
            pManager.AddTextParameter("BuildingNames", "BN", "List of building names", GH_ParamAccess.tree);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        
            for (int i = 0; i < pManager.ParamCount; i++) { pManager[i].Optional = true; }
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Payload", "J", "Output JSON payload", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> contoursTree)) return;
            var floorContours = new List<Curve>();
            foreach (var path in contoursTree.Paths)
            {
                var branch = contoursTree.get_Branch(path);
                foreach (GH_Curve ghCurve in branch)
                {
                    if (ghCurve != null && ghCurve.Value != null)
                        floorContours.Add(ghCurve.Value);
                    else
                        floorContours.Add(null);
                }
            }

            if (!DA.GetDataTree(1, out GH_Structure<GH_Number> heightsTree)) return;
            var floorHeights = new List<double>();
            foreach (var path in heightsTree.Paths)
            {
                var branch = heightsTree.get_Branch(path);
                foreach (GH_Number ghNum in branch)
                {
                    if (ghNum != null) floorHeights.Add(ghNum.Value);
                }
            }

            double snapTolerance = 0.15;
            DA.GetData(2, ref snapTolerance);

            var programs = new List<string>();
            if (DA.GetDataTree(3, out GH_Structure<GH_String> progsTree))
            {
                foreach (var path in progsTree.Paths)
                {
                    var branch = progsTree.get_Branch(path);
                    foreach (GH_String ghStr in branch)
                    {
                        if (ghStr != null) programs.Add(ghStr.Value);
                    }
                }
            }

            var towerIds = new List<string>();
            if (DA.GetDataTree(4, out GH_Structure<GH_String> towersTree))
            {
                foreach (var path in towersTree.Paths)
                {
                    var branch = towersTree.get_Branch(path);
                    foreach (GH_String ghStr in branch)
                    {
                        if (ghStr != null) towerIds.Add(ghStr.Value);
                    }
                }
            }

            var buildingNames = new List<string>();
            if (DA.GetDataTree(5, out GH_Structure<GH_String> bnamesTree))
            {
                foreach (var path in bnamesTree.Paths)
                {
                    var branch = bnamesTree.get_Branch(path);
                    foreach (GH_String ghStr in branch)
                    {
                        if (ghStr != null) buildingNames.Add(ghStr.Value);
                    }
                }
            }

            if (floorContours.Count == 0 || floorHeights.Count == 0)
            {
                
                return;
            }

            int limit = Math.Min(floorContours.Count, floorHeights.Count);
            var raw_floors = new List<ADArchicadFloor>();

            for (int i = 0; i < limit; i++)
            {
                var crv = floorContours[i];
                if (crv == null) continue;

                double h = floorHeights[i];
                string prog = (programs.Count > i) ? programs[i] : "Mixed_Use";
                string tid = (towerIds.Count > i) ? towerIds[i] : "Main_Mass";
                string bname = (buildingNames.Count > i) ? buildingNames[i] : "Building_01";

                var bbox = crv.GetBoundingBox(true);
                double min_z = bbox.Min.Z;
                double max_z = min_z + h;

                raw_floors.Add(new ADArchicadFloor
                {
                    bname = bname,
                    tid = tid,
                    prog = prog,
                    min_z = min_z,
                    max_z = max_z,
                    crv = crv.DuplicateCurve()
                });
            }

            var buildings = new Dictionary<string, BuildingData>();
            foreach (var f in raw_floors)
            {
                if (!buildings.ContainsKey(f.bname))
                {
                    buildings[f.bname] = new BuildingData
                    {
                        true_base_elevation = double.PositiveInfinity,
                        towers = new Dictionary<string, List<ADArchicadFloor>>()
                    };
                }

                if (!buildings[f.bname].towers.ContainsKey(f.tid))
                {
                    buildings[f.bname].towers[f.tid] = new List<ADArchicadFloor>();
                }

                buildings[f.bname].towers[f.tid].Add(f);
            }

            int healed_blocks = 0;
            int total_heals_applied = 0;

            foreach (var kvp in buildings)
            {
                var bdata = kvp.Value;
                foreach (var towerKvp in bdata.towers)
                {
                    var floors = towerKvp.Value;
                    floors.Sort((a, b) => a.min_z.CompareTo(b.min_z));

                    double? current_top_z = null;
                    for (int i = 0; i < floors.Count; i++)
                    {
                        var f = floors[i];
                        double healed_min_z = f.min_z;

                        if (current_top_z.HasValue)
                        {
                            double gap = f.min_z - current_top_z.Value;
                            if (Math.Abs(gap) <= snapTolerance && Math.Abs(gap) > 0.001)
                            {
                                healed_min_z = current_top_z.Value;
                                total_heals_applied++;
                            }
                        }

                        double healed_height = f.max_z - healed_min_z;
                        if (healed_min_z < bdata.true_base_elevation)
                        {
                            bdata.true_base_elevation = healed_min_z;
                        }

                        f.healed_min_z = healed_min_z;
                        f.healed_height = healed_height;
                        current_top_z = f.max_z;
                    }
                }
            }

            var output_buildings = new List<Dictionary<string, object>>();
            foreach (var kvp in buildings)
            {
                string bname = kvp.Key;
                var bdata = kvp.Value;
                double true_base = bdata.true_base_elevation;
                var bldg_blocks = new List<Dictionary<string, object>>();

                foreach (var towerKvp in bdata.towers)
                {
                    string tid = towerKvp.Key;
                    foreach (var f in towerKvp.Value)
                    {
                        double relative_z = f.healed_min_z - true_base;
                        var block_dict = new Dictionary<string, object>
                        {
                            { "name", $"Floor_{Math.Round(relative_z)}_{tid}" },
                            { "tower_id", tid },
                            { "program", f.prog },
                            { "floor_height", f.healed_height },
                            { "floors", 1 },
                            { "base_z", Math.Round(relative_z, 3) },
                            { "boundary_segments", SerializeExactCurve(f.crv) }
                        };
                        bldg_blocks.Add(block_dict);
                        healed_blocks++;
                    }
                }

                output_buildings.Add(new Dictionary<string, object>
                {
                    { "name", bname },
                    { "true_base_elevation", Math.Round(true_base, 3) },
                    { "blocks", bldg_blocks }
                });
            }

            var payloadDict = new Dictionary<string, object>
            {
                { "buildings", output_buildings }
            };

            string JSON_Payload = JsonConvert.SerializeObject(payloadDict, Formatting.Indented);
            DA.SetData(0, JSON_Payload);

            this.Message = $"ARCHICAD ADAPTER\n---\nBuildings: {output_buildings.Count}\nFloors: {healed_blocks}\nHeals Applied: {total_heals_applied}";
        }

        private List<Dictionary<string, object>> SerializeExactCurve(Curve crv)
        {
            var segments_data = new List<Dictionary<string, object>>();

            if (crv.TryGetPolyline(out Polyline poly) && poly.Count > 0)
            {
                var pts = new List<double[]>();
                foreach (var pt in poly)
                {
                    pts.Add(new double[] { Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4) });
                }
                segments_data.Add(new Dictionary<string, object> {
                    { "type", "Polyline" },
                    { "points", pts }
                });
                return segments_data;
            }

            Curve rationalized = crv.ToArcsAndLines(0.05, 0.1, 0.1, 1000.0);
            if (rationalized != null) crv = rationalized;

            Curve[] segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

            foreach (var seg in segments)
            {
                if (seg.IsLinear(0.001))
                {
                    segments_data.Add(new Dictionary<string, object> {
                        { "type", "Line" },
                        { "start", new double[] { Math.Round(seg.PointAtStart.X, 4), Math.Round(seg.PointAtStart.Y, 4), Math.Round(seg.PointAtStart.Z, 4) } },
                        { "end", new double[] { Math.Round(seg.PointAtEnd.X, 4), Math.Round(seg.PointAtEnd.Y, 4), Math.Round(seg.PointAtEnd.Z, 4) } }
                    });
                }
                else if (seg.IsArc(0.001))
                {
                    if (seg.TryGetArc(out Arc arc))
                    {
                        segments_data.Add(new Dictionary<string, object> {
                            { "type", "Arc" },
                            { "start", new double[] { Math.Round(arc.StartPoint.X, 4), Math.Round(arc.StartPoint.Y, 4), Math.Round(arc.StartPoint.Z, 4) } },
                            { "mid", new double[] { Math.Round(arc.MidPoint.X, 4), Math.Round(arc.MidPoint.Y, 4), Math.Round(arc.MidPoint.Z, 4) } },
                            { "end", new double[] { Math.Round(arc.EndPoint.X, 4), Math.Round(arc.EndPoint.Y, 4), Math.Round(arc.EndPoint.Z, 4) } }
                        });
                    }
                }
                else
                {
                    Curve poly_crv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0);
                    if (poly_crv != null && poly_crv.TryGetPolyline(out Polyline poly2))
                    {
                        var pts = new List<double[]>();
                        foreach (var pt in poly2)
                        {
                            pts.Add(new double[] { Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4) });
                        }
                        segments_data.Add(new Dictionary<string, object> {
                            { "type", "Polyline" },
                            { "points", pts }
                        });
                    }
                }
            }
            return segments_data;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AD_ARCHICAD.png");

        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public override Guid ComponentGuid
        {
            get { return new Guid("1f6b432a-5a9e-4e42-b054-933e4b75a133"); }
        }

        class ADArchicadFloor
        {
            public string bname;
            public string tid;
            public string prog;
            public double min_z;
            public double max_z;
            public Curve crv;
            public double healed_min_z;
            public double healed_height;
        }

        class BuildingData
        {
            public double true_base_elevation;
            public Dictionary<string, List<ADArchicadFloor>> towers;
        }
    }
}
