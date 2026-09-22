import re

with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

# We need to replace the tangent flip logic.
# Find this block:
#                 double lastPillarDist = 0;
#                 Vector3d prevTangent = Vector3d.XAxis;
# 
#                 for (int i = 0; i < tParams.Length; i++)
#                 {
# ...
#                     double t = tParams[i];
#                     Point3d pt = nCrv.PointAt(t);
#                     Vector3d tangent = nCrv.TangentAt(t);
#                     tangent.Z = 0; 
#                     if (!tangent.Unitize()) tangent = prevTangent;
#                     else prevTangent = tangent;

old_block = """                double lastPillarDist = 0;
                Vector3d prevTangent = Vector3d.XAxis;

                for (int i = 0; i < tParams.Length; i++)
                {
                    if (rd.IsClosed && i == tParams.Length - 1 && rd.leftPts.Count > 0)
                    {
                        rd.leftPts.Add(rd.leftPts[0]);
                        rd.rightPts.Add(rd.rightPts[0]);
                        for (int j = 0; j < totalLanes; j++) rd.allLanes[j].Add(rd.allLanes[j][0]);
                        if (rd.roadProfiles.Count > 0) {
                            rd.roadProfiles.Add(rd.roadProfiles[0]);
                            rd.terrProfiles.Add(rd.terrProfiles[0]);
                        }
                        continue;
                    }

                    double t = tParams[i];
                    Point3d pt = nCrv.PointAt(t);
                    Vector3d tangent = nCrv.TangentAt(t);
                    tangent.Z = 0; 
                    if (!tangent.Unitize()) tangent = prevTangent;
                    else prevTangent = tangent;"""

new_block = """                double lastPillarDist = 0;
                Vector3d prevTangent = Vector3d.Unset;

                for (int i = 0; i < tParams.Length; i++)
                {
                    if (rd.IsClosed && i == tParams.Length - 1 && rd.leftPts.Count > 0)
                    {
                        rd.leftPts.Add(rd.leftPts[0]);
                        rd.rightPts.Add(rd.rightPts[0]);
                        for (int j = 0; j < totalLanes; j++) rd.allLanes[j].Add(rd.allLanes[j][0]);
                        if (rd.roadProfiles.Count > 0) {
                            rd.roadProfiles.Add(rd.roadProfiles[0]);
                            rd.terrProfiles.Add(rd.terrProfiles[0]);
                        }
                        continue;
                    }

                    double t = tParams[i];
                    Point3d pt = nCrv.PointAt(t);
                    Vector3d tangent = nCrv.TangentAt(t);
                    tangent.Z = 0; 
                    if (!tangent.Unitize()) {
                        if (prevTangent != Vector3d.Unset) tangent = prevTangent;
                        else tangent = Vector3d.XAxis;
                    } else {
                        // CRITICAL FIX: If the NURBS segment was joined backwards, the tangent will flip 180 degrees.
                        // We strictly un-flip it to preserve continuous sweeping orientation!
                        if (prevTangent != Vector3d.Unset && tangent * prevTangent < 0.0) {
                            tangent = -tangent; 
                        }
                        prevTangent = tangent;
                    }"""

content = content.replace(old_block, new_block)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
