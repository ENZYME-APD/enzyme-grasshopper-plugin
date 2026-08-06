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
  private void RunScript(DataTree<Curve> curves, DataTree<double> modSize, DataTree<double> tDepth, DataTree<double> tHeight, ref object transoms, ref object divLines)
  {
    // Initialize output trees
DataTree<Brep> transomsTree = new DataTree<Brep>();
DataTree<Line> divisionLinesTree = new DataTree<Line>();

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
    foreach (var curve in curves.Branch(path))
    {
        if (curve == null || !curve.IsClosed || !curve.IsPlanar()) continue;

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
    uniformDepth = GetDoubleValue(tDepth.AllData()[0], defaultDepth);

double uniformHeight = defaultHeight;
bool useUniformHeight = tHeight.DataCount == 1;
if (useUniformHeight && tHeight.DataCount > 0)
    uniformHeight = GetDoubleValue(tHeight.AllData()[0], defaultHeight);

double uniformModSize = defaultModSize;
bool useUniformModSize = modSize.DataCount == 1;
if (useUniformModSize && modSize.DataCount > 0)
    uniformModSize = GetDoubleValue(modSize.AllData()[0], defaultModSize);

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
        var branch = tDepth.Branch(path);
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
        var branch = tHeight.Branch(path);
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
        var branch = modSize.Branch(path);
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
        divisionLinesTree.Add(new Line(pt, endPt), branchPath);

        Brep transom = Brep.CreateFromCornerPoints(
            pt,
            endPt,
            endPt + extrusionDir * height,
            pt + extrusionDir * height,
            0.001
        );

        if (transom != null)
        {
            transomsTree.Add(transom, branchPath);
        }
    }
}

// Output assignment
transoms = transomsTree;
divLines = divisionLinesTree;

// Helper
double GetDoubleValue(object obj, double fallback)
{
    if (obj == null) return fallback;
    if (obj is double d) return d;
    if (obj is int i) return i;
    if (obj is GH_Number n) return n.Value;
    if (obj is string s && double.TryParse(s, out double val)) return val;
    return fallback;
}
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
        DataTree<Curve> curves = null;
    if (inputs[0] != null)
    {
      curves = GH_DirtyCaster.CastToTree<Curve>(inputs[0]);
    }

    DataTree<double> modSize = null;
    if (inputs[1] != null)
    {
      modSize = GH_DirtyCaster.CastToTree<double>(inputs[1]);
    }

    DataTree<double> tDepth = null;
    if (inputs[2] != null)
    {
      tDepth = GH_DirtyCaster.CastToTree<double>(inputs[2]);
    }

    DataTree<double> tHeight = null;
    if (inputs[3] != null)
    {
      tHeight = GH_DirtyCaster.CastToTree<double>(inputs[3]);
    }



    //3. Declare output parameters
      object transoms = null;
  object divLines = null;


    //4. Invoke RunScript
    RunScript(curves, modSize, tDepth, tHeight, ref transoms, ref divLines);
      
    try
    {
      //5. Assign output parameters to component...
            if (transoms != null)
      {
        if (GH_Format.TreatAsCollection(transoms))
        {
          IEnumerable __enum_transoms = (IEnumerable)(transoms);
          DA.SetDataList(1, __enum_transoms);
        }
        else
        {
          if (transoms is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(transoms));
          }
          else
          {
            //assign direct
            DA.SetData(1, transoms);
          }
        }
      }
      else
      {
        DA.SetData(1, null);
      }
      if (divLines != null)
      {
        if (GH_Format.TreatAsCollection(divLines))
        {
          IEnumerable __enum_divLines = (IEnumerable)(divLines);
          DA.SetDataList(2, __enum_divLines);
        }
        else
        {
          if (divLines is Grasshopper.Kernel.Data.IGH_DataTree)
          {
            //merge tree
            DA.SetDataTree(2, (Grasshopper.Kernel.Data.IGH_DataTree)(divLines));
          }
          else
          {
            //assign direct
            DA.SetData(2, divLines);
          }
        }
      }
      else
      {
        DA.SetData(2, null);
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
