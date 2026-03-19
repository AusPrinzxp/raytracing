using System;

namespace raytracing.Model
{
    public class Vec3 : IEquatable<Vec3>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static Vec3 Zero => new Vec3(0f, 0f, 0f);
        public static Vec3 One => new Vec3(1f, 1f, 1f);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 v) => new Vec3(-v.X, -v.Y, -v.Z);

        public static Vec3 operator *(Vec3 v, float s) => new Vec3(v.X * s, v.Y * s, v.Z * s);
        public static Vec3 operator *(float s, Vec3 v) => v * s;
        public static Vec3 operator /(Vec3 v, float s) => new Vec3(v.X / s, v.Y / s, v.Z / s);

        // Component-wise multiply (useful for colors)
        public static Vec3 operator *(Vec3 a, Vec3 b) => new Vec3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        public float LengthSquared() => X * X + Y * Y + Z * Z;
        public float Length() => MathF.Sqrt(LengthSquared());

        public Vec3 Normalized()
        {
            float len = Length();
            return len > 0f ? this / len : Zero;
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;

        public static Vec3 Clamp(Vec3 v, float min, float max) =>
            new Vec3(
                MathF.Min(MathF.Max(v.X, min), max),
                MathF.Min(MathF.Max(v.Y, min), max),
                MathF.Min(MathF.Max(v.Z, min), max)
            );

        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is Vec3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
