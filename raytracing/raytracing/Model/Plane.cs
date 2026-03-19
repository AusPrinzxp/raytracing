namespace raytracing.Model
{
    public class Plane : ISceneObject
    {
        public Vec3 Point { get; }
        public Vec3 Normal { get; }
        public Vec3 Color { get; }

        public Plane(Vec3 point, Vec3 normal, Vec3 color)
        {
            Point = point;
            Normal = normal.Normalized();
            Color = color;
        }

        public bool TryIntersect(Ray ray, out Hit hit)
        {
            hit = default;

            float denom = Vec3.Dot(ray.Direction, Normal);

            const float epsilon = 0.0001f;
            if (MathF.Abs(denom) < epsilon)
                return false; // ray is parallel to plane

            float t = Vec3.Dot(Point - ray.Origin, Normal) / denom;

            if (t <= epsilon)
                return false;

            Vec3 position = ray.At(t);

            hit = new Hit
            {
                T = t,
                Position = position,
                Normal = Normal,
                Color = Color
            };

            return true;
        }
    }
}
