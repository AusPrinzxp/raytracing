using System;

namespace raytracing.Model
{
    public class Vec2 : IEquatable<Vec2>
    {
        public float X { get; }
        public float Y { get; }

        public Vec2(float x, float y) { X = x; Y = y; }

        public static Vec2 Zero => new Vec2(0f, 0f);
        public static Vec2 One => new Vec2(1f, 1f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 v) => new Vec2(-v.X, -v.Y);

        public static Vec2 operator *(Vec2 v, float s) => new Vec2(v.X * s, v.Y * s);
        public static Vec2 operator *(float s, Vec2 v) => v * s;
        public static Vec2 operator /(Vec2 v, float s) => new Vec2(v.X / s, v.Y / s);

        public float LengthSquared() => X * X + Y * Y;
        public float Length() => MathF.Sqrt(LengthSquared());

        public Vec2 Normalized()
        {
            float len = Length();
            return len > 0f ? this / len : Zero;
        }

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
    }
}
