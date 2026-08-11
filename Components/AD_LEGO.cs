using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class AD_LEGO : GH_Component
    {
        public AD_LEGO()
          : base("Adapter: The Lego Builder", "AD_LEGO",
              "STAGE 1 ADAPTER: THE LEGO BUILDER (METHOD 3) - VERBOSE DIAGNOSTICS & FIXED STACKING",
              "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("MassingBlocks", "MB", "MassingBlocks", GH_ParamAccess.list);
            pManager.AddTextParameter("Programs", "P", "Programs", GH_ParamAccess.list);
            pManager.AddTextParameter("TowerIDs", "TID", "TowerIDs", GH_ParamAccess.list);
            pManager.AddTextParameter("BuildingNames", "BN", "BuildingNames", GH_ParamAccess.list);
            pManager.AddNumberParameter("FloorHeights", "FH", "FloorHeights", GH_ParamAccess.list);
            pManager.AddIntegerParameter("HeightResolution", "HR", "0 = Strict Cutoff, 1 = Stretch Top Floor", GH_ParamAccess.item, 0);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
        
            for (int i = 0; i < pManager.ParamCount; i++) { pManager[i].Optional = true; }
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON_Payload", "J", "JSON Payload", GH_ParamAccess.item);
        }
        
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AD_LEGO.png");
        
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        public override Guid ComponentGuid => new Guid("0a47d2c3-4211-4770-b4bd-5561a34c11b1");

        

        private string DoubleArrToJson(double[] arr)
        {
            return $"[{string.Join(", ", arr.Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";
        }

        private string SerializeExactCurve(Curve crv)
        {
            var segmentsData = new List<string>();
            var rationalized = crv.ToArcsAndLines(0.05, 0.1, 0.1, 1000.0);
            if (rationalized != null) crv = rationalized;

            var segments = crv.DuplicateSegments();
            if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

            foreach (var seg in segments)
            {
                if (seg.IsLinear(0.001))
                {
                    var start = new double[] { Math.Round(seg.PointAtStart.X, 4), Math.Round(seg.PointAtStart.Y, 4), Math.Round(seg.PointAtStart.Z, 4) };
                    var end = new double[] { Math.Round(seg.PointAtEnd.X, 4), Math.Round(seg.PointAtEnd.Y, 4), Math.Round(seg.PointAtEnd.Z, 4) };
                    segmentsData.Add($"{{\"type\": \"Line\", \"start\": {DoubleArrToJson(start)}, \"end\": {DoubleArrToJson(end)}}}");
                }
                else if (seg.IsArc(0.001))
                {
                    if (seg.TryGetArc(out Arc arc))
                    {
                        var start = new double[] { Math.Round(arc.StartPoint.X, 4), Math.Round(arc.StartPoint.Y, 4), Math.Round(arc.StartPoint.Z, 4) };
                        var mid = new double[] { Math.Round(arc.MidPoint.X, 4), Math.Round(arc.MidPoint.Y, 4), Math.Round(arc.MidPoint.Z, 4) };
                        var end = new double[] { Math.Round(arc.EndPoint.X, 4), Math.Round(arc.EndPoint.Y, 4), Math.Round(arc.EndPoint.Z, 4) };
                        segmentsData.Add($"{{\"type\": \"Arc\", \"start\": {DoubleArrToJson(start)}, \"mid\": {DoubleArrToJson(mid)}, \"end\": {DoubleArrToJson(end)}}}");
                    }
                }
                else
                {
                    var polyCrv = seg.ToPolyline(0.01, 0.1, 0.0, 0.0);
                    if (polyCrv != null && polyCrv.TryGetPolyline(out Polyline rc))
                    {
                        var pointsList = new List<string>();
                        foreach (var pt in rc)
                        {
                            pointsList.Add(DoubleArrToJson(new double[] { Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4) }));
                        }
                        segmentsData.Add($"{{\"type\": \"Polyline\", \"points\": [{string.Join(", ", pointsList)}]}}");
                    }
                }
            }
            return $"[{string.Join(", ", segmentsData)}]";
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> massingBlocks = new List<Brep>();
            List<string> programs = new List<string>();
            List<string> towerIDs = new List<string>();
            List<string> buildingNames = new List<string>();
            List<double> floorHeights = new List<double>();
            int heightResolution = 0;

            DA.GetDataList(0, massingBlocks);
            DA.GetDataList(1, programs);
            DA.GetDataList(2, towerIDs);
            DA.GetDataList(3, buildingNames);
            DA.GetDataList(4, floorHeights);
            DA.GetData(5, ref heightResolution);

            var buildingMinZs = new Dictionary<string, double>();
            if (massingBlocks != null)
            {
                for (int i = 0; i < massingBlocks.Count; i++)
                {
                    var brep = massingBlocks[i];
                    if (brep == null) continue;

                    string bname = (buildingNames != null && i < buildingNames.Count) ? buildingNames[i] : "Building_01";
                    double minZ = brep.GetBoundingBox(Plane.WorldXY).Min.Z;

                    if (!buildingMinZs.ContainsKey(bname))
                        buildingMinZs[bname] = minZ;
                    else
                        buildingMinZs[bname] = Math.Min(buildingMinZs[bname], minZ);
                }
            }

            var buildings = new Dictionary<string, BuildingData>();
            int totalGeneratedFloors = 0;
            int stretchedFloorsCreated = 0;

            if (massingBlocks != null)
            {
                for (int i = 0; i < massingBlocks.Count; i++)
                {
                    var brep = massingBlocks[i];
                    if (brep == null) continue;

                    string prog = (programs != null && i < programs.Count) ? programs[i] : "Mixed_Use";
                    string tid = (towerIDs != null && i < towerIDs.Count) ? towerIDs[i] : "Main_Mass";
                    string bname = (buildingNames != null && i < buildingNames.Count) ? buildingNames[i] : "Building_01";
                    
                    double fh = (floorHeights != null && i < floorHeights.Count) ? floorHeights[i] : 4.0;
                    if (fh <= 0) fh = 4.0;

                    var bbox = brep.GetBoundingBox(Plane.WorldXY);
                    double minZ = bbox.Min.Z;
                    double maxZ = bbox.Max.Z;
                    double totalHeight = maxZ - minZ;

                    var slicePlane = new Plane(new Point3d(0, 0, minZ + 0.01), Vector3d.ZAxis);
                    if (Rhino.Geometry.Intersect.Intersection.BrepPlane(brep, slicePlane, 0.01, out Curve[] intersections, out Point3d[] pts))
                    {
                        if (intersections != null && intersections.Length > 0)
                        {
                            var sortedIntersections = intersections.OrderByDescending(c => c.IsClosed ? AreaMassProperties.Compute(c)?.Area ?? 0 : 0).ToList();
                            var baseCrv = sortedIntersections[0];
                            baseCrv.Translate(new Vector3d(0, 0, -0.01));
                            
                            string serializedCrv = SerializeExactCurve(baseCrv);

                            int numFullFloors = (int)(totalHeight / fh);
                            double remainder = Math.Round(totalHeight % fh, 3);

                            if (!buildings.ContainsKey(bname))
                            {
                                buildings[bname] = new BuildingData { TrueBaseElevation = buildingMinZs[bname], Blocks = new List<BlockData>() };
                            }

                            double relativeBaseZ = minZ - buildingMinZs[bname];

                            if (numFullFloors > 0)
                            {
                                if (heightResolution == 1 && remainder > 0.1)
                                {
                                    int standardFloors = numFullFloors - 1;
                                    if (standardFloors > 0)
                                    {
                                        buildings[bname].Blocks.Add(new BlockData
                                        {
                                            Name = $"{prog}_{standardFloors}Fl_{tid}",
                                            TowerID = tid,
                                            Program = prog,
                                            FloorHeight = fh,
                                            Floors = standardFloors,
                                            BaseZ = Math.Round(relativeBaseZ, 3),
                                            BoundarySegmentsJson = serializedCrv
                                        });
                                        totalGeneratedFloors += standardFloors;
                                    }

                                    double topBaseZ = relativeBaseZ + (standardFloors * fh);
                                    double stretchedHeight = fh + remainder;
                                    buildings[bname].Blocks.Add(new BlockData
                                    {
                                        Name = $"{prog}_TopStretched_{tid}",
                                        TowerID = tid,
                                        Program = $"{prog} (Top)",
                                        FloorHeight = Math.Round(stretchedHeight, 3),
                                        Floors = 1,
                                        BaseZ = Math.Round(topBaseZ, 3),
                                        BoundarySegmentsJson = serializedCrv
                                    });
                                    totalGeneratedFloors += 1;
                                    stretchedFloorsCreated += 1;
                                }
                                else
                                {
                                    buildings[bname].Blocks.Add(new BlockData
                                    {
                                        Name = $"{prog}_{numFullFloors}Fl_{tid}",
                                        TowerID = tid,
                                        Program = prog,
                                        FloorHeight = fh,
                                        Floors = numFullFloors,
                                        BaseZ = Math.Round(relativeBaseZ, 3),
                                        BoundarySegmentsJson = serializedCrv
                                    });
                                    totalGeneratedFloors += numFullFloors;
                                }
                            }
                        }
                    }
                }
            }

            var outputBuildings = new List<string>();
            foreach (var kvp in buildings)
            {
                string bname = kvp.Key;
                var bdata = kvp.Value;
                
                var blocksJsonList = new List<string>();
                foreach (var block in bdata.Blocks)
                {
                    blocksJsonList.Add($@"{{
      ""name"": ""{block.Name}"",
      ""tower_id"": ""{block.TowerID}"",
      ""program"": ""{block.Program}"",
      ""floor_height"": {block.FloorHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)},
      ""floors"": {block.Floors},
      ""base_z"": {block.BaseZ.ToString(System.Globalization.CultureInfo.InvariantCulture)},
      ""boundary_segments"": {block.BoundarySegmentsJson}
    }}");
                }

                outputBuildings.Add($@"{{
  ""name"": ""{bname}"",
  ""true_base_elevation"": {Math.Round(bdata.TrueBaseElevation, 3).ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""blocks"": [
    {string.Join(",\n    ", blocksJsonList)}
  ]
}}");
            }

            string finalJson = $@"{{
  ""buildings"": [
    {string.Join(",\n    ", outputBuildings)}
  ]
}}";

            DA.SetData(0, finalJson);

            Message = $"LEGO ADAPTER\n---\nTotal Floors: {totalGeneratedFloors}\nStretched Tops: {stretchedFloorsCreated}";
        }

        private class BuildingData
        {
            public double TrueBaseElevation { get; set; }
            public List<BlockData> Blocks { get; set; }
        }

        private class BlockData
        {
            public string Name { get; set; }
            public string TowerID { get; set; }
            public string Program { get; set; }
            public double FloorHeight { get; set; }
            public int Floors { get; set; }
            public double BaseZ { get; set; }
            public string BoundarySegmentsJson { get; set; }
        }
    }
}
