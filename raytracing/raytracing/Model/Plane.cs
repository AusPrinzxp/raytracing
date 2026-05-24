namespace raytracing.Model
{
    public class Plane : ISceneObject
    {
        public Vec3 Point { get; }
        public Vec3 Normal { get; }
        public Vec3 Color { get; }
        public Vec3 ColorB { get; }       // grout / second tile color
        public float TileSize { get; }    // world-space tile width; 0 = no pattern
        public float GroutFraction { get; } // fraction of tile that is grout

        public float Ambient { get; }
        public float Diffuse { get; }
        public float Specular { get; }
        public float Shininess { get; }
        public float Reflectivity { get; }
        public float Transparency { get; }
        public float IOR { get; }

        // Precomputed tangent basis for UV projection
        private readonly Vec3 _tang1;
        private readonly Vec3 _tang2;

        public Plane(Vec3 point, Vec3 normal, Vec3 color, float ambient = 0.1f,
            float diffuse = 1.0f, float specular = 0.5f, float shininess = 32f,
            float reflectivity = 0f, float transparency = 0f, float ior = 1f,
            Vec3 colorB = default, float tileSize = 0f, float groutFraction = 0.05f)
        {
            Point = point;
            Normal = normal.Normalized();
            Color = color;
            ColorB = colorB == default ? new Vec3(0.5f, 0.5f, 0.5f) : colorB;
            TileSize = tileSize;
            GroutFraction = groutFraction;
            Ambient = ambient;
            Diffuse = diffuse;
            Specular = specular;
            Shininess = shininess;
            Reflectivity = reflectivity;
            Transparency = transparency;
            IOR = ior;

            // Build orthonormal tangent frame once
            Vec3 up = MathF.Abs(Normal.Y) > 0.9f ? new Vec3(1f, 0f, 0f) : new Vec3(0f, 1f, 0f);
            _tang1 = Vec3.Cross(up, Normal).Normalized();
            _tang2 = Vec3.Cross(Normal, _tang1).Normalized();
        }

        public bool TryIntersect(Ray ray, out Hit hit)
        {
            hit = default;

            float denom = Vec3.Dot(ray.Direction, Normal);

            const float epsilon = 0.0001f;
            if (MathF.Abs(denom) < epsilon)
                return false;

            float t = Vec3.Dot(Point - ray.Origin, Normal) / denom;
            if (t <= epsilon)
                return false;

            Vec3 position = ray.At(t);

            Vec3 tileColor = Color;
            if (TileSize > 0f)
            {
                float u = Vec3.Dot(position, _tang1) / TileSize;
                float v = Vec3.Dot(position, _tang2) / TileSize;
                float fu = u - MathF.Floor(u);
                float fv = v - MathF.Floor(v);
                bool isGrout = fu < GroutFraction || fu > (1f - GroutFraction)
                            || fv < GroutFraction || fv > (1f - GroutFraction);
                tileColor = isGrout ? ColorB : Color;
            }

            hit = new Hit
            {
                T = t,
                Position = position,
                Normal = Normal,
                Color = tileColor,
                Ambient = Ambient,
                Diffuse = Diffuse,
                Specular = Specular,
                Shininess = Shininess,
                Reflectivity = Reflectivity,
                Transparency = Transparency,
                IOR = IOR
            };

            return true;
        }
    }
}
