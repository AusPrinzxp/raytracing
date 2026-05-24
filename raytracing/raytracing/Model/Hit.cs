namespace raytracing.Model
{
    public struct Hit
    {
        public float T;
        public Vec3 Position;
        public Vec3 Normal;
        public Vec3 Color;

        public float Ambient;
        public float Diffuse;
        public float Specular;
        public float Shininess;

        public float Reflectivity;
        public float Transparency;
        public float IOR;
    }
}
