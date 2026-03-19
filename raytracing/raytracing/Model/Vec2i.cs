using System;

namespace raytracing.Model
{
    /// <summary>
    /// Simple 2D integer vector for pixel/grid math.
    /// </summary>
    public class Vec2i : IEquatable<Vec2i>
    {
        public int X { get; }
        public int Y { get; }

        public Vec2i(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Vec2i operator +(Vec2i a, Vec2i b) => new Vec2i(a.X + b.X, a.Y + b.Y);
        public static Vec2i operator -(Vec2i a, Vec2i b) => new Vec2i(a.X - b.X, a.Y - b.Y);
        public static Vec2i operator *(Vec2i v, int s) => new Vec2i(v.X * s, v.Y * s);
        public static Vec2i operator *(int s, Vec2i v) => v * s;

        public int LengthSquared() => X * X + Y * Y;

        public static int Dot(Vec2i a, Vec2i b) => a.X * b.X + a.Y * b.Y;

        public bool Equals(Vec2i other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is Vec2i v && Equals(v);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X}, {Y})";
    }
}
