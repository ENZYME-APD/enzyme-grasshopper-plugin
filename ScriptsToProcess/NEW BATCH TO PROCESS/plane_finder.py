import Rhino.Geometry as rg

# Manually set the component's name, nickname, and version
component_name = "Surface Plane Finder"
component_nickname = "plane_finder"
component_version = "v1.0"

# Check if the input `x` is a list
if isinstance(x, list):
    if len(x) > 0:
        input_item = x[0]  # Get the first item in the list
    else:
        input_item = None  # The list is empty
else:
    input_item = x  # Input is not a list

# Initialize `surface` as None
surface = None

# Check the type of the input and process accordingly
if input_item is not None:
    if isinstance(input_item, rg.Surface):
        # Input is a Surface
        surface = input_item
    elif isinstance(input_item, rg.Brep):
        # Input is a Brep, try to extract the first face
        if input_item.Faces.Count == 1:
            surface = input_item.Faces[0].UnderlyingSurface()  # Convert to Surface
        else:
            ghenv.Component.Message = f"{component_name} {component_version}\nInput is a polysurface."
    else:
        ghenv.Component.Message = f"{component_name} {component_version}\nInvalid input type."

# Proceed if a valid surface is extracted
if surface is not None:
    if surface.IsPlanar():
        # Get midpoints of the domain
        u = surface.Domain(0).Mid
        v = surface.Domain(1).Mid

        # Compute the normal
        normal = surface.NormalAt(u, v)
        normal.Unitize()

        # Reverse the normal
        reverse_normal = -normal

        # Compute the centroid
        if isinstance(surface, rg.PlaneSurface):
            # For PlaneSurface, use the bounding box to calculate the centroid
            bbox = surface.GetBoundingBox(True)
            centroid = bbox.Center
        else:
            # For other surfaces, use AreaCentroid
            success, centroid = surface.AreaCentroid()
            if not success:
                centroid = surface.PointAt(u, v)

        # Create a plane at the centroid with the normal
        plane = rg.Plane(centroid, normal)

        # Output
        plane_normal = normal
        reverse_normal = reverse_normal
        output_plane = plane
        ghenv.Component.Message = f"{component_name} {component_version}\nSurface is planar."
    else:
        plane_normal = None
        reverse_normal = None
        output_plane = None
        ghenv.Component.Message = f"{component_name} {component_version}\nSurface is NOT planar."
else:
    plane_normal = None
    reverse_normal = None
    output_plane = None
    ghenv.Component.Message = f"{component_name} {component_version}\nNo valid surface provided."

# Optional: Print debugging information
print(f"Component Name: {component_name}")
print(f"Component Nickname: {component_nickname}")
print(f"Component Version: {component_version}")
print(f"Input type: {type(input_item)}")

ghenv.Component.Name = component_name
ghenv.Component.NickName = component_nickname