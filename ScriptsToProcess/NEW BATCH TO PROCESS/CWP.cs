using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;

public class Script_Instance : GH_ScriptInstance
{
    private void RunScript(
		double A,
		double B,
		double dA,
		double dB,
		double R,
		ref object Polyline)
    {
        // Name and version for display
        string version = "v1.0";
        Component.Name = "CWProfile";
        Component.NickName = "CWP";

        // Set default values if inputs are not provided
        if (!Component.Params.Input[0].VolatileData.AllData(true).Cast<object>().Any())
        {
            A = 0.3; // Default value for A
        }
        if (!Component.Params.Input[1].VolatileData.AllData(true).Cast<object>().Any())
        {
            B = 0.5; // Default value for B
        }
        if (!Component.Params.Input[2].VolatileData.AllData(true).Cast<object>().Any())
        {
            dA = 0.0; // Default value for dA
        }
        if (!Component.Params.Input[3].VolatileData.AllData(true).Cast<object>().Any())
        {
            dB = 0.0; // Default value for dB
        }
        if (!Component.Params.Input[4].VolatileData.AllData(true).Cast<object>().Any())
        {
            R = 0.0; // Default value for R
        }

        // Set Component.Message with version, dimensions, and fillet info
        string filletMessage = R == 0 ? "No fillet" : $"R={R}";
        Component.Message = $"CWProfile {version}\n{A}x{B}\n{filletMessage}";

        // Validate inputs
        if (A <= 0 || B <= 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Dimensions A and B must be positive.");
            Polyline = null;
            return;
        }
        if (R < 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Fillet radius R cannot be negative.");
            Polyline = null;
            return;
        }

        // Create rectangle centered at origin
        double halfA = A / 2.0;
        double halfB = B / 2.0;
        Point3d[] corners = new Point3d[]
        {
            new Point3d(-halfA, -halfB, 0),
            new Point3d(halfA, -halfB, 0),
            new Point3d(halfA, halfB, 0),
            new Point3d(-halfA, halfB, 0),
            new Point3d(-halfA, -halfB, 0) // Close the polyline
        };

        // Create initial polyline
        Polyline polyline = new Polyline(corners);

        // Apply fillet if R > 0
        if (R > 0)
        {
            // Check if fillet radius is too large
            if (R > Math.Min(A, B) / 2.0)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Fillet radius R is too large for the rectangle dimensions. Using sharp corners.");
                R = 0;
            }
            else
            {
                // Convert polyline to curve for filleting
                Curve polyCurve = polyline.ToPolylineCurve();
                if (polyCurve != null && polyCurve.IsValid && polyCurve.IsClosed && polyCurve.IsPlanar())
                {
                    // Fillet corners
                    Curve filletedCurve = Curve.CreateFilletCornersCurve(
                        polyCurve,
                        R,
                        RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                        RhinoDoc.ActiveDoc.ModelAngleToleranceRadians);

                    if (filletedCurve != null && filletedCurve.IsValid && filletedCurve.IsClosed)
                    {
                        // Convert the filleted curve back to a polyline with better control
                        Polyline tempPoly;
                        bool converted = filletedCurve.TryGetPolyline(out tempPoly);
                        if (!converted || tempPoly == null)
                        {
                            // If TryGetPolyline fails, approximate the curve as a polyline
                            PolylineCurve polylineCurve = filletedCurve.ToPolyline(
                                RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, // Distance tolerance
                                RhinoDoc.ActiveDoc.ModelAngleToleranceRadians, // Angle tolerance
                                0.01, // Minimum segment length
                                0.0 // Maximum segment length (0 means no limit)
                            );
                            if (polylineCurve != null && polylineCurve.TryGetPolyline(out tempPoly))
                            {
                                polyline = tempPoly;
                            }
                            else
                            {
                                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to convert filleted curve to polyline. Using sharp corners.");
                            }
                        }
                        else
                        {
                            polyline = tempPoly;
                        }
                    }
                    else
                    {
                        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Filleting failed. Using sharp corners.");
                    }
                }
                else
                {
                    Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Invalid or non-planar curve. Using sharp corners.");
                }
            }
        }

        // Apply offset (dA, dB)
        Vector3d offset = new Vector3d(dA, dB, 0);
        polyline.Transform(Transform.Translation(offset));

        // Set output
        Polyline = polyline;
    }
}