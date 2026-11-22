using Enzyme.Utils;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Enzyme; // for IconLoader

namespace Enzyme.Components
{
    public class GroupByKey : GH_Component
    {
        public GroupByKey()
            : base("GroupByKey", "GrpByKey",
                "Groups a list of values by a corresponding list of keys",
                "Enzyme", "Data")
        {
            Trace.WriteLine("GroupByKey constructor called");
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Keys", "Keys", "Keys to group by", GH_ParamAccess.list);
            pManager.AddGenericParameter("List", "List", "List of values to be grouped (must be same length as Keys)", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Unique Keys", "UniqueKeys", "List of unique keys", GH_ParamAccess.list);
            pManager.AddGenericParameter("Grouped Values", "GroupedValues", "Data tree with values grouped by keys", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            List<object> keys = new List<object>();
            List<object> values = new List<object>();

            DA.GetDataList(0, keys);
            DA.GetDataList(1, values);

            GH_Structure<IGH_Goo> groupedTree = new GH_Structure<IGH_Goo>();

            if (keys.Count != values.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Keys and Values must be of the same length.");
                return;
            }

            Dictionary<object, int> keyToIndex = new Dictionary<object, int>();
            int branchIndex = 0;

            // With a data tree on the inputs the component runs once per branch: prefix the group index with
            // the input branch path so groups of different branches do not merge. A single-branch input keeps
            // the flat {0}, {1}, {2}... paths.
            bool multiBranch = Params.Input[0].VolatileData.PathCount > 1 || Params.Input[1].VolatileData.PathCount > 1;
            GH_Path basePath = multiBranch ? DA.ParameterTargetPath(1) : null;

            for (int i = 0; i < keys.Count; i++)
            {
                object rawKey = keys[i];

                // Unwraps Grasshopper wrapper types (IGH_Goo) into their underlying .NET types for consistent key comparison.
                // Example:
                //   - GH_String("A")    → "A"   (string)
                //   - GH_Integer(1)     → 1     (int)
                //   - GH_Number(1.5)    → 1.5   (double)
                //   - GH_Boolean(true)  → true  (bool)
                // This ensures that different GH_ types with the same value group correctly in the dictionary.
                object unwrappedKey = rawKey is IGH_Goo gooKey ? gooKey.ScriptVariable() : rawKey;
                if (unwrappedKey == null) unwrappedKey = NullKey; // Dictionary keys cannot be null

                object value = values[i];

                if (!keyToIndex.ContainsKey(unwrappedKey))
                {
                    keyToIndex[unwrappedKey] = branchIndex;
                    branchIndex++;
                }

                int pathIndex = keyToIndex[unwrappedKey];
                GH_Path path = basePath != null ? basePath.AppendElement(pathIndex) : new GH_Path(pathIndex);

                IGH_Goo gooValue = value as IGH_Goo ?? new GH_ObjectWrapper(value);
                groupedTree.Append(gooValue, path);
            }

            // Extract unique keys
            List<object> uniqueKeys = new List<object>(keyToIndex.Keys);
            for (int k = 0; k < uniqueKeys.Count; k++)
                if (ReferenceEquals(uniqueKeys[k], NullKey)) uniqueKeys[k] = null;

            // Sets outputs
            DA.SetDataList(0, uniqueKeys);
            DA.SetDataTree(1, groupedTree);

            // Creates compact 2-line status message
            int itemCount = values.Count;
            int branchCount = keyToIndex.Count;

            stopwatch.Stop();
            Hud.Done(this, stopwatch.Elapsed.TotalMilliseconds, $"{itemCount} values\n{branchCount} groups");
        }

        private static readonly object NullKey = new object();

        protected override Bitmap Icon
        {
            get
            {
                Bitmap icon = IconLoader.Load("gk_icon.png");
                return icon;
            }
        }

        public override Guid ComponentGuid => new Guid("7aea2b14-9eef-49af-a173-f2223b2c70bc");

        public override GH_Exposure Exposure => GH_Exposure.primary;
    }
}
