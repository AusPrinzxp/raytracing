using System;

namespace raytracing.Model
{
    public class Vec3i : IEquatable<Vec3i>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public Vec3i(int x, int y, int z) { X = x; Y = y; Z = z; }

        public static Vec3i operator +(Vec3i a, Vec3i b) => new Vec3i(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3i operator -(Vec3i a, Vec3i b) => new Vec3i(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3i operator *(Vec3i v, int s) => new Vec3i(v.X * s, v.Y * s, v.Z * s);

        public int LengthSquared() => X * X + Y * Y + Z * Z;

        public bool Equals(Vec3i other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is Vec3i v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
