using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace Enzyme.Components
{
    public class CWTComponent : GH_Component
    {
        public CWTComponent()
          : base("CWT", "CWT",
              "CWT",
              Enzyme.Utils.TabInfo.TabName, "Facade")
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
                Enzyme.Utils.AutoWireHelper.WireCustomPreview(this, document, 0, System.Drawing.Color.FromArgb(230, 230, 230), 220, -38);
                Enzyme.Utils.AutoWireHelper.WireOutputParam(this, document, 1, "line", 220, 37);
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("curves", "curves", "curves", GH_ParamAccess.tree);
            pManager.AddNumberParameter("modSize", "modSize", "modSize", GH_ParamAccess.tree);
            pManager.AddNumberParameter("tDepth", "tDepth", "tDepth", GH_ParamAccess.tree);
            pManager.AddNumberParameter("tHeight", "tHeight", "tHeight", GH_ParamAccess.tree);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("transoms", "transoms", "transoms", GH_ParamAccess.tree);
            pManager.AddLineParameter("divLines", "divLines", "divLines", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!DA.GetDataTree(0, out GH_Structure<GH_Curve> curves)) return;
            
            DA.GetDataTree(1, out GH_Structure<GH_Number> modSize);
            DA.GetDataTree(2, out GH_Structure<GH_Number> tDepth);
            DA.GetDataTree(3, out GH_Structure<GH_Number> tHeight);

            if (modSize == null) modSize = new GH_Structure<GH_Number>();
            if (tDepth == null) tDepth = new GH_Structure<GH_Number>();
            if (tHeight == null) tHeight = new GH_Structure<GH_Number>();

            // Initialize output trees
            GH_Structure<GH_Brep> transomsTree = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Line> divisionLinesTree = new GH_Structure<GH_Line>();

            // Defaults
            double defaultDepth = 0.2;
            double defaultHeight = 0.1;
            double defaultModSize = 1.0;

            // Containers
            var validCurves = new List<Curve>();
            var curvePaths = new List<GH_Path>();
            var curveIndices = new List<int>(); // Store original indices within each branch
            var depthValues = new List<double>();
            var heightValues = new List<double>();
            var modSizeValues = new List<double>();

            // STEP 1: Collect valid curves and preserve original structure
            int curveIndex = 0;
            foreach (GH_Path path in curves.Paths)
            {
                int branchIndex = 0;
                var branch = curves.get_Branch(path);
                foreach (GH_Curve ghCurve in branch.Cast<GH_Curve>())
                {
                    Curve curve = ghCurve?.Value;
                    if (curve == null || !curve.IsClosed || !curve.IsPlanar())
                    {
                        branchIndex++;
                        continue;
                    }

                    validCurves.Add(curve);
                    curvePaths.Add(path);
                    curveIndices.Add(branchIndex); // Store the index within this branch
                    depthValues.Add(defaultDepth);
                    heightValues.Add(defaultHeight);
                    modSizeValues.Add(defaultModSize);

                    branchIndex++;
                }
                curveIndex++;
            }

            // STEP 2: Check for uniform inputs
            double uniformDepth = defaultDepth;
            bool useUniformDepth = tDepth.DataCount == 1;
            if (useUniformDepth && tDepth.DataCount > 0)
                uniformDepth = GetDoubleValue(tDepth.get_Branch(0)[0], defaultDepth);

            double uniformHeight = defaultHeight;
            bool useUniformHeight = tHeight.DataCount == 1;
            if (useUniformHeight && tHeight.DataCount > 0)
                uniformHeight = GetDoubleValue(tHeight.get_Branch(0)[0], defaultHeight);

            double uniformModSize = defaultModSize;
            bool useUniformModSize = modSize.DataCount == 1;
            if (useUniformModSize && modSize.DataCount > 0)
                uniformModSize = GetDoubleValue(modSize.get_Branch(0)[0], defaultModSize);

            // STEP 3: Assign values per curve with exact path and index matching
            for (int i = 0; i < validCurves.Count; i++)
            {
                GH_Path path = curvePaths[i];
                int branchIndex = curveIndices[i];

                // Assign depth values - exact path and index matching
                if (useUniformDepth)
                    depthValues[i] = uniformDepth;
                else if (tDepth.PathExists(path))
                {
                    var branch = tDepth.get_Branch(path);
                    if (branch.Count > 0)
                    {
                        // Use exact index if available, otherwise use first value
                        if (branchIndex < branch.Count)
                            depthValues[i] = GetDoubleValue(branch[branchIndex], defaultDepth);
                        else if (branch.Count > 0)
                            depthValues[i] = GetDoubleValue(branch[0], defaultDepth);
                    }
                }

                // Assign height values - exact path and index matching
                if (useUniformHeight)
                    heightValues[i] = uniformHeight;
                else if (tHeight.PathExists(path))
                {
                    var branch = tHeight.get_Branch(path);
                    if (branch.Count > 0)
                    {
                        // Use exact index if available, otherwise use first value
                        if (branchIndex < branch.Count)
                            heightValues[i] = GetDoubleValue(branch[branchIndex], defaultHeight);
                        else if (branch.Count > 0)
                            heightValues[i] = GetDoubleValue(branch[0], defaultHeight);
                    }
                }

                // Assign modSize values - exact path and index matching
                if (useUniformModSize)
                    modSizeValues[i] = uniformModSize;
                else if (modSize.PathExists(path))
                {
                    var branch = modSize.get_Branch(path);
                    if (branch.Count > 0)
                    {
                        // Use exact index if available, otherwise use first value
                        if (branchIndex < branch.Count)
                            modSizeValues[i] = GetDoubleValue(branch[branchIndex], defaultModSize);
                        else if (branch.Count > 0)
                            modSizeValues[i] = GetDoubleValue(branch[0], defaultModSize);
                    }
                }
            }

            // STEP 4: Generate transoms and division lines
            for (int i = 0; i < validCurves.Count; i++)
            {
                Curve curve = validCurves[i];
                GH_Path originalPath = curvePaths[i];
                int branchIndex = curveIndices[i];
                double depth = depthValues[i];
                double height = heightValues[i];
                double moduleSize = modSizeValues[i];

                if (!curve.TryGetPlane(out Plane plane)) continue;

                // Ensure the normal points upward for extrusion
                Vector3d extrusionDir = plane.Normal;
                if (extrusionDir.Z < 0)
                    extrusionDir = -extrusionDir;

                // Calculate curve center for orientation
                Point3d curveCenter;
                AreaMassProperties amp = AreaMassProperties.Compute(curve);
                if (amp != null)
                    curveCenter = amp.Centroid;
                else
                {
                    // Fallback - calculate average of curve points
                    int samplePoints = 20;
                    Point3d sum = Point3d.Origin;
                    for (int s = 0; s < samplePoints; s++)
                    {
                        double param = s / (double)(samplePoints - 1);
                        Point3d pt = curve.PointAt(curve.Domain.ParameterAt(param));
                        sum += pt;
                    }
                    curveCenter = sum / samplePoints;
                }

                double length = curve.GetLength();
                int divCount = Math.Max(4, (int)Math.Ceiling(length / moduleSize));

                // For closed curves, make sure we have the correct number of points
                double[] divParams = curve.DivideByCount(divCount, true);
                if (divParams == null) continue;

                // Preserve exact path structure in output
                GH_Path branchPath = originalPath.AppendElement(branchIndex);

                // Process all division points
                int endIdx = divParams.Length;

                for (int j = 0; j < endIdx; j++)
                {
                    double t = divParams[j];
                    Point3d pt = curve.PointAt(t);
                    Vector3d tangent = curve.TangentAt(t);
                    tangent.Unitize();

                    Vector3d perp = Vector3d.CrossProduct(plane.Normal, tangent);
                    perp.Unitize();

                    // Determine direction based on vector from center to point
                    Vector3d toPoint = new Vector3d(pt - curveCenter);
                    toPoint.Unitize();

                    // Check if perp is pointing outward using dot product
                    if (Vector3d.Multiply(perp, toPoint) < 0)
                        perp.Reverse();

                    Point3d endPt = pt + perp * depth;
                    divisionLinesTree.Append(new GH_Line(new Line(pt, endPt)), branchPath);

                    Brep transom = Brep.CreateFromCornerPoints(
                        pt,
                        endPt,
                        endPt + extrusionDir * height,
                        pt + extrusionDir * height,
                        0.001
                    );

                    if (transom != null)
                    {
                        transomsTree.Append(new GH_Brep(transom), branchPath);
                    }
                }
            }

            // Output assignment
            DA.SetDataTree(0, transomsTree);
            DA.SetDataTree(1, divisionLinesTree);
        }

        private double GetDoubleValue(object obj, double fallback)
        {
            if (obj == null) return fallback;
            if (obj is double d) return d;
            if (obj is int i) return i;
            if (obj is GH_Number n) return n.Value;
            if (obj is string s && double.TryParse(s, out double val)) return val;
            return fallback;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("CWT.png");

        public override Guid ComponentGuid => new Guid("B451A2C3-A88B-4C0F-87E7-F11B93A17B87");
    }
}
