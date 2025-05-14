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
                "Enzyme", "Utilities")
        {
            Trace.WriteLine("GroupByKey constructor called");
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Keys", "K", "Keys to group by", GH_ParamAccess.list);
            pManager.AddGenericParameter("List", "L", "List of values to be grouped (must be same length as Keys)", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("UniqueKeys", "UK", "List of unique keys", GH_ParamAccess.list);
            pManager.AddGenericParameter("Grouped", "G", "Data tree with values grouped by keys", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
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

                object value = values[i];

                if (!keyToIndex.ContainsKey(unwrappedKey))
                {
                    keyToIndex[unwrappedKey] = branchIndex;
                    branchIndex++;
                }

                int pathIndex = keyToIndex[unwrappedKey];
                GH_Path path = new GH_Path(pathIndex);

                IGH_Goo gooValue = value as IGH_Goo ?? new GH_ObjectWrapper(value);
                groupedTree.Append(gooValue, path);
            }

            // Extract unique keys
            List<object> uniqueKeys = new List<object>(keyToIndex.Keys);

            // Sets outputs
            DA.SetDataList(0, uniqueKeys);
            DA.SetDataTree(1, groupedTree);

            // Creates compact 2-line status message
            int itemCount = values.Count;
            int branchCount = keyToIndex.Count;

            // Get version from assembly
            Version version = GetType().Assembly.GetName().Version;
            string versionString = $"{version.Major}.{version.Minor}.{version.Build}";

            Message = $"GrpKey v{versionString}\n{itemCount} values → {branchCount} groups";
        }

        protected override Bitmap Icon
        {
            get
            {
                Bitmap icon = IconLoader.Load("gk_icon.png");
                if (icon == null)
                {
                    this.Message = "Icon missing";
                }
                return icon;
            }
        }

        public override Guid ComponentGuid => new Guid("7aea2b14-9eef-49af-a173-f2223b2c70bc");
    }
}
