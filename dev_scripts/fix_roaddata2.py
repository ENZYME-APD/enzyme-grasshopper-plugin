with open('Components/RoadGenerator.cs', 'r') as f:
    content = f.read()

road_data_class = """
        private class RoadData
        {
            public List<Point3d> leftPts = new List<Point3d>();
            public List<Point3d> rightPts = new List<Point3d>();
            public List<List<Point3d>> allLanes = new List<List<Point3d>>();
            public List<Point3d[]> roadProfiles = new List<Point3d[]>();
            public List<Point3d[]> terrProfiles = new List<Point3d[]>();
            public List<Tuple<Point3d, int>> extraPoints = new List<Tuple<Point3d, int>>();
            public List<Tuple<Point3d, int>> asphaltCenters = new List<Tuple<Point3d, int>>();
            public List<Tuple<Point3d, double>> daylightFootprints = new List<Tuple<Point3d, double>>();
            public List<LineCurve> pillars = new List<LineCurve>();
            public bool IsClosed = false;
        }

        private double TriVolume("""

content = content.replace("private double TriVolume(", road_data_class)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.write(content)
