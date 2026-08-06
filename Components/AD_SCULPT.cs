using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Enzyme.Components
{
    public class AD_SCULPT : GH_Component
    {
        public AD_SCULPT()
          : base("Adapter: The Sculptor", "AD_SCULPT",
              "Adapter: The Sculptor (Method 1) - V2 (Rationalized)",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Massing", "M", "Massing (Brep List)", GH_ParamAccess.list);
            pManager.AddTextParameter("TowerIDs", "T", "TowerIDs (Str List)", GH_ParamAccess.list);
            pManager.AddTextParameter("BuildingNames", "B", "BuildingNames (Str List)", GH_ParamAccess.list);
            pManager.AddTextParameter("RecipeJSON", "R", "RecipeJSON (Str)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("RepeatLast", "RL", "RepeatLast (Bool)", GH_ParamAccess.item);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Payload", "J", "Output JSON Payload", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> massing = new List<Brep>();
            List<string> towerIDs = new List<string>();
            List<string> buildingNames = new List<string>();
            string recipeJSON = null;
            bool repeatLast = false;

            if (!DA.GetDataList(0, massing)) return;
            DA.GetDataList(1, towerIDs);
            DA.GetDataList(2, buildingNames);
            DA.GetData(3, ref recipeJSON);
            DA.GetData(4, ref repeatLast);

            // ==============================================================================
            // 2. PARSE RECIPE
            // ==============================================================================
            var raw_recipe = new List<RecipeItem>();
            try
            {
                if (!string.IsNullOrEmpty(recipeJSON))
                {
                    JArray parsed = JArray.Parse(recipeJSON);
                    foreach (JObject r in parsed)
                    {
                        int floors = r.ContainsKey("floors") ? r["floors"].Value<int>() : 1;
                        string program = r.ContainsKey("program") ? r["program"].Value<string>() : "Office";
                        double height = r.ContainsKey("height") ? r["height"].Value<double>() : 4.0;
                        
                        for (int i = 0; i < floors; i++)
                        {
                            raw_recipe.Add(new RecipeItem { Program = program, Height = height });
                        }
                    }
                }
            }
            catch { }

            if (raw_recipe.Count == 0)
            {
                raw_recipe.Add(new RecipeItem { Program = "Mixed_Use", Height = 4.0 });
            }

            // ==============================================================================
            // 3. INITIALIZE BUILDINGS & FIND BASE ELEVATIONS
            // ==============================================================================
            var buildings = new Dictionary<string, BuildingData>();

            if (massing != null)
            {
                for (int i = 0; i < massing.Count; i++)
                {
                    Brep brep = massing[i];
                    if (brep == null) continue;

                    string tid = (towerIDs != null && i < towerIDs.Count) ? towerIDs[i] : "Main_Mass";
                    string bname = (buildingNames != null && i < buildingNames.Count) ? buildingNames[i] : "Building_01";

                    BoundingBox bbox = brep.GetBoundingBox(true);
                    if (!buildings.ContainsKey(bname))
                    {
                        buildings[bname] = new BuildingData
                        {
                            Name = bname,
                            TrueBaseElevation = bbox.Min.Z,
                            MaxZ = bbox.Max.Z,
                            Breps = new List<Tuple<Brep, string>>(),
                            Blocks = new List<Dictionary<string, object>>()
                        };
                    }
                    else
                    {
                        buildings[bname].TrueBaseElevation = Math.Min(buildings[bname].TrueBaseElevation, bbox.Min.Z);
                        buildings[bname].MaxZ = Math.Max(buildings[bname].MaxZ, bbox.Max.Z);
                    }

                    buildings[bname].Breps.Add(new Tuple<Brep, string>(brep, tid));
                }
            }

            // ==============================================================================
            // 4. GENERATE GLOBAL Z-RULER & SLICE
            // ==============================================================================
            int total_sliced_floors = 0;

            foreach (var kvp in buildings)
            {
                string bname = kvp.Key;
                BuildingData bdata = kvp.Value;
                double true_base = bdata.TrueBaseElevation;
                double max_bldg_z = bdata.MaxZ;

                var z_grid = new List<FloorData>();
                double curr_z = true_base;
                int f_idx = 0;

                while (curr_z + 0.1 < max_bldg_z)
                {
                    string prog;
                    double fh;
                    if (f_idx < raw_recipe.Count)
                    {
                        prog = raw_recipe[f_idx].Program;
                        fh = raw_recipe[f_idx].Height;
                    }
                    else
                    {
                        if (repeatLast)
                        {
                            prog = raw_recipe[raw_recipe.Count - 1].Program;
                            fh = raw_recipe[raw_recipe.Count - 1].Height;
                        }
                        else
                        {
                            break;
                        }
                    }

                    z_grid.Add(new FloorData { TrueZ = curr_z, Prog = prog, Height = fh });
                    curr_z += fh;
                    f_idx += 1;
                }

                foreach (var tuple in bdata.Breps)
                {
                    Brep brep = tuple.Item1;
                    string tid = tuple.Item2;
                    BoundingBox bbox = brep.GetBoundingBox(true);

                    foreach (var floor_data in z_grid)
                    {
                        double z_plane = floor_data.TrueZ;

                        if (z_plane >= bbox.Min.Z - 0.05 && z_plane <= bbox.Max.Z - 0.05)
                        {
                            Plane slice_plane = new Plane(new Point3d(0, 0, z_plane + 0.01), Vector3d.ZAxis);

                            Curve[] intersections;
                            Point3d[] pts;
                            bool rc = Rhino.Geometry.Intersect.Intersection.BrepPlane(brep, slice_plane, 0.01, out intersections, out pts);

                            if (rc && intersections != null)
                            {
                                foreach (Curve crv in intersections)
                                {
                                    Curve crvCopy = crv.DuplicateCurve();
                                    crvCopy.Translate(new Vector3d(0, 0, -0.01));

                                    Curve rationalized = crvCopy.ToArcsAndLines(0.05, 0.1, 0.1, 1000.0);
                                    if (rationalized != null)
                                    {
                                        crvCopy = rationalized;
                                    }

                                    double relative_z = z_plane - true_base;

                                    var block_dict = new Dictionary<string, object>
                                    {
                                        { "name", $"Floor_{Math.Round(relative_z)}_{tid}" },
                                        { "tower_id", tid },
                                        { "program", floor_data.Prog },
                                        { "floor_height", floor_data.Height },
                                        { "floors", 1 },
                                        { "base_z", Math.Round(relative_z, 3) },
                                        { "boundary_segments", SerializeExactCurve(crvCopy) }
                                    };
                                    bdata.Blocks.Add(block_dict);
                                    total_sliced_floors += 1;
                                }
                            }
                        }
                    }
                }
            }

            // ==============================================================================
            // 5. SERIALIZE TO MP ENGINE FORMAT
            // ==============================================================================
            var output_buildings = new List<Dictionary<string, object>>();
            foreach (var kvp in buildings)
            {
                output_buildings.Add(new Dictionary<string, object>
                {
                    { "name", kvp.Key },
                    { "true_base_elevation", Math.Round(kvp.Value.TrueBaseElevation, 3) },
                    { "blocks", kvp.Value.Blocks }
                });
            }

            var payload_dict = new Dictionary<string, object>
            {
                { "buildings", output_buildings }
            };

            string jsonPayload = JsonConvert.SerializeObject(payload_dict, Formatting.Indented);
            DA.SetData(0, jsonPayload);

            this.Message = $"SCULPTOR ADAPTER\n---\nBuildings: {output_buildings.Count}\nFloors Sliced: {total_sliced_floors}";
        }

        // ==============================================================================
        // 1. HELPER: CURVE SERIALIZATION
        // ==============================================================================
        private List<Dictionary<string, object>> SerializeExactCurve(Curve crv)
        {
            var segments_data = new List<Dictionary<string, object>>();
            Curve[] segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0)
            {
                segments = new Curve[] { crv };
            }

            foreach (Curve seg in segments)
            {
                if (seg.IsLinear(0.001))
                {
                    segments_data.Add(new Dictionary<string, object>
                    {
                        { "type", "Line" },
                        { "start", new double[] { Math.Round(seg.PointAtStart.X, 4), Math.Round(seg.PointAtStart.Y, 4), Math.Round(seg.PointAtStart.Z, 4) } },
                        { "end", new double[] { Math.Round(seg.PointAtEnd.X, 4), Math.Round(seg.PointAtEnd.Y, 4), Math.Round(seg.PointAtEnd.Z, 4) } }
                    });
                }
                else if (seg.IsArc(0.001))
                {
                    Arc arc;
                    bool rc = seg.TryGetArc(out arc);
                    if (rc)
                    {
                        segments_data.Add(new Dictionary<string, object>
                        {
                            { "type", "Arc" },
                            { "start", new double[] { Math.Round(arc.StartPoint.X, 4), Math.Round(arc.StartPoint.Y, 4), Math.Round(arc.StartPoint.Z, 4) } },
                            { "mid", new double[] { Math.Round(arc.MidPoint.X, 4), Math.Round(arc.MidPoint.Y, 4), Math.Round(arc.MidPoint.Z, 4) } },
                            { "end", new double[] { Math.Round(arc.EndPoint.X, 4), Math.Round(arc.EndPoint.Y, 4), Math.Round(arc.EndPoint.Z, 4) } }
                        });
                    }
                }
                else
                {
                    PolylineCurve poly_crv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0);
                    if (poly_crv != null)
                    {
                        Polyline polyline;
                        if (poly_crv.TryGetPolyline(out polyline))
                        {
                            var points = new List<double[]>();
                            foreach (Point3d pt in polyline)
                            {
                                points.Add(new double[] { Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4) });
                            }
                            segments_data.Add(new Dictionary<string, object>
                            {
                                { "type", "Polyline" },
                                { "points", points }
                            });
                        }
                    }
                }
            }
            return segments_data;
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("7D50DE24-F428-40F1-BEB4-3E7DBE94F8D7"); }
        }

        private class RecipeItem
        {
            public string Program { get; set; }
            public double Height { get; set; }
        }

        private class BuildingData
        {
            public string Name { get; set; }
            public double TrueBaseElevation { get; set; }
            public double MaxZ { get; set; }
            public List<Tuple<Brep, string>> Breps { get; set; }
            public List<Dictionary<string, object>> Blocks { get; set; }
        }

        private class FloorData
        {
            public double TrueZ { get; set; }
            public string Prog { get; set; }
            public double Height { get; set; }
        }
    }
}
