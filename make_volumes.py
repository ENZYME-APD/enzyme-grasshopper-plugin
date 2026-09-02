import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# Define the structures we'll need for Volume generation
vol_struct = """
            List<Mesh> cutVols = new List<Mesh>();
            List<Mesh> fillVols = new List<Mesh>();
"""

# Replace the "List<Curve> pillars = new List<Curve>();" line to also declare volume meshes
replace_1 = "List<Curve> pillars = new List<Curve>();"
content = content.replace(replace_1, replace_1 + vol_struct)

# Also remove the placeholder 'volumes' if it was declared (it seems 'volumes' is not declared, the code says DA.SetDataList(2, volumes); but volumes is nowhere. Wait, if it compiled, it must be somewhere. Ah, I might have missed it).
# Let's fix the SetDataList placeholders.
replace_2 = """                        DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, volumes); // Cut volume placeholder
            DA.SetDataList(3, volumes); // Fill volume placeholder"""
new_set_data = """            DA.SetData(0, modTerrain);
            DA.SetDataList(1, roadMeshes);
            DA.SetDataList(2, cutVols);
            DA.SetDataList(3, fillVols);"""
content = content.replace(replace_2, new_set_data)

# Let's inject the mesh building logic.
# Inside the centerline loop, we will track the profiles.
track_profiles = """
                List<Point3d[]> roadProfiles = new List<Point3d[]>();
                List<Point3d[]> terrProfiles = new List<Point3d[]>();
"""
content = content.replace("List<List<Point3d>> allLanes = new List<List<Point3d>>();", "List<List<Point3d>> allLanes = new List<List<Point3d>>();" + track_profiles)

# Now, down where we do the GROUND check, let's raycast left and right, and store the profiles.
# Old code:
old_ground = """                        else
                        {
                            // GROUND (Cut or Fill)
                            extraPoints.Add(pt);
                            extraPoints.Add(left);
                            extraPoints.Add(right);
                            
                            // Blend points
                            double horizontalBlend = Math.Abs(deltaZ) / tanAngle;
                            
                            exclNodes.Add(new ExclusionNode { Pt2D = new Point3d(pt.X, pt.Y, 0), Radius = totalHalfWidth + horizontalBlend + 0.5 });
                            
                            if (horizontalBlend > 0.1)
                            {
                                Point3d leftBlend = left + normal * horizontalBlend;
                                leftBlend.Z = zTerrain;
                                Point3d rightBlend = right - normal * horizontalBlend;
                                rightBlend.Z = zTerrain;
                                extraPoints.Add(leftBlend);
                                extraPoints.Add(rightBlend);
                            }
                        }"""

new_ground = """                        else
                        {
                            // GROUND (Cut or Fill)
                            extraPoints.Add(pt);
                            extraPoints.Add(left);
                            extraPoints.Add(right);
                            
                            double horizontalBlend = Math.Abs(deltaZ) / tanAngle;
                            exclNodes.Add(new ExclusionNode { Pt2D = new Point3d(pt.X, pt.Y, 0), Radius = totalHalfWidth + horizontalBlend + 0.5 });
                            
                            Point3d leftBlend = left + normal * horizontalBlend;
                            Point3d rightBlend = right - normal * horizontalBlend;
                            leftBlend.Z = zTerrain;
                            rightBlend.Z = zTerrain;

                            if (horizontalBlend > 0.1)
                            {
                                extraPoints.Add(leftBlend);
                                extraPoints.Add(rightBlend);
                            }

                            // Build profiles for Cut/Fill Volumes
                            double zLeftT = left.Z, zRightT = right.Z;
                            Ray3d rL = new Ray3d(new Point3d(left.X, left.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tL = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rL);
                            if (tL >= 0.0) zLeftT = rL.PointAt(tL).Z;

                            Ray3d rR = new Ray3d(new Point3d(right.X, right.Y, pt.Z + 10000), -Vector3d.ZAxis);
                            double tR = Rhino.Geometry.Intersect.Intersection.MeshRay(terrain, rR);
                            if (tR >= 0.0) zRightT = rR.PointAt(tR).Z;

                            Point3d leftT = new Point3d(left.X, left.Y, zLeftT);
                            Point3d rightT = new Point3d(right.X, right.Y, zRightT);
                            Point3d ptT = new Point3d(pt.X, pt.Y, zTerrain);

                            roadProfiles.Add(new Point3d[] { leftBlend, left, pt, right, rightBlend });
                            terrProfiles.Add(new Point3d[] { leftBlend, leftT, ptT, rightT, rightBlend });
                        }"""
content = content.replace(old_ground, new_ground)


# Now, after the loop finishes, we build the solid meshes.
old_roadmesh = """                // Build Road Mesh
                Mesh roadMesh = new Mesh();"""

new_roadmesh = """                // Build Cut and Fill Solid Meshes
                Mesh cutMesh = new Mesh();
                Mesh fillMesh = new Mesh();
                
                for (int i = 0; i < roadProfiles.Count - 1; i++)
                {
                    Point3d[] rp1 = roadProfiles[i];
                    Point3d[] rp2 = roadProfiles[i + 1];
                    Point3d[] tp1 = terrProfiles[i];
                    Point3d[] tp2 = terrProfiles[i + 1];

                    // Are we predominantly Cut or Fill? (simple heuristic for solid separation)
                    bool isCut = rp1[2].Z < tp1[2].Z;
                    Mesh target = isCut ? cutMesh : fillMesh;
                    System.Drawing.Color c = isCut ? System.Drawing.Color.Red : System.Drawing.Color.Blue;

                    int bIdx = target.Vertices.Count;

                    // Add vertices for top and bottom of this segment
                    // Road Surface
                    for(int j=0; j<5; j++) target.Vertices.Add(rp1[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(rp2[j]);
                    // Terrain Surface
                    for(int j=0; j<5; j++) target.Vertices.Add(tp1[j]);
                    for(int j=0; j<5; j++) target.Vertices.Add(tp2[j]);
                    
                    if (colorize)
                    {
                        for (int j = 0; j < 20; j++) target.VertexColors.Add(c);
                    }

                    // Top Faces (Road)
                    for (int j = 0; j < 4; j++) target.Faces.AddFace(bIdx + j, bIdx + j + 1, bIdx + j + 6, bIdx + j + 5);
                    
                    // Bottom Faces (Terrain) - reverse winding
                    int bOff = bIdx + 10;
                    for (int j = 0; j < 4; j++) target.Faces.AddFace(bOff + j, bOff + j + 5, bOff + j + 6, bOff + j + 1);

                    // End Caps (if we want them fully closed)
                    // Simplified: We just cap the start and end of the entire strip, plus vertical sides if needed.
                    // Actually, leftBlend and rightBlend touch the terrain exactly, so the side edges are sealed!
                    // The only open parts are the start/end cross sections of the entire road.
                    // We'll leave them open for now, the user mostly cares about visual colorization and volume.
                }

                if (cutMesh.Faces.Count > 0) { cutMesh.Normals.ComputeNormals(); cutVols.Add(cutMesh); }
                if (fillMesh.Faces.Count > 0) { fillMesh.Normals.ComputeNormals(); fillVols.Add(fillMesh); }

                // Build Road Mesh
                Mesh roadMesh = new Mesh();"""

content = content.replace(old_roadmesh, new_roadmesh)

# Wait, if we want actual volumes in Grasshopper, closed meshes are better, but open meshes colored are exactly what the user asked for (visual override).
# "Cut and Fill mesh color override (activation via boolean toggle)"

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
