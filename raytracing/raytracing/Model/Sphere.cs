namespace raytracing.Model
{
    public class Sphere : ISceneObject
    {
        public Vec3 Center { get; }
        public float Radius { get; }
        public Vec3 Color { get; }

        public Sphere(Vec3 center, float radius, Vec3 color)
        {
            Center = center;
            Radius = radius;
            Color = color;
        }

        public bool TryIntersect(Ray ray, out Hit hit)
        {
            hit = default;

            Vec3 oc = ray.Origin - Center;

            float a = Vec3.Dot(ray.Direction, ray.Direction);
            float b = 2f * Vec3.Dot(oc, ray.Direction);
            float c = Vec3.Dot(oc, oc) - Radius * Radius;

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
                return false;

            float sqrtD = MathF.Sqrt(discriminant);
            float t0 = (-b - sqrtD) / (2f * a);
            float t1 = (-b + sqrtD) / (2f * a);

            const float epsilon = 0.0001f;
            float t = float.PositiveInfinity;

            if (t0 > epsilon) t = t0;
            else if (t1 > epsilon) t = t1;
            else return false;

            Vec3 position = ray.At(t);
            Vec3 normal = (position - Center).Normalized();

            hit = new Hit
            {
                T = t,
                Position = position,
                Normal = normal,
                Color = Color
            };

            return true;
        }
    }
}
