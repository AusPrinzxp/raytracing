using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace raytracing.Model
{
    public static class ObjLoader
    {
        // Returns raw face vertex triples grouped by material name.
        // Use this to apply per-material colors before building Triangle objects.
        public static Dictionary<string, List<(Vec3 A, Vec3 B, Vec3 C)>> LoadGrouped(string path)
        {
            var vertices = new List<Vec3>();
            var groups   = new Dictionary<string, List<(Vec3, Vec3, Vec3)>>();
            string currentMat = "default";

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("usemtl "))
                {
                    currentMat = trimmed.Substring(7).Trim();
                    if (!groups.ContainsKey(currentMat))
                        groups[currentMat] = new List<(Vec3, Vec3, Vec3)>();
                }
                else if (trimmed.StartsWith("v "))
                {
                    var p = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    vertices.Add(new Vec3(
                        float.Parse(p[1], CultureInfo.InvariantCulture),
                        float.Parse(p[2], CultureInfo.InvariantCulture),
                        float.Parse(p[3], CultureInfo.InvariantCulture)));
                }
                else if (trimmed.StartsWith("f "))
                {
                    if (!groups.ContainsKey(currentMat))
                        groups[currentMat] = new List<(Vec3, Vec3, Vec3)>();

                    var parts   = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var indices = new int[parts.Length - 1];
                    for (int i = 0; i < indices.Length; i++)
                        indices[i] = int.Parse(parts[i + 1].Split('/')[0], CultureInfo.InvariantCulture) - 1;

                    for (int i = 1; i < indices.Length - 1; i++)
                        groups[currentMat].Add((vertices[indices[0]], vertices[indices[i]], vertices[indices[i + 1]]));
                }
            }

            return groups;
        }

        public static List<Triangle> Load(
            string path,
            Vec3 color,
            float ambient = 0.1f,
            float diffuse = 0.9f,
            float specular = 0.4f,
            float shininess = 32f,
            float reflectivity = 0f,
            float transparency = 0f,
            float ior = 1f)
        {
            var vertices = new List<Vec3>();
            var triangles = new List<Triangle>();

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("v "))
                {
                    var parts = trimmed.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    vertices.Add(new Vec3(
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)));
                }
                else if (trimmed.StartsWith("f "))
                {
                    var parts = trimmed.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    var indices = new int[parts.Length - 1];

                    for (int i = 0; i < indices.Length; i++)
                    {
                        // format can be: v  |  v/vt  |  v/vt/vn  |  v//vn
                        var sub = parts[i + 1].Split('/');
                        indices[i] = int.Parse(sub[0], CultureInfo.InvariantCulture) - 1;
                    }

                    // Fan triangulation: (0,1,2), (0,2,3), (0,3,4), ...
                    for (int i = 1; i < indices.Length - 1; i++)
                    {
                        triangles.Add(new Triangle(
                            vertices[indices[0]],
                            vertices[indices[i]],
                            vertices[indices[i + 1]],
                            color, ambient, diffuse, specular, shininess,
                            reflectivity, transparency, ior));
                    }
                }
            }

            return triangles;
        }
    }
}
