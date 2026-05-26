namespace raytracing.Model
{
    public class Puddle : ISceneObject
    {
        private readonly Vec3  _center;
        private readonly float _baseRadius;

        private static readonly Vec3 _normal = new Vec3(0f, 1f, 0f);
        private static readonly Vec3 _color  = new Vec3(0.01f, 0.015f, 0.025f);

        public Puddle(Vec3 center, float baseRadius)
        {
            _center     = center;
            _baseRadius = baseRadius;
        }

        private bool IsInside(float x, float z)
        {
            float dx = x - _center.X;
            float dz = z - _center.Z;

            // Stretch slightly along Z so it looks like water that flowed forward
            float sx = dx * 0.72f;
            float sz = dz;

            float angle = MathF.Atan2(sz, sx);
            float dist  = MathF.Sqrt(sx * sx + sz * sz);

            // Sum of sinusoids at incommensurate frequencies → organic, non-repeating boundary
            float r = _baseRadius * (
                1.00f
                + 0.30f * MathF.Sin(1.9f * angle + 0.6f)
                + 0.18f * MathF.Sin(3.3f * angle + 1.4f)
                + 0.12f * MathF.Sin(5.7f * angle + 2.9f)
                + 0.08f * MathF.Sin(8.2f * angle + 0.3f)
                + 0.05f * MathF.Sin(11.5f * angle + 1.7f)
            );

            return dist < r;
        }

        public bool TryIntersect(Ray ray, out Hit hit)
        {
            hit = default;

            const float epsilon = 0.0001f;
            if (MathF.Abs(ray.Direction.Y) < epsilon) return false;

            float t = (_center.Y - ray.Origin.Y) / ray.Direction.Y;
            if (t <= epsilon) return false;

            Vec3 pos = ray.At(t);
            if (!IsInside(pos.X, pos.Z)) return false;

            hit = new Hit
            {
                T            = t,
                Position     = pos,
                Normal       = _normal,
                Color        = _color,
                Ambient      = 0.04f,
                Diffuse      = 0.12f,
                Specular     = 0.95f,
                Shininess    = 160f,
                Reflectivity = 0.72f,
                Transparency = 0f,
                IOR          = 1.33f
            };
            return true;
        }
    }
}
