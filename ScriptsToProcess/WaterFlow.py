"""
[INPUTS]
TerrainMesh  : Mesh (Item Access) - The unified topological surface.
GridSpacing  : float (Item Access) - The XY distance between starting raindrops.
StepSize     : float (Item Access) - Distance the water travels per tick.
MaxSteps     : int (Item Access) - Safety limit for the simulation loop.

[OUTPUTS]
FlowPaths        : PolylineCurve (Tree Access) - The generated water movement paths.
DropPoints       : Point3d (Tree Access) - The valid starting grid points on the mesh.
Instructions_Out : string (Item Access) - Node configuration guide.
"""

import time
import Rhino.Geometry as rg
from Grasshopper import DataTree
import Grasshopper.Kernel.Data as gh_data
import System

# --- [ASSISTANT-GENERATED COMPONENT METADATA] ---
ghenv.Component.Name = "Auto-Grid Raindrop Flow Engine"
ghenv.Component.NickName = "WaterFlow"
ghenv.Component.Description = "Generates a parametric grid, projects it to the terrain, and simulates downhill flow paths."

Instructions_Out = __doc__

# --- [EXECUTION] ---
start_time = time.perf_counter()

FlowPaths = DataTree[object]()
DropPoints = DataTree[object]()
path_count = 0
stalled_count = 0
grid_points_generated = 0

if TerrainMesh and GridSpacing and StepSize and MaxSteps:
    TerrainMesh.FaceNormals.ComputeFaceNormals()
    
    # 1. Bounding Box & Grid Generation
    bbox = TerrainMesh.GetBoundingBox(True)
    min_x, max_x = bbox.Min.X, bbox.Max.X
    min_y, max_y = bbox.Min.Y, bbox.Max.Y
    max_z = bbox.Max.Z + 1.0 # Start slightly above the highest peak
    
    start_grid_points = []
    current_x = min_x
    while current_x <= max_x:
        current_y = min_y
        while current_y <= max_y:
            start_grid_points.append(rg.Point3d(current_x, current_y, max_z))
            current_y += GridSpacing
        current_x += GridSpacing
        
    grid_points_generated = len(start_grid_points)
    
    # 2. Optimized Vertical Projection onto Terrain
    gravity = rg.Vector3d(0, 0, -1)
    projected_pts = rg.Intersect.Intersection.ProjectPointsToMeshes([TerrainMesh], start_grid_points, gravity, 0.001)
    
    z_tolerance = 0.001
    
    # 3. Physics Simulation Loop
    if projected_pts:
        for i, pt in enumerate(projected_pts):
            path_index = gh_data.GH_Path(i)
            FlowPaths.EnsurePath(path_index)
            DropPoints.EnsurePath(path_index)
            
            DropPoints.Add(pt, path_index)
            
            polyline_vertices = []
            
            # Find exact starting face/normal
            mesh_pt = TerrainMesh.ClosestMeshPoint(pt, 0.0)
            if not mesh_pt:
                stalled_count += 1
                continue
                
            current_location = mesh_pt.Point
            polyline_vertices.append(current_location)
            
            # Physics tick loop
            for step in range(MaxSteps):
                face_normal = TerrainMesh.FaceNormals[mesh_pt.FaceIndex]
                
                # Filter out perfectly flat faces
                if abs(face_normal.Z) >= 0.9999:
                    break
                    
                # Gravity vector mapped to sloped plane
                strike = rg.Vector3d.CrossProduct(face_normal, gravity)
                downhill = rg.Vector3d.CrossProduct(strike, face_normal)
                
                if not downhill.Unitize():
                    break
                    
                next_location = current_location + (downhill * StepSize)
                next_mesh_pt = TerrainMesh.ClosestMeshPoint(next_location, 0.0)
                
                if not next_mesh_pt:
                    break # Ran off mesh edge
                    
                projected_point = next_mesh_pt.Point
                
                # Sinkhole check
                if projected_point.Z >= current_location.Z - z_tolerance:
                    break
                    
                polyline_vertices.append(projected_point)
                current_location = projected_point
                mesh_pt = next_mesh_pt
                
            if len(polyline_vertices) > 1:
                FlowPaths.Add(rg.PolylineCurve(polyline_vertices), path_index)
                path_count += 1
            else:
                stalled_count += 1

# --- [TELEMETRY & HUD] ---
end_time = time.perf_counter()
duration_ms = (end_time - start_time) * 1000.0

ghenv.Component.Message = (
    f"{ghenv.Component.NickName}\n"
    f"Time: {duration_ms:.1f} ms\n"
    f"---\n"
    f"Grid Seeds: {grid_points_generated}\n"
    f"Total Paths: {path_count}\n"
    f"● Active: {path_count} | ○ Stalled: {stalled_count}"
)