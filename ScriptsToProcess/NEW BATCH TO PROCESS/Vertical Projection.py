import Rhino.Geometry as rg

def project_point_vertically(pt, plane):
    """
    Projects a point vertically (along world Z-axis) onto a given plane.
    
    Args:
        pt (rg.Point3d): The input point.
        plane (rg.Plane): The target plane.

    Returns:
        tuple: (bool, projected_point, message)
    """
    # Compute the signed distance from the point to the plane
    dist = plane.DistanceTo(pt)

    # Create a vertical line from the point (World Z-axis)
    vertical_dir = rg.Vector3d(0, 0, 1)  # Global Z-axis
    vertical_line = rg.Line(pt - vertical_dir * 10000, pt + vertical_dir * 10000)  # Long enough line

    # Find intersection of the vertical line with the plane
    success, parameter = rg.Intersect.Intersection.LinePlane(vertical_line, plane)

    if success:
        projected_pt = vertical_line.PointAt(parameter)  # Get the actual intersection point

        # Determine if the point is on, above, or below the plane
        if abs(dist) < 1e-6:
            message = "Point is on the plane\nNo projection needed"
        elif dist > 0:
            message = "Point was above the plane\nProjected downward"
        else:
            message = "Point was below the plane\nProjected upward"

        return abs(dist) < 1e-6, projected_pt, message

    return False, pt, "No valid projection\nCheck input values"  # Fallback case

# Grasshopper Inputs:
# pt: Input point (Type Hint: Point3d)
# plane: Input plane (Type Hint: Plane)

is_on_plane, projected_pt, msg = project_point_vertically(pt, plane)

# Grasshopper Outputs:
isContained = is_on_plane  # Boolean if the point is on the plane
ProjectedPoint = projected_pt  # The actual projected Point3d
Message = msg  # Multi-line message output

# Set Grasshopper component metadata
if 'ghenv' in globals():
    ghenv.Component.Name = "Vertical Projection"
    ghenv.Component.NickName = "VertProj"
    ghenv.Component.Message = msg