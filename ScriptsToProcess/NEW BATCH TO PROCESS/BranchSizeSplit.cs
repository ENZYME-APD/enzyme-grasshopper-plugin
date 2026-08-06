// this is an extra line that isn't in the CS_Edit_Template and was added because line numbers in compiler messages are zero based and editor line numbers are 1 based. This extra line makes error line numbers correspond to editor line numbers.
#region using
using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

#region class comment
/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
#endregion
public class Script_Instance : GH_ScriptInstance
{
#region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { __out.Add(text); }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { __out.Add(string.Format(format, args)); }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj)); }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj, method_name)); }
#endregion

#region Members
  /// <summary>Gets the current Rhino document.</summary>
  private RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private IGH_Component Component; 
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private int Iteration;
#endregion

#region method comment
  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments, 
  /// Output parameters as ref arguments. You don't have to assign output parameters, 
  /// they will have a default value.
  /// </summary>
#endregion
  private void RunScript(DataTree<System.Object> T, double minItems, double maxItems, ref object A, ref object B, ref object C)
  {
    // Clear output trees
DataTree<object> treeA = new DataTree<object>();
DataTree<object> treeB = new DataTree<object>();
DataTree<object> treeC = new DataTree<object>();

// Get threshold values for branch sizes
int minThreshold = (int)minItems; // Branches with fewer items go to tree A
int maxThreshold = (int)maxItems; // Branches with more items go to tree C

// Validate thresholds
if (minThreshold < 0 || maxThreshold < minThreshold)
{
  Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "minItems must be ≥ 0 and maxItems must be ≥ minItems");
  return;
}

// Initialize counters
int countA = 0;
int countB = 0;
int countC = 0;
int totalBranches = T.BranchCount;

// Process each branch based on its item count
for (int i = 0; i < totalBranches; i++)
{
  GH_Path originalPath = T.Paths[i];
  List<object> items = new List<object>(T.Branch(i));
  int itemCount = items.Count;
  
  // Distribute to appropriate tree based on item count
  if (itemCount < minThreshold)
  {
    treeA.AddRange(items, originalPath);
    countA++;
  }
  else if (itemCount <= maxThreshold)
  {
    treeB.AddRange(items, originalPath);
    countB++;
  }
  else // itemCount > maxThreshold
  {
    treeC.AddRange(items, originalPath);
    countC++;
  }
}

// Assign output trees
A = treeA;
B = treeB;
C = treeC;

// Display component message
Component.Message = "BranchSizeSplit\n" + 
                    countA + " branches in A (<" + minThreshold + " items)\n" + 
                    countB + " branches in B (" + minThreshold + "-" + maxThreshold + " items)\n" +
                    countC + " branches in C (>" + maxThreshold + " items)";
  }

  // <Custom additional code> 
  
  // </Custom additional code> 

  private List<string> __err = new List<string>(); //Do not modify this list directly.
  private List<string> __out = new List<string>(); //Do not modify this list directly.
  private RhinoDoc doc = Instances.ActiveRhinoDoc;       //Legacy field.
  private IGH_ActiveObject owner;                  //Legacy field.
  private int runCount;                            //Legacy field.
  
  public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA)
  {
    //Prepare for a new run...
    //1. Reset lists
    this.__out.Clear();
    this.__err.Clear();

    this.Component = owner;
    this.Iteration = iteration;
    this.GrasshopperDocument = owner.OnPingDocument();
    this.RhinoDocument = rhinoDocument as Rhino.RhinoDoc;

    this.owner = this.Component;
    this.runCount = this.Iteration;
    this. doc = this.RhinoDocument;

    //2. Assign input parameters
        DataTree<System.Object> T = null;
    if (inputs[0] != null)
    {
      T = GH_DirtyCaster.CastToTree<System.Object>(inputs[0]);
    }

    double minItems = default(double);
    if (inputs[1] != null)
    {
      minItems = (double)(inputs[1]);
    }

    double maxItems = default(double);
    if (inputs[2] != null)
    {
      maxItems = (double)(inputs[2]);
    }



    //3. Declare output parameters
      object A = null;
  object B = null;
  object C = null;


    //4. Invoke RunScript
    RunScript(T, minItems, maxItems, ref A, ref B, ref C);
      
    try
    {
      //5. Assign output parameters to component...
            if (A != null)
      {
        if (GH_Format.TreatAsCollection(A))
        {
          IEnumerable __enum_A = (IEnumerable)(A);
          DA.SetDataList(1, __enum_A);
        }
        else
        {
          if (A is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(A));
          }
          else
          {
            //assign direct
            DA.SetData(1, A);
          }
        }
      }
      else
      {
        DA.SetData(1, null);
      }
      if (B != null)
      {
        if (GH_Format.TreatAsCollection(B))
        {
          IEnumerable __enum_B = (IEnumerable)(B);
          DA.SetDataList(2, __enum_B);
        }
        else
        {
          if (B is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(2, (Grasshopper.Kernel.Data.IGH_DataTree)(B));
          }
          else
          {
            //assign direct
            DA.SetData(2, B);
          }
        }
      }
      else
      {
        DA.SetData(2, null);
      }
      if (C != null)
      {
        if (GH_Format.TreatAsCollection(C))
        {
          IEnumerable __enum_C = (IEnumerable)(C);
          DA.SetDataList(3, __enum_C);
        }
        else
        {
          if (C is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(3, (Grasshopper.Kernel.Data.IGH_DataTree)(C));
          }
          else
          {
            //assign direct
            DA.SetData(3, C);
          }
        }
      }
      else
      {
        DA.SetData(3, null);
      }

    }
    catch (Exception ex)
    {
      this.__err.Add(string.Format("Script exception: {0}", ex.Message));
    }
    finally
    {
      //Add errors and messages... 
      if (owner.Params.Output.Count > 0)
      {
        if (owner.Params.Output[0] is Grasshopper.Kernel.Parameters.Param_String)
        {
          List<string> __errors_plus_messages = new List<string>();
          if (this.__err != null) { __errors_plus_messages.AddRange(this.__err); }
          if (this.__out != null) { __errors_plus_messages.AddRange(this.__out); }
          if (__errors_plus_messages.Count > 0) 
            DA.SetDataList(0, __errors_plus_messages);
        }
      }
    }
  }
}
