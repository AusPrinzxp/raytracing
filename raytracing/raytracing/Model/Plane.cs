namespace raytracing.Model
{
    public class Plane : ISceneObject
    {
        public Vec3 Point { get; }
        public Vec3 Normal { get; }
        public Vec3 Color { get; }
        public Vec3 ColorB { get; }          // grout / second stripe or tile color
        public float TileSize { get; }       // world-space tile/stripe width; 0 = no pattern
        public float GroutFraction { get; }  // fraction of cell that is grout/line
        public bool VerticalStripes { get; } // true = vertical stripes instead of tile grid

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

        // Optional texture overlay
        private readonly Vec3[]? _texture;
        private readonly int _texW, _texH;
        private readonly Vec3 _texOrigin;
        private readonly float _texWorldW, _texWorldH;

        public Plane(Vec3 point, Vec3 normal, Vec3 color, float ambient = 0.1f,
            float diffuse = 1.0f, float specular = 0.5f, float shininess = 32f,
            float reflectivity = 0f, float transparency = 0f, float ior = 1f,
            Vec3 colorB = default, float tileSize = 0f, float groutFraction = 0.05f,
            bool verticalStripes = false,
            Vec3[]? texture = null, int texW = 0, int texH = 0,
            Vec3 texOrigin = default, float texWorldW = 1f, float texWorldH = 1f)
        {
            Point = point;
            Normal = normal.Normalized();
            Color = color;
            ColorB = colorB == default ? new Vec3(0.5f, 0.5f, 0.5f) : colorB;
            TileSize = tileSize;
            GroutFraction = groutFraction;
            VerticalStripes = verticalStripes;
            Ambient = ambient;
            Diffuse = diffuse;
            Specular = specular;
            Shininess = shininess;
            Reflectivity = reflectivity;
            Transparency = transparency;
            IOR = ior;

            _texture  = texture;
            _texW     = texW; _texH = texH;
            _texOrigin  = texOrigin;
            _texWorldW  = texWorldW; _texWorldH = texWorldH;

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
                float fu = u - MathF.Floor(u);
                if (VerticalStripes)
                {
                    // Alternate Color / ColorB per stripe; thin line at boundary
                    bool isLine = fu < GroutFraction || fu > (1f - GroutFraction);
                    bool isOdd  = MathF.Floor(u) % 2f != 0f;
                    tileColor = isLine ? ColorB : (isOdd ? ColorB : Color);
                }
                else
                {
                    float v = Vec3.Dot(position, _tang2) / TileSize;
                    float fv = v - MathF.Floor(v);
                    bool isGrout = fu < GroutFraction || fu > (1f - GroutFraction)
                                || fv < GroutFraction || fv > (1f - GroutFraction);
                    tileColor = isGrout ? ColorB : Color;
                }
            }

            if (_texture != null && _texW > 0 && _texH > 0)
            {
                float tu = Vec3.Dot(position - _texOrigin, _tang1) / _texWorldW;
                float tv = Vec3.Dot(position - _texOrigin, _tang2) / _texWorldH;
                if (tu >= 0f && tu <= 1f && tv >= 0f && tv <= 1f)
                {
                    int tx = Math.Clamp((int)(tu * _texW), 0, _texW - 1);
                    int ty = Math.Clamp((int)((1f - tv) * _texH), 0, _texH - 1);
                    Vec3 tp = _texture[ty * _texW + tx];
                    if (tp.X + tp.Y + tp.Z > 0.14f)
                        tileColor = tp;
                }
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
