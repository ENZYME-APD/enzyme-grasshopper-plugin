using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Grasshopper.Components
{
    public class ModSpandrelComponent : GH_Component
    {
        public ModSpandrelComponent()
          : base("Facade Module: Spandrels", "Mod_Spandrel",
              "Generates solid horizontal spandrel bands, vision glass, and variable-depth mullions.",
              "Enzyme", "Masterplan")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Bounds", "B", "Bounds", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "H", "Heights", GH_ParamAccess.tree);
            pManager.AddNumberParameter("BayWidth", "BW", "BayWidth", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("SillHeight", "SH", "SillHeight", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("HeaderDrop", "HD", "HeaderDrop", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("MullionDepth", "MD", "MullionDepth", GH_ParamAccess.item, 0.15);
            pManager.AddBooleanParameter("FullHeightMullions", "FHM", "FullHeightMullions", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("FlipDir", "FD", "FlipDir", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Glass", "G", "Glass", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Spandrels", "S", "Spandrels", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Mullions", "M", "Mullions", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Curve> ghBounds;
            if (!DA.GetDataTree(0, out ghBounds)) return;

            GH_Structure<GH_Number> ghHeights;
            DA.GetDataTree(1, out ghHeights);

            double BayWidth = 1.5;
            DA.GetData(2, ref BayWidth);

            double SillHeight = 0.0;
            DA.GetData(3, ref SillHeight);

            double HeaderDrop = 0.0;
            DA.GetData(4, ref HeaderDrop);

            double MullionDepth = 0.15;
            DA.GetData(5, ref MullionDepth);

            bool FullHeightMullions = false;
            DA.GetData(6, ref FullHeightMullions);

            bool FlipDir = false;
            DA.GetData(7, ref FlipDir);

            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (BayWidth <= 0.0) BayWidth = 1.5;
            if (MullionDepth <= 0.0) MullionDepth = 0.15;
            if (SillHeight < 0.0) SillHeight = 0.0;
            if (HeaderDrop < 0.0) HeaderDrop = 0.0;

            GH_Structure<GH_Brep> outGlass = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outSpandrel = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outMullions = new GH_Structure<GH_Brep>();

            double glassArea = 0, spandrelArea = 0;
            int mullionCount = 0;

            for (int p = 0; p < ghBounds.PathCount; p++)
            {
                GH_Path path = ghBounds.Paths[p];
                var crvs = ghBounds.get_Branch(path);
                
                var hts = new List<double>();
                if (ghHeights != null && ghHeights.PathExists(path))
                {
                    foreach (var ghNum in ghHeights.get_Branch(path))
                    {
                        if (ghNum != null) hts.Add(ghNum.Value);
                    }
                }

                for (int i = 0; i < crvs.Count; i++)
                {
                    if (crvs[i] == null) continue;
                    Curve crv = crvs[i].Value;
                    if (crv == null) continue;

                    double h = (hts.Count > i) ? hts[i] : 4.0;
                    double crvLen = crv.GetLength();

                    if (SillHeight > 0)
                    {
                        Extrusion extSill = Extrusion.Create(crv, SillHeight, false);
                        if (extSill != null)
                        {
                            outSpandrel.Append(new GH_Brep(extSill.ToBrep()), path);
                            spandrelArea += crvLen * SillHeight;
                        }
                    }

                    double visionH = h - SillHeight - HeaderDrop;
                    if (visionH > 0)
                    {
                        Curve crvVision = crv.DuplicateCurve();
                        crvVision.Transform(Transform.Translation(new Vector3d(0, 0, SillHeight)));
                        Extrusion extVision = Extrusion.Create(crvVision, visionH, false);
                        if (extVision != null)
                        {
                            outGlass.Append(new GH_Brep(extVision.ToBrep()), path);
                            glassArea += crvLen * visionH;
                        }
                    }

                    if (HeaderDrop > 0)
                    {
                        Curve crvTop = crv.DuplicateCurve();
                        crvTop.Transform(Transform.Translation(new Vector3d(0, 0, h - HeaderDrop)));
                        Extrusion extTop = Extrusion.Create(crvTop, HeaderDrop, false);
                        if (extTop != null)
                        {
                            outSpandrel.Append(new GH_Brep(extTop.ToBrep()), path);
                            spandrelArea += crvLen * HeaderDrop;
                        }
                    }

                    double mStartZ = FullHeightMullions ? 0.0 : SillHeight;
                    double mHeight = FullHeightMullions ? h : visionH;

                    if (mHeight > 0 && MullionDepth > 0)
                    {
                        Curve[] segments = crv.DuplicateSegments();
                        if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

                        HashSet<string> placedPts = new HashSet<string>();

                        foreach (Curve seg in segments)
                        {
                            double segLen = seg.GetLength();
                            int divCount = Math.Max(1, (int)Math.Round(segLen / BayWidth));

                            double[] tParams = seg.DivideByCount(divCount, true);
                            if (tParams != null)
                            {
                                foreach (double t in tParams)
                                {
                                    Point3d pt = seg.PointAt(t);
                                    string ptKey = $"{Math.Round(pt.X, 3)},{Math.Round(pt.Y, 3)},{Math.Round(pt.Z, 3)}";

                                    if (!placedPts.Add(ptKey)) continue;

                                    Vector3d tan = seg.TangentAt(t);
                                    Vector3d normal = new Vector3d(-tan.Y, tan.X, 0);
                                    normal.Unitize();

                                    if (FlipDir) normal.Reverse();

                                    Line finLine = new Line(pt, pt + (normal * MullionDepth));
                                    finLine.Transform(Transform.Translation(new Vector3d(0, 0, mStartZ)));
                                    Extrusion finExt = Extrusion.Create(finLine.ToNurbsCurve(), mHeight, false);

                                    if (finExt != null)
                                    {
                                        outMullions.Append(new GH_Brep(finExt.ToBrep()), path);
                                        mullionCount++;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            watch.Stop();

            DA.SetDataTree(0, outGlass);
            DA.SetDataTree(1, outSpandrel);
            DA.SetDataTree(2, outMullions);

            if (glassArea > 0 || spandrelArea > 0)
            {
                string msg = string.Format(
                    "MOD: HORIZONTAL BANDS\nTime: {0} ms\nSill: {1:0.0#}m | Drop: {2:0.0#}m\n---\nGlass: {3:N0} SQM\nSolid: {4:N0} SQM",
                    watch.ElapsedMilliseconds, SillHeight, HeaderDrop, glassArea, spandrelArea);
                    
                if (mullionCount > 0) msg += string.Format("\nMullions: {0}", mullionCount);
                
                this.Message = this.NickName + "\n" + msg;
            }
            else
            {
                this.Message = this.NickName + "\nAwaiting Data";
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override Bitmap Icon => IconLoader.Load("Mod_Spandrel.png");

        public override Guid ComponentGuid => new Guid("4b1e5dc4-6e87-43f1-b844-32e67df1ca5d");
    }
}
