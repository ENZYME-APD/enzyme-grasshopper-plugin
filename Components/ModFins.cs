using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Masterplan
{
    public class ModFinsComponent : GH_Component
    {
        public ModFinsComponent()
          : base("Facade Module: Vertical Fins", "Mod_Fins",
              "Generates patterned vertical fins, spandrels, and vision glass.",
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
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 2, 0.0, 3.0, 1.5, 160, -75);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 3, 0.0, 2.0, 0.3, 160, -45);
                Enzyme.Utils.AutoWireHelper.WireSlider(this, document, 4, 0.0, 2.0, 0.0, 160, -15);
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 5, "1", 120, 15, 80, 25);
                Enzyme.Utils.AutoWireHelper.WirePanel(this, document, 6, "0", 120, 45, 80, 25);
                Enzyme.Utils.AutoWireHelper.WireToggle(this, document, 7, false, 80, 75);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(150, 200, 255), 150, -75);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 1, System.Drawing.Color.FromArgb(230, 230, 230), 150, -15);
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 2, System.Drawing.Color.FromArgb(230, 230, 230), 150, 45);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Bounds", "B", "Boundary curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Heights", "H", "Heights", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Spacing", "S", "Spacing", GH_ParamAccess.item, 1.5);
            pManager.AddNumberParameter("Depth", "D", "Depth", GH_ParamAccess.item, 0.3);
            pManager.AddNumberParameter("HeaderDrop", "HD", "Header Drop", GH_ParamAccess.item, 0.0);
            pManager.AddTextParameter("HorizPattern", "HP", "Horizontal Pattern", GH_ParamAccess.list, "1");
            pManager.AddTextParameter("VertPattern", "VP", "Vertical Pattern", GH_ParamAccess.item, "0");
            pManager.AddBooleanParameter("FlipDir", "FD", "Flip Direction", GH_ParamAccess.item, false);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Glass", "G", "Vision Glass", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Spandrels", "S", "Spandrels", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Fins", "F", "Fins", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            GH_Structure<GH_Curve> boundsTree;
            if (!DA.GetDataTree(0, out boundsTree)) return;

            GH_Structure<GH_Number> heightsTree;
            DA.GetDataTree(1, out heightsTree);

            double spacing = 1.5;
            DA.GetData(2, ref spacing);
            if (spacing <= 0.0) spacing = 1.5;

            double depth = 0.3;
            DA.GetData(3, ref depth);
            if (depth <= 0.0) depth = 0.3;

            double headerDrop = 0.0;
            DA.GetData(4, ref headerDrop);
            if (headerDrop < 0.0) headerDrop = 0.0;

            List<string> horizPattern = new List<string>();
            if (!DA.GetDataList(5, horizPattern)) horizPattern = new List<string> { "1" };

            string vertPattern = "0";
            DA.GetData(6, ref vertPattern);

            bool flipDir = false;
            DA.GetData(7, ref flipDir);

            // Parse Patterns
            if (horizPattern == null || horizPattern.Count == 0) horizPattern = new List<string> { "1" };
            List<string> cleanHPats = new List<string>();
            foreach (var pat in horizPattern)
            {
                string clean = new string((pat ?? "1").Where(c => c == '0' || c == '1').ToArray());
                cleanHPats.Add(string.IsNullOrEmpty(clean) ? "1" : clean);
            }

            string rawVPat = (vertPattern ?? "0").Trim();
            List<int> vIndices = rawVPat.Where(char.IsDigit).Select(c => (int)char.GetNumericValue(c)).ToList();
            if (vIndices.Count == 0) vIndices.Add(0);

            // Initialize Output Trees
            GH_Structure<GH_Brep> outGlass = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outSpandrel = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Brep> outFins = new GH_Structure<GH_Brep>();

            double glassArea = 0, spandrelArea = 0;
            int finCount = 0;

            // Main Processing Loop
            for (int p = 0; p < boundsTree.PathCount; p++)
            {
                GH_Path path = boundsTree.Paths[p];
                var crvs = boundsTree.get_Branch(path);
                var hts = (heightsTree != null && heightsTree.PathExists(path)) ? heightsTree.get_Branch(path) : new List<GH_Number>();

                for (int i = 0; i < crvs.Count; i++)
                {
                    var ghCrv = crvs[i] as GH_Curve;
                    if (ghCrv == null || ghCrv.Value == null) continue;
                    Curve crv = ghCrv.Value;

                    double h = 4.0;
                    if (hts.Count > i)
                    {
                        var ghNum = hts[i] as GH_Number;
                        if (ghNum != null) h = ghNum.Value;
                    }
                    double crvLen = crv.GetLength();

                    // --- INNER WALL: GLASS & SPANDREL ---
                    double visionH = h - headerDrop;
                    if (visionH > 0)
                    {
                        Extrusion glassExt = Extrusion.Create(crv, visionH, false);
                        if (glassExt != null)
                        {
                            outGlass.Append(new GH_Brep(glassExt.ToBrep()), path);
                            glassArea += crvLen * visionH;
                        }
                    }

                    if (headerDrop > 0)
                    {
                        Curve crvTop = crv.DuplicateCurve();
                        crvTop.Transform(Transform.Translation(new Vector3d(0, 0, visionH)));
                        Extrusion spanExt = Extrusion.Create(crvTop, headerDrop, false);
                        if (spanExt != null)
                        {
                            outSpandrel.Append(new GH_Brep(spanExt.ToBrep()), path);
                            spandrelArea += crvLen * headerDrop;
                        }
                    }

                    // --- PATTERNED FINS ---
                    int vIdx = vIndices[i % vIndices.Count];
                    string activeHStr = cleanHPats[vIdx % cleanHPats.Count];
                    List<char> pattern = activeHStr.ToList();

                    Curve[] segments = crv.DuplicateSegments();
                    if (segments == null || segments.Length == 0) segments = new Curve[] { crv };

                    HashSet<string> placedPts = new HashSet<string>();
                    int finIndex = 0;

                    foreach (Curve seg in segments)
                    {
                        double segLen = seg.GetLength();
                        int divCount = Math.Max(1, (int)Math.Round(segLen / spacing));

                        double[] tParams = seg.DivideByCount(divCount, true);
                        if (tParams != null)
                        {
                            foreach (double t in tParams)
                            {
                                Point3d pt = seg.PointAt(t);
                                string ptKey = $"{Math.Round(pt.X, 3)},{Math.Round(pt.Y, 3)},{Math.Round(pt.Z, 3)}";

                                if (!placedPts.Add(ptKey)) continue;

                                if (pattern[finIndex % pattern.Count] == '1')
                                {
                                    Vector3d tan = seg.TangentAt(t);
                                    Vector3d normal = new Vector3d(-tan.Y, tan.X, 0);
                                    normal.Unitize();

                                    if (flipDir) normal.Reverse();

                                    Line finLine = new Line(pt, pt + (normal * depth));
                                    Extrusion finExt = Extrusion.Create(finLine.ToNurbsCurve(), h, false);
                                    
                                    if (finExt != null)
                                    {
                                        outFins.Append(new GH_Brep(finExt.ToBrep()), path);
                                        finCount++;
                                    }
                                }
                                finIndex++;
                            }
                        }
                    }
                }
            }

            watch.Stop();

            DA.SetDataTree(0, outGlass);
            DA.SetDataTree(1, outSpandrel);
            DA.SetDataTree(2, outFins);

            if (glassArea > 0 || spandrelArea > 0)
            {
                this.Message = string.Format("{0}\nTime: {1} ms\nDrop: {2:0.0#}m\n---\nFins:    {3}\nGlass:   {4:N0} SQM\nSpandrel:{5:N0} SQM",
                    this.NickName, watch.ElapsedMilliseconds, headerDrop, finCount, glassArea, spandrelArea);
            }
            else
            {
                this.Message = this.NickName + "\nAwaiting Data";
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return IconLoader.Load("Mod_Fins.png");
            }
        }

        public override Guid ComponentGuid => new Guid("4673B8A6-880F-470E-BB1E-82B59FC77271");
    }
}
