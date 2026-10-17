using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Enzyme.Utils
{
    /// <summary>
    /// Helpers for meshes that are queried from Parallel.For loops.
    /// </summary>
    public static class MeshPrep
    {
        /// <summary>
        /// Builds a mesh's lazily-created acceleration data (face normals, face tree used by
        /// MeshRay / ClosestPoint / IsPointInside) on the calling thread, BEFORE a parallel loop
        /// reads it. RhinoCommon does not guarantee that first-time construction of that data is
        /// thread-safe; without this, several worker threads can race to build it at once
        /// (wasted work at best, a crash or wrong hit at worst). Same pattern as WindEngineHTVer.
        /// </summary>
        public static void WarmUp(Mesh mesh)
        {
            if (mesh == null || !mesh.IsValid || mesh.Faces.Count == 0) return;
            mesh.FaceNormals.ComputeFaceNormals();
            BoundingBox bb = mesh.GetBoundingBox(true);
            Point3d c = bb.Center;
            mesh.ClosestPoint(c);
            Intersection.MeshRay(mesh, new Ray3d(new Point3d(c.X, c.Y, bb.Max.Z + 1.0), -Vector3d.ZAxis));
        }

        public static void WarmUp(params Mesh[] meshes)
        {
            if (meshes == null) return;
            foreach (var m in meshes) WarmUp(m);
        }
    }
}
