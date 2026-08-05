using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Enzyme.Terrain
{
    public class ElevationLabel : GH_Component
    {
        public ElevationLabel()
          : base("Elevation Labeler Pro", "ELEV_LABEL",
              "Custom text/elevation labels with radial rotation and auto-sync.",
              "Enzyme", "Terrain")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "Coordinates to label", GH_ParamAccess.tree);
            pManager.AddTextParameter("LabelText", "LT", "Optional text override", GH_ParamAccess.tree);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("Length", "L", "Leader line length", GH_ParamAccess.tree);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Gap", "G", "Gap between line and text", GH_ParamAccess.tree);
            pManager[3].Optional = true;
            pManager.AddTextParameter("Style", "S", "Rhino Text Style name", GH_ParamAccess.tree);
            pManager[4].Optional = true;
            pManager.AddIntegerParameter("TextPlane", "TP", "0=XY, 1=XZ, 2=YZ", GH_ParamAccess.tree);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("Orientation", "O", "Rotation in degrees", GH_ParamAccess.tree);
            pManager[6].Optional = true;
            pManager.AddIntegerParameter("Anchor", "A", "1-9 Justification", GH_ParamAccess.tree);
            pManager[7].Optional = true;
            pManager.AddBooleanParameter("Bake", "B", "Bake toggle/button", GH_ParamAccess.tree);
            pManager[8].Optional = true;
            pManager.AddTextParameter("BakeLayer", "BL", "Target bake layer", GH_ParamAccess.tree);
            pManager[9].Optional = true;
            pManager.AddTextParameter("BakeName", "BN", "Substitution group ID", GH_ParamAccess.tree);
            pManager[10].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("LeaderLine", "LL", "Generated leader lines", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Text", "T", "Generated text entities", GH_ParamAccess.tree);
            pManager.AddTextParameter("Instructions", "I", "Contract constraints", GH_ParamAccess.item);
        }

        private static readonly Dictionary<int, TextJustification> Justifications = new Dictionary<int, TextJustification>
        {
            {1, TextJustification.BottomLeft},
            {2, TextJustification.BottomCenter},
            {3, TextJustification.BottomRight},
            {4, TextJustification.MiddleLeft},
            {5, TextJustification.MiddleCenter},
            {6, TextJustification.MiddleRight},
            {7, TextJustification.TopLeft},
            {8, TextJustification.TopCenter},
            {9, TextJustification.TopRight}
        };

        private double GetItem(GH_Structure<GH_Number> tree, double defaultVal)
        {
            if (tree != null && tree.PathCount > 0 && tree.get_Branch(0).Count > 0)
            {
                var item = tree.get_Branch(0)[0] as GH_Number;
                if (item != null) return item.Value;
            }
            return defaultVal;
        }

        private int GetItem(GH_Structure<GH_Integer> tree, int defaultVal)
        {
            if (tree != null && tree.PathCount > 0 && tree.get_Branch(0).Count > 0)
            {
                var item = tree.get_Branch(0)[0] as GH_Integer;
                if (item != null) return item.Value;
            }
            return defaultVal;
        }

        private string GetItem(GH_Structure<GH_String> tree, string defaultVal)
        {
            if (tree != null && tree.PathCount > 0 && tree.get_Branch(0).Count > 0)
            {
                var item = tree.get_Branch(0)[0] as GH_String;
                if (item != null) return item.Value;
            }
            return defaultVal;
        }

        private bool GetItem(GH_Structure<GH_Boolean> tree, bool defaultVal)
        {
            if (tree != null && tree.PathCount > 0 && tree.get_Branch(0).Count > 0)
            {
                var item = tree.get_Branch(0)[0] as GH_Boolean;
                if (item != null) return item.Value;
            }
            return defaultVal;
        }

        private void SyncValueLists()
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;
            var docStyles = doc.DimStyles.Select(s => s.Name).ToList();

            var textPlaneDict = new Dictionary<string, string>
            {
                {"XY Plane (Top)", "0"},
                {"XZ Plane (Front)", "1"},
                {"YZ Plane (Right)", "2"}
            };
            var orientationDict = new Dictionary<string, string>
            {
                {"0°", "0"}, {"45°", "45"}, {"90°", "90"}, {"135°", "135"},
                {"180°", "180"}, {"225°", "225"}, {"270°", "270"}, {"315°", "315"}
            };
            var anchorDict = new Dictionary<string, string>
            {
                {"Top Left", "7"}, {"Top Center", "8"}, {"Top Right", "9"},
                {"Middle Left", "4"}, {"Middle Center", "5"}, {"Middle Right", "6"},
                {"Bottom Left", "1"}, {"Bottom Center", "2"}, {"Bottom Right", "3"}
            };

            bool needsRecompute = false;

            Action<string, Dictionary<string, string>> syncStatic = (paramName, dict) =>
            {
                var param = Params.Input.FirstOrDefault(p => p.Name == paramName);
                if (param == null) return;

                var targetKeys = dict.Keys.ToList();
                foreach (var source in param.Sources)
                {
                    if (source is Grasshopper.Kernel.Special.GH_ValueList valueList)
                    {
                        var currentKeys = valueList.ListItems.Select(item => item.Name).ToList();
                        if (!currentKeys.SequenceEqual(targetKeys))
                        {
                            valueList.ListItems.Clear();
                            foreach (var kvp in dict)
                            {
                                valueList.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(kvp.Key, kvp.Value));
                            }
                            valueList.ExpireSolution(false);
                            needsRecompute = true;
                        }
                    }
                }
            };

            Action syncDynamic = () =>
            {
                var param = Params.Input.FirstOrDefault(p => p.Name == "Style");
                if (param == null) return;

                foreach (var source in param.Sources)
                {
                    if (source is Grasshopper.Kernel.Special.GH_ValueList valueList)
                    {
                        var currentKeys = valueList.ListItems.Select(item => item.Name).ToList();
                        if (!currentKeys.SequenceEqual(docStyles))
                        {
                            valueList.ListItems.Clear();
                            foreach (var sName in docStyles)
                            {
                                valueList.ListItems.Add(new Grasshopper.Kernel.Special.GH_ValueListItem(sName, $"\"{sName}\""));
                            }
                            valueList.ExpireSolution(false);
                            needsRecompute = true;
                        }
                    }
                }
            };

            syncDynamic();
            syncStatic("TextPlane", textPlaneDict);
            syncStatic("Orientation", orientationDict);
            syncStatic("Anchor", anchorDict);

            if (needsRecompute)
            {
                var ghdoc = this.OnPingDocument();
                if (ghdoc != null)
                {
                    ghdoc.ScheduleSolution(5);
                }
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            SyncValueLists();

            var stopwatch = Stopwatch.StartNew();

            if (!DA.GetDataTree(0, out GH_Structure<GH_Point> pointsTree)) return;

            DA.GetDataTree(1, out GH_Structure<GH_String> labelTextTree);
            DA.GetDataTree(2, out GH_Structure<GH_Number> lengthTree);
            DA.GetDataTree(3, out GH_Structure<GH_Number> gapTree);
            DA.GetDataTree(4, out GH_Structure<GH_String> styleTree);
            DA.GetDataTree(5, out GH_Structure<GH_Integer> textPlaneTree);
            DA.GetDataTree(6, out GH_Structure<GH_Number> orientationTree);
            DA.GetDataTree(7, out GH_Structure<GH_Integer> anchorTree);
            DA.GetDataTree(8, out GH_Structure<GH_Boolean> bakeTree);
            DA.GetDataTree(9, out GH_Structure<GH_String> bakeLayerTree);
            DA.GetDataTree(10, out GH_Structure<GH_String> bakeNameTree);

            double lengthVal = GetItem(lengthTree, 10.0);
            double gapVal = GetItem(gapTree, 2.0);
            string styleVal = GetItem(styleTree, "Default");
            int planeVal = GetItem(textPlaneTree, 1);
            double orientVal = GetItem(orientationTree, 0.0);
            int anchorVal = GetItem(anchorTree, 2);
            bool bakeVal = GetItem(bakeTree, false);
            string layerVal = GetItem(bakeLayerTree, "Elevations");
            string bakeNameVal = GetItem(bakeNameTree, "");

            string instructions = @"INTERFACE CONTRACT:
Inputs:
- Points      : Point3d (DataTree) -> Coordinates to label
- LabelText   : String (DataTree) -> Optional text override
- Length      : Float (DataTree) -> Leader line length
- Gap         : Float (DataTree) -> Gap between line and text
- Style       : String (DataTree) -> Rhino Text Style name
- TextPlane   : Integer (DataTree) -> 0=XY, 1=XZ, 2=YZ 
- Orientation : Float (DataTree) -> Rotation in degrees 
- Anchor      : Integer (DataTree) -> 1-9 Justification 
- Bake        : Boolean (DataTree) -> Bake toggle/button
- BakeLayer   : String (DataTree) -> Target bake layer
- BakeName    : String (DataTree) -> Substitution group ID

Outputs:
- LeaderLine  : LineCurve (DataTree)
- Text        : TextEntity (DataTree)
- Instructions : String";

            var doc = RhinoDoc.ActiveDoc;
            int totalItems = 0;
            int bakeCount = 0;

            int layerIdx = -1;
            if (bakeVal && doc != null)
            {
                layerIdx = doc.Layers.FindByFullPath(layerVal, -1);
                if (layerIdx < 0)
                {
                    var newLayer = new Rhino.DocObjects.Layer { Name = layerVal };
                    layerIdx = doc.Layers.Add(newLayer);
                }

                if (!string.IsNullOrEmpty(bakeNameVal))
                {
                    var existingObjs = doc.Objects.FindByUserString("BakeName", bakeNameVal, true);
                    if (existingObjs != null)
                    {
                        foreach (var obj in existingObjs)
                        {
                            doc.Objects.Delete(obj, true);
                        }
                    }
                }
            }

            var dimStyle = doc?.DimStyles.FindName(styleVal);

            var leaderLineTree = new GH_Structure<GH_Curve>();
            var textTree = new GH_Structure<GH_ObjectWrapper>();

            for (int i = 0; i < pointsTree.PathCount; i++)
            {
                var path = pointsTree.get_Path(i);
                var branch = pointsTree.get_Branch(path);

                IList<GH_String> textBranch = null;
                if (labelTextTree != null && labelTextTree.PathCount > 0)
                {
                    if (i < labelTextTree.PathCount)
                    {
                        textBranch = (IList<GH_String>)labelTextTree.get_Branch(labelTextTree.get_Path(i));
                    }
                    else
                    {
                        textBranch = (IList<GH_String>)labelTextTree.get_Branch(labelTextTree.get_Path(labelTextTree.PathCount - 1));
                    }
                }

                leaderLineTree.EnsurePath(path);
                textTree.EnsurePath(path);

                if (branch.Count == 0) continue;

                for (int j = 0; j < branch.Count; j++)
                {
                    var ptGoo = branch[j] as GH_Point;
                    if (ptGoo == null) continue;
                    var pt = ptGoo.Value;

                    var p1 = pt;
                    var p2 = new Point3d(pt.X, pt.Y, pt.Z + lengthVal);
                    var lCrv = new LineCurve(new Line(p1, p2));

                    var p3 = new Point3d(pt.X, pt.Y, pt.Z + lengthVal + gapVal);
                    var te = new TextEntity();

                    Plane plane;
                    if (planeVal == 0) plane = Plane.WorldXY;
                    else if (planeVal == 2) plane = Plane.WorldYZ;
                    else plane = Plane.WorldZX;

                    plane.Origin = p3;

                    if (orientVal != 0.0)
                    {
                        plane.Rotate(orientVal * Math.PI / 180.0, plane.ZAxis, plane.Origin);
                    }

                    te.Plane = plane;

                    string customText = null;
                    if (textBranch != null && j < textBranch.Count)
                    {
                        customText = textBranch[j]?.Value;
                    }
                    else if (textBranch != null && textBranch.Count > 0)
                    {
                        customText = textBranch[textBranch.Count - 1]?.Value;
                    }

                    if (!string.IsNullOrEmpty(customText))
                    {
                        te.PlainText = customText;
                    }
                    else
                    {
                        te.PlainText = pt.Z.ToString("0.00");
                    }

                    te.Justification = Justifications.TryGetValue(anchorVal, out var just) ? just : TextJustification.BottomCenter;

                    if (dimStyle != null)
                    {
                        te.DimensionStyleId = dimStyle.Id;
                    }
                    else
                    {
                        te.TextHeight = 2.0;
                    }

                    leaderLineTree.Append(new GH_Curve(lCrv), path);
                    textTree.Append(new GH_ObjectWrapper(te), path);
                    totalItems++;

                    if (bakeVal && layerIdx >= 0 && doc != null)
                    {
                        var attr = new ObjectAttributes { LayerIndex = layerIdx };
                        if (!string.IsNullOrEmpty(bakeNameVal))
                        {
                            attr.SetUserString("BakeName", bakeNameVal);
                        }
                        doc.Objects.AddCurve(lCrv, attr);
                        doc.Objects.AddText(te, attr);
                        bakeCount += 2;
                    }
                }
            }

            DA.SetDataTree(0, leaderLineTree);
            DA.SetDataTree(1, textTree);
            DA.SetData(2, instructions);

            stopwatch.Stop();
            double elapsed = stopwatch.Elapsed.TotalMilliseconds;
            Message = $"ELEV_LABEL\nTime: {elapsed:0.00} ms\n---\nBranches: {pointsTree.PathCount}\nTotal Items: {totalItems}\n● Baked Geo: {bakeCount}";
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("ELEV_LABEL.png");
        public override Guid ComponentGuid => new Guid("e1b5f210-9c24-4f81-a67b-1132a2c53db1");
    }
}
