with open('Components/RoadGenerator.cs', 'r') as f:
    lines = f.readlines()

out_lines = []
for line in lines:
    if "private double TriVolume(" in line:
        out_lines.append("""
        private class RoadData
        {
            public List<Point3d> leftPts = new List<Point3d>();
            public List<Point3d> rightPts = new List<Point3d>();
            public List<List<Point3d>> allLanes = new List<List<Point3d>>();
            public List<Point3d[]> roadProfiles = new List<Point3d[]>();
            public List<Point3d[]> terrProfiles = new List<Point3d[]>();
            public List<Point3d> extraPoints = new List<Point3d>();
            public List<Point3d> asphaltCenters = new List<Point3d>();
            public List<Tuple<Point3d, double>> daylightFootprints = new List<Tuple<Point3d, double>>();
            public List<LineCurve> pillars = new List<LineCurve>();
            public bool IsClosed = false;
        }

""")
    out_lines.append(line)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.writelines(out_lines)
