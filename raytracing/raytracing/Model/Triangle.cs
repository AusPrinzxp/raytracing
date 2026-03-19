namespace raytracing.Model
{
    public class Triangle : ISceneObject
    {
        public Vec3 A { get; }
        public Vec3 B { get; }
        public Vec3 C { get; }
        public Vec3 Color { get; }

        public Triangle(Vec3 a, Vec3 b, Vec3 c, Vec3 color)
        {
            A = a;
            B = b;
            C = c;
            Color = color;
        }

        public bool TryIntersect(Ray ray, out Hit hit)
        {
            hit = default;

            Vec3 ab = B - A;
            Vec3 ac = C - A;
            Vec3 normal = Vec3.Cross(ab, ac).Normalized();

            float denom = Vec3.Dot(ray.Direction, normal);
            const float epsilon = 0.0001f;

            if (MathF.Abs(denom) < epsilon)
                return false; // ray parallel to triangle plane

            float t = Vec3.Dot(A - ray.Origin, normal) / denom;
            if (t <= epsilon)
                return false;

            Vec3 p = ray.At(t);

            if (!IsPointInTriangle(p, A, B, C, normal))
                return false;

            hit = new Hit
            {
                T = t,
                Position = p,
                Normal = normal,
                Color = Color
            };

            return true;
        }

        private static bool IsPointInTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c, Vec3 normal)
        {
            Vec3 ab = b - a;
            Vec3 bc = c - b;
            Vec3 ca = a - c;

            Vec3 ap = p - a;
            Vec3 bp = p - b;
            Vec3 cp = p - c;

            Vec3 cross1 = Vec3.Cross(ab, ap);
            Vec3 cross2 = Vec3.Cross(bc, bp);
            Vec3 cross3 = Vec3.Cross(ca, cp);

            float d1 = Vec3.Dot(cross1, normal);
            float d2 = Vec3.Dot(cross2, normal);
            float d3 = Vec3.Dot(cross3, normal);

            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }
    }
}
