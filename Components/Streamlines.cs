using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class Streamlines : GH_Component
    {
        public Streamlines()
          : base("Streamlines", "Streamlines",
              "Generates smooth streamlines (particle traces) through any vector field using Inverse Distance Weighting.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("FieldPoints", "FieldPoints", "The locations of the vector field data", GH_ParamAccess.list);
            pManager.AddVectorParameter("FieldVectors", "FieldVectors", "The vectors corresponding to each FieldPoint", GH_ParamAccess.list);
            pManager.AddPointParameter("SeedPoints", "SeedPoints", "The starting locations for the streamlines", GH_ParamAccess.list);
            pManager.AddNumberParameter("StepSize", "StepSize", "Distance to move per integration step", GH_ParamAccess.item, 1.0);
            pManager.AddIntegerParameter("MaxSteps", "MaxSteps", "Maximum number of steps per streamline", GH_ParamAccess.item, 500);
            pManager.AddNumberParameter("SearchRadius", "SearchRadius", "Radius to search for blending local vectors", GH_ParamAccess.item, 6.0);
            
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Streamlines", "Streamlines", "The generated streamline curves", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "Info", "Component documentation and stats", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> fPts = new List<Point3d>();
            List<Vector3d> fVecs = new List<Vector3d>();
            List<Point3d> seeds = new List<Point3d>();
            
            if (!DA.GetDataList(0, fPts)) return;
            if (!DA.GetDataList(1, fVecs)) return;
            if (!DA.GetDataList(2, seeds)) return;
            
            double stepSize = 1.0; DA.GetData(3, ref stepSize);
            int maxSteps = 500; DA.GetData(4, ref maxSteps);
            double searchRadius = 6.0; DA.GetData(5, ref searchRadius);

            if (fPts.Count == 0 || fVecs.Count == 0 || fPts.Count != fVecs.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "FieldPoints and FieldVectors must have the same number of items.");
                return;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            RTree tree = new RTree();
            for (int i = 0; i < fPts.Count; i++)
            {
                tree.Insert(fPts[i], i);
            }

            List<Curve> outCurves = new List<Curve>();

            foreach (var seed in seeds)
            {
                List<Point3d> trace = new List<Point3d>();
                Point3d current = seed;
                trace.Add(current);

                for (int step = 0; step < maxSteps; step++)
                {
                    if (!SampleVector2D(current, tree, fPts, fVecs, searchRadius, out Vector3d v, out double z))
                    {
                        break; 
                    }

                    double speed = v.Length;
                    if (speed < 0.05) break; 

                    current.Z = z;
                    if (step == 0) trace[0] = current; 

                    v.Unitize();
                    Point3d next = current + v * stepSize;
                    
                    trace.Add(next);
                    current = next;
                }

                if (trace.Count > 1)
                {
                    Polyline pl = new Polyline(trace);
                    Curve crv = new PolylineCurve(pl);
                    
                    if (crv.IsValid)
                    {
                        outCurves.Add(crv);
                    }
                }
            }

            sw.Stop();
            
            DA.SetDataList(0, outCurves);
            
            string infoStr = 
                "UNIVERSAL STREAMLINES\n" +
                "=====================\n\n" +
                "HOW IT WORKS:\n" +
                "This node uses Inverse Distance Weighting (IDW) to smoothly interpolate any vector field. It builds an R-Tree for lightning-fast spatial lookups, dropping particles at your SeedPoints and integrating their path step-by-step through the field.\n\n" +
                "2.5D SMART DRAPING:\n" +
                "The search algorithm ignores Z-height during the vector lookup, but blends the Z-height of the local field points. This means your streamlines will automatically drape over the terrain exactly like the FastCFD vectors do, without needing the terrain mesh!\n\n" +
                "INPUTS:\n" +
                "- StepSize: Smaller = smoother curves but shorter total travel. Larger = covers more distance but jagged.\n" +
                "- SearchRadius: Must be larger than the CellSize of your CFD grid (default 6.0m). If too small, particles will hit 'dead zones' and stop.";
                
            DA.SetData(1, infoStr);

            Message = $"STREAMLINES\nTime: {sw.ElapsedMilliseconds} ms\n---\nLines: {outCurves.Count}";
        }

        private bool SampleVector2D(Point3d p, RTree tree, List<Point3d> fPts, List<Vector3d> fVecs, double radius, out Vector3d outVec, out double outZ)
        {
            BoundingBox searchBox = new BoundingBox(p.X - radius, p.Y - radius, -100000, p.X + radius, p.Y + radius, 100000);
            Vector3d blendedVec = Vector3d.Zero;
            double zBlend = 0;
            double totalWeight = 0;
            bool exactHit = false;
            
            tree.Search(searchBox, (sender, args) => {
                if (exactHit) return;
                int i = args.Id;
                double dx = p.X - fPts[i].X;
                double dy = p.Y - fPts[i].Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                
                if (d < 0.001) {
                    blendedVec = fVecs[i];
                    zBlend = fPts[i].Z;
                    totalWeight = 1.0;
                    exactHit = true;
                    return;
                }
                if (d <= radius) {
                    double w = 1.0 / (d * d);
                    blendedVec += fVecs[i] * w;
                    zBlend += fPts[i].Z * w;
                    totalWeight += w;
                }
            });
            
            if (totalWeight > 0) {
                if (!exactHit) {
                    blendedVec /= totalWeight;
                    zBlend /= totalWeight;
                }
                outVec = blendedVec;
                outZ = zBlend;
                return true;
            }
            
            outVec = Vector3d.Zero;
            outZ = p.Z;
            return false;
        }

        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("C4E1D2A9-1234-4ABC-8D9E-F98765432101");
    }
}
