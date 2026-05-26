using System;

namespace raytracing.Model
{
    public class Camera
    {
        public Vec3 Position { get; set; }
        public Vec3 Forward { get; private set; }
        public Vec3 Right { get; private set; }
        public Vec3 Up { get; private set; }

        public float FovDegrees { get; set; }
        public float AspectRatio { get; set; }

        public Camera(Vec3 position, Vec3 lookAt, Vec3 worldUp, float fovDegrees, float aspectRatio)
        {
            Position = position;
            FovDegrees = fovDegrees;
            AspectRatio = aspectRatio;

            UpdateVectors(lookAt, worldUp);
        }

        public void UpdateVectors(Vec3 lookAt, Vec3 worldUp)
        {
            Forward = (lookAt - Position).Normalized();
            Right = Vec3.Cross(Forward, worldUp).Normalized();
            Up = Vec3.Cross(Right, Forward).Normalized();
        }

        public Ray GetRay(int x, int y, int width, int height)
            => GetRay(x + 0.5f, y + 0.5f, width, height);

        public Ray GetRay(float px, float py, int width, int height)
        {
            float fovRad = FovDegrees * (MathF.PI / 180f);
            float halfHeight = MathF.Tan(fovRad / 2f);
            float halfWidth = AspectRatio * halfHeight;

            float u = (2f * (px / width)  - 1f) * halfWidth;
            float v = (1f - 2f * (py / height)) * halfHeight;

            Vec3 direction = (Forward + Right * u + Up * v).Normalized();
            return new Ray(Position, direction);
        }
    }
}
