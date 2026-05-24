using System;

namespace raytracing.Model
{
    public struct AABB
    {
        public Vec3 Min;
        public Vec3 Max;

        public AABB(Vec3 min, Vec3 max) { Min = min; Max = max; }

        public Vec3 Centroid => (Min + Max) * 0.5f;

        public static AABB FromTriangle(Triangle t)
        {
            const float pad = 0.0001f;
            return new AABB(
                new Vec3(
                    MathF.Min(t.A.X, MathF.Min(t.B.X, t.C.X)) - pad,
                    MathF.Min(t.A.Y, MathF.Min(t.B.Y, t.C.Y)) - pad,
                    MathF.Min(t.A.Z, MathF.Min(t.B.Z, t.C.Z)) - pad),
                new Vec3(
                    MathF.Max(t.A.X, MathF.Max(t.B.X, t.C.X)) + pad,
                    MathF.Max(t.A.Y, MathF.Max(t.B.Y, t.C.Y)) + pad,
                    MathF.Max(t.A.Z, MathF.Max(t.B.Z, t.C.Z)) + pad));
        }

        public static AABB FromSphere(Sphere s)
        {
            var r = new Vec3(s.Radius, s.Radius, s.Radius);
            return new AABB(s.Center - r, s.Center + r);
        }

        public static AABB Union(AABB a, AABB b) => new AABB(
            new Vec3(MathF.Min(a.Min.X, b.Min.X), MathF.Min(a.Min.Y, b.Min.Y), MathF.Min(a.Min.Z, b.Min.Z)),
            new Vec3(MathF.Max(a.Max.X, b.Max.X), MathF.Max(a.Max.Y, b.Max.Y), MathF.Max(a.Max.Z, b.Max.Z)));

        // Slab method — handles zero-direction components via ±infinity arithmetic
        public bool IntersectsRay(Ray ray)
        {
            float tMin = 0f;
            float tMax = float.MaxValue;

            float invDx = 1f / ray.Direction.X;
            float tx0 = (Min.X - ray.Origin.X) * invDx;
            float tx1 = (Max.X - ray.Origin.X) * invDx;
            if (invDx < 0f) (tx0, tx1) = (tx1, tx0);
            tMin = MathF.Max(tMin, tx0);
            tMax = MathF.Min(tMax, tx1);
            if (tMax < tMin) return false;

            float invDy = 1f / ray.Direction.Y;
            float ty0 = (Min.Y - ray.Origin.Y) * invDy;
            float ty1 = (Max.Y - ray.Origin.Y) * invDy;
            if (invDy < 0f) (ty0, ty1) = (ty1, ty0);
            tMin = MathF.Max(tMin, ty0);
            tMax = MathF.Min(tMax, ty1);
            if (tMax < tMin) return false;

            float invDz = 1f / ray.Direction.Z;
            float tz0 = (Min.Z - ray.Origin.Z) * invDz;
            float tz1 = (Max.Z - ray.Origin.Z) * invDz;
            if (invDz < 0f) (tz0, tz1) = (tz1, tz0);
            tMin = MathF.Max(tMin, tz0);
            tMax = MathF.Min(tMax, tz1);

            return tMax >= tMin;
        }
    }
}
