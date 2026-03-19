namespace raytracing.Model
{
    public class LightSource
    {
        public Vec3 Position { get; private set; }
        public Vec3 Color { get; private set; }

        public LightSource(Vec3 position, Vec3 color)
        {
            Position = position;
            Color = color;
        }
    }
}
