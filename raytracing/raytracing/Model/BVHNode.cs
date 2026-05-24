using System;
using System.Collections.Generic;

namespace raytracing.Model
{
    // Bounding Volume Hierarchy — wraps a list of bounded scene objects (Sphere, Triangle).
    // Reduces ray-object intersection from O(n) to O(log n).
    // Planes are unbounded and must be kept outside the BVH in the scene object list.
    public class BVHNode : ISceneObject
    {
        private readonly AABB _bounds;
        private readonly BVHNode? _left;
        private readonly BVHNode? _right;
        private readonly ISceneObject[]? _leaves;

        private const int LeafThreshold = 4;

        private BVHNode(AABB bounds, BVHNode left, BVHNode right)
        {
            _bounds = bounds; _left = left; _right = right;
        }

        private BVHNode(AABB bounds, ISceneObject[] leaves)
        {
            _bounds = bounds; _leaves = leaves;
        }

        public static BVHNode Build(List<ISceneObject> objects)
        {
            if (objects.Count == 0)
                throw new ArgumentException("Cannot build BVH from empty list.");
            return BuildRecursive(objects, 0, objects.Count);
        }

        private static BVHNode BuildRecursive(List<ISceneObject> objects, int start, int end)
        {
            int count = end - start;

            AABB bounds = GetAABB(objects[start]);
            for (int i = start + 1; i < end; i++)
                bounds = AABB.Union(bounds, GetAABB(objects[i]));

            if (count <= LeafThreshold)
            {
                var leaves = new ISceneObject[count];
                for (int i = 0; i < count; i++) leaves[i] = objects[start + i];
                return new BVHNode(bounds, leaves);
            }

            // Split on the axis with the widest centroid spread
            Vec3 cMin = GetAABB(objects[start]).Centroid;
            Vec3 cMax = cMin;
            for (int i = start + 1; i < end; i++)
            {
                Vec3 c = GetAABB(objects[i]).Centroid;
                cMin = new Vec3(MathF.Min(cMin.X, c.X), MathF.Min(cMin.Y, c.Y), MathF.Min(cMin.Z, c.Z));
                cMax = new Vec3(MathF.Max(cMax.X, c.X), MathF.Max(cMax.Y, c.Y), MathF.Max(cMax.Z, c.Z));
            }

            Vec3 span = cMax - cMin;
            int axis = span.X > span.Y ? (span.X > span.Z ? 0 : 2) : (span.Y > span.Z ? 1 : 2);

            // Median split: sort the slice and write back in place
            var slice = new List<ISceneObject>(count);
            for (int i = start; i < end; i++) slice.Add(objects[i]);
            slice.Sort((a, b) => Centroid(GetAABB(a), axis).CompareTo(Centroid(GetAABB(b), axis)));
            for (int i = 0; i < count; i++) objects[start + i] = slice[i];

            int mid = (start + end) / 2;
            return new BVHNode(bounds,
                BuildRecursive(objects, start, mid),
                BuildRecursive(objects, mid, end));
        }

        public bool TryIntersect(Ray ray, out Hit hit)
        {
            hit = default;
            if (!_bounds.IntersectsRay(ray)) return false;

            // Leaf: test all primitives, keep closest
            if (_leaves != null)
            {
                bool any = false;
                float closestT = float.PositiveInfinity;
                foreach (var obj in _leaves)
                {
                    if (obj.TryIntersect(ray, out Hit h) && h.T < closestT)
                    {
                        closestT = h.T; hit = h; any = true;
                    }
                }
                return any;
            }

            // Inner node: descend both children, return closer hit
            bool hitL = _left!.TryIntersect(ray, out Hit lh);
            bool hitR = _right!.TryIntersect(ray, out Hit rh);

            if (hitL && hitR) { hit = lh.T < rh.T ? lh : rh; return true; }
            if (hitL) { hit = lh; return true; }
            if (hitR) { hit = rh; return true; }
            return false;
        }

        private static AABB GetAABB(ISceneObject obj) => obj switch
        {
            Triangle t => AABB.FromTriangle(t),
            Sphere s   => AABB.FromSphere(s),
            _          => throw new NotSupportedException(
                              $"{obj.GetType().Name} is unbounded — keep Planes outside the BVH.")
        };

        private static float Centroid(AABB b, int axis) => axis switch
        {
            0 => b.Centroid.X,
            1 => b.Centroid.Y,
            _ => b.Centroid.Z,
        };
    }
}
