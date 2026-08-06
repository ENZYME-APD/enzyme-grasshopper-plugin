using System;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Enzyme.Components
{
    public class SegDisp : GH_Component
    {
        public SegDisp()
          : base("Curve Segment Dispatcher", "SegDisp",
              "Explodes curves into Lines and Arcs, extracting Radii, Centers, and visual Dimensions.",
              "Enzyme", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("curves", "curves", "Curves to process", GH_ParamAccess.tree);
            pManager.AddNumberParameter("offset", "offset", "Dimension offset", GH_ParamAccess.item, 10.0);
            pManager.AddBooleanParameter("dim_toggle", "dim_toggle", "Show dimensions", GH_ParamAccess.item, true);
            pManager.AddIntegerParameter("num_dec", "num_dec", "Number of decimals", GH_ParamAccess.item, 2);
            
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("lines", "lines", "Linear segments", GH_ParamAccess.tree);
            pManager.AddCurveParameter("arcs", "arcs", "Arc segments", GH_ParamAccess.tree);
            pManager.AddNumberParameter("radii", "radii", "Arc radii", GH_ParamAccess.tree);
            pManager.AddPointParameter("centers", "centers", "Arc centers", GH_ParamAccess.tree);
            pManager.AddGenericParameter("dims", "dims", "Visual dimensions", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> curvesTree)) return;

            double offset = 10.0;
            DA.GetData(1, ref offset);

            bool dimToggle = true;
            DA.GetData(2, ref dimToggle);

            int numDec = 2;
            DA.GetData(3, ref numDec);

            var treeLines = new GH_Structure<GH_Curve>();
            var treeArcs = new GH_Structure<GH_Curve>();
            var treeRadii = new GH_Structure<GH_Number>();
            var treeCenters = new GH_Structure<GH_Point>();
            var treeDims = new GH_Structure<IGH_Goo>();

            foreach (var path in curvesTree.Paths)
            {
                var branch = curvesTree.get_Branch(path);
                
                // Cast branch elements explicitly as instructed
                foreach (GH_Curve ghCurve in branch.Cast<GH_Curve>())
                {
                    if (ghCurve == null || ghCurve.Value == null) continue;
                    
                    Curve crv = ghCurve.Value;
                    Curve[] segments = crv.DuplicateSegments();
                    if (segments == null || segments.Length == 0)
                    {
                        segments = new Curve[] { crv };
                    }

                    foreach (Curve seg in segments)
                    {
                        if (seg == null) continue;

                        if (seg.IsLinear())
                        {
                            treeLines.Append(new GH_Curve(seg), path);
                        }
                        else
                        {
                            if (seg.TryGetArc(out Arc arcPrim, 0.001))
                            {
                                treeArcs.Append(new GH_Curve(seg), path);
                                treeRadii.Append(new GH_Number(arcPrim.Radius), path);
                                treeCenters.Append(new GH_Point(arcPrim.Center), path);

                                if (dimToggle)
                                {
                                    double midAngle = arcPrim.AngleDomain.Mid;
                                    Point3d ptOnArc = arcPrim.PointAt(midAngle);

                                    Vector3d dirVec = ptOnArc - arcPrim.Center;
                                    dirVec.Unitize();

                                    Point3d offsetPt = ptOnArc + (dirVec * offset);

                                    Curve leaderLine = new Line(ptOnArc, offsetPt).ToNurbsCurve();
                                    
                                    string label = "R " + arcPrim.Radius.ToString("F" + numDec, System.Globalization.CultureInfo.InvariantCulture);
                                    TextDot textDot = new TextDot(label, offsetPt);

                                    treeDims.Append(new GH_Curve(leaderLine), path);
                                    treeDims.Append(new GH_ObjectWrapper(textDot), path);
                                }
                            }
                        }
                    }
                }
            }

            watch.Stop();
            double elapsedMs = watch.Elapsed.TotalMilliseconds;

            string statusStr = dimToggle ? "DIMS: ON" : "DIMS: OFF";
            int totalCount = treeLines.DataCount + treeArcs.DataCount;
            
            this.Message = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} ({1} dec)\n{2} Segs | {3:F2} ms", statusStr, numDec, totalCount, elapsedMs);

            DA.SetDataTree(0, treeLines);
            DA.SetDataTree(1, treeArcs);
            DA.SetDataTree(2, treeRadii);
            DA.SetDataTree(3, treeCenters);
            DA.SetDataTree(4, treeDims);
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("B5D142EF-0158-45F4-A0C3-8B39B2A7EAC4"); }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return IconLoader.Load("SegDisp.png"); }
        }
    }
}
