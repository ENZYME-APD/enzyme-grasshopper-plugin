# Component Metadata
ghenv.Component.Name = "Masterplan Clearance Engine"
ghenv.Component.NickName = "Clearance"
ghenv.Component.Description = "Calculates topological (Delaunay) or proximity-based clearances between tower outlines with dynamic categorization."

import clr
clr.AddReference("Grasshopper")

import Rhino.Geometry as rg
import Grasshopper as gh
from Grasshopper.Kernel.Geometry.Delaunay import Solver as DelaunaySolver
from Grasshopper.Kernel.Geometry import Node2, Node2List
from Grasshopper import DataTree
from Grasshopper.Kernel.Data import GH_Path
import time
import math

def solve_hybrid_proximity(curves, search_radius, mode, limit1, limit2):
    start_time = time.time()
    
    # Standardized Outputs
    line_tree = DataTree[rg.Line]()
    dist_tree = DataTree[float]()
    text_tree = DataTree[str]()
    plane_tree = DataTree[rg.Plane]() 
    cat_tree = DataTree[int]()

    # 1. Centroid Collection
    nodes_raw = []
    valid_indices = []
    centroids_3d = [] 
    for i, crv in enumerate(curves):
        if crv is None: 
            centroids_3d.append(rg.Point3d.Unset)
            continue
        bbox = crv.GetBoundingBox(True)
        if bbox.IsValid:
            center = bbox.Center
            nodes_raw.append(Node2(center.X, center.Y))
            centroids_3d.append(center)
            valid_indices.append(i)
        else:
            centroids_3d.append(rg.Point3d.Unset)

    # --- Helper: Tree Management ---
    def add_data_to_trees(idx_a, pA, pB, distance):
        path = GH_Path(idx_a)
        ln = rg.Line(pA, pB)
        line_tree.Add(ln, path)
        dist_tree.Add(distance, path)
        text_tree.Add("{:.0f}m".format(distance), path)
        
        # --- DYNAMIC CATEGORIES ---
        if distance < limit1: cat_tree.Add(0, path)
        elif distance <= limit2: cat_tree.Add(1, path)
        else: cat_tree.Add(2, path)
        
        # Plane Alignment for Text Tags
        mid = ln.PointAt(0.5)
        vec = ln.Direction
        if vec.Length > 0.001:
            angle = math.atan2(vec.Y, vec.X)
            if angle > math.pi/2 or angle < -math.pi/2: angle += math.pi
            pln = rg.Plane.WorldXY
            pln.Origin = mid
            pln.Rotate(angle, rg.Vector3d.ZAxis, mid)
            plane_tree.Add(pln, path)

    # --- METHOD 0: DELAUNAY ---
    if mode == 0:
        method_name = "Delaunay Method"
        if len(nodes_raw) >= 3:
            nodes_list = Node2List(nodes_raw)
            res = DelaunaySolver.Solve_Mesh(nodes_list, 0.1, None)
            delaunay_mesh = res[0] 
            
            if delaunay_mesh:
                top_edges = delaunay_mesh.TopologyEdges
                for i in range(top_edges.Count):
                    pair = top_edges.GetTopologyVertices(i)
                    idx_a_valid, idx_b_valid = pair.I, pair.J
                    idx_a, idx_b = valid_indices[idx_a_valid], valid_indices[idx_b_valid]
                    
                    res_cp, cpA, cpB = rg.Curve.ClosestPoints(curves[idx_a], curves[idx_b])
                    if res_cp:
                        d = cpA.DistanceTo(cpB)
                        if d <= search_radius:
                            add_data_to_trees(idx_a, cpA, cpB, d)

    # --- METHOD 1: RADIUS (PROXIMITY) ---
    else:
        method_name = "Proximity Method"
        rtree = rg.RTree()
        for i, pt in enumerate(centroids_3d):
            if pt != rg.Point3d.Unset: rtree.Insert(pt, i)

        for i in range(len(curves)):
            if centroids_3d[i] == rg.Point3d.Unset: continue
            potential_ids = []
            def rtree_callback(sender, e):
                if e.Id > i: potential_ids.append(e.Id)

            bbox = curves[i].GetBoundingBox(True)
            bbox.Inflate(search_radius)
            rtree.Search(bbox, rtree_callback)

            for j in potential_ids:
                mid = (centroids_3d[i] + centroids_3d[j]) * 0.5
                limit = centroids_3d[i].DistanceTo(centroids_3d[j]) * 0.5
                
                is_obscured = False
                for k in range(len(curves)):
                    if k == i or k == j or centroids_3d[k] == rg.Point3d.Unset: continue
                    if centroids_3d[k].DistanceTo(mid) < (limit * 0.85):
                        is_obscured = True
                        break
                
                if not is_obscured:
                    res_cp, cpA, cpB = rg.Curve.ClosestPoints(curves[i], curves[j])
                    if res_cp:
                        d = cpA.DistanceTo(cpB)
                        if d <= search_radius:
                            add_data_to_trees(i, cpA, cpB, d)

    # UI Feedback: Method / Legend / Time
    ms = (time.time() - start_time) * 1000
    dynamic_legend = "0-{:.0f} / {:.0f}-{:.0f} / >{:.0f}".format(limit1, limit1, limit2, limit2)
    ghenv.Component.Message = "{}\n{}\n{:.1f}ms".format(method_name, dynamic_legend, ms)
    
    return dist_tree, line_tree, cat_tree, plane_tree

# --- EXECUTION ---
if 'Outlines' in globals() and Outlines:
    r_val = float(SearchRadius) if ('SearchRadius' in globals() and SearchRadius is not None) else 200.0
    m_val = int(Method) if ('Method' in globals() and Method is not None) else 0
    val_l1 = float(L1) if ('L1' in globals() and L1 is not None) else 50.0
    val_l2 = float(L2) if ('L2' in globals() and L2 is not None) else 100.0
    
    Distances, Lines, Categories, LabelPlanes = solve_hybrid_proximity(Outlines, r_val, m_val, val_l1, val_l2)