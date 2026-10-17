with open('Components/RoadGenerator.cs', 'r') as f:
    lines = f.readlines()

out_lines = []
for line in lines:
    if "public override Guid ComponentGuid" in line:
        break
    out_lines.append(line)

with open('Components/RoadGenerator.cs', 'w') as f:
    f.writelines(out_lines)
    f.write("""
        private double TriVolume(Point3d t1, Point3d t2, Point3d t3, Point3d b1, Point3d b2, Point3d b3)
        {
            double area2D = 0.5 * Math.Abs(t1.X*(t2.Y - t3.Y) + t2.X*(t3.Y - t1.Y) + t3.X*(t1.Y - t2.Y));
            double avgDz = ((t1.Z - b1.Z) + (t2.Z - b2.Z) + (t3.Z - b3.Z)) / 3.0;
            if (avgDz < 0) avgDz = 0;
            return area2D * avgDz;
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("E5A7B8C9-1234-4ABC-9DEF-0123456789AB"); }
        }
    }
}
""")
