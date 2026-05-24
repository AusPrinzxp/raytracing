using Microsoft.AspNetCore.Mvc;
using raytracing.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace raytracing.Controllers
{
    public class SemesterController : Controller
    {
        private const int MAX_DEPTH = 5;

        [HttpGet("/semester")]
        public IActionResult Render(int width = 800, int height = 450)
        {
            Vec3 bg = new Vec3(0.01f, 0.01f, 0.02f);

            // 2 light sources
            var lights = new List<(Vec3 pos, Vec3 color)>
            {
                (new Vec3(-5f, 8f, -1f), new Vec3(1.0f, 0.92f, 0.75f)),   // warm key light
                (new Vec3(6f,  5f,  2f), new Vec3(0.35f, 0.5f, 1.0f)),    // cool fill light
            };

            var objects = new List<ISceneObject>();

            // === Object type 1: Plane ===

            // Ground — slightly reflective dark floor
            objects.Add(new Plane(
                new Vec3(0f, -1f, 0f), new Vec3(0f, 1f, 0f),
                new Vec3(0.28f, 0.28f, 0.32f),
                ambient: 0.08f, diffuse: 0.7f, specular: 0.3f, shininess: 32f, reflectivity: 0.25f));

            // Back wall
            objects.Add(new Plane(
                new Vec3(0f, 0f, 9f), new Vec3(0f, 0f, -1f),
                new Vec3(0.18f, 0.18f, 0.22f),
                ambient: 0.1f, diffuse: 0.6f, specular: 0.05f, shininess: 8f));

            // === Object type 2: Sphere ===

            // Chrome mirror sphere — demonstrates reflection
            objects.Add(new Sphere(
                new Vec3(-1.8f, 0f, 4f), 1f, new Vec3(0.85f, 0.87f, 0.9f),
                ambient: 0.04f, diffuse: 0.05f, specular: 1.0f, shininess: 512f, reflectivity: 0.92f));

            // Glass sphere — demonstrates refraction
            objects.Add(new Sphere(
                new Vec3(1.8f, 0f, 4f), 1f, new Vec3(0.92f, 0.97f, 1.0f),
                ambient: 0.04f, diffuse: 0.05f, specular: 0.95f, shininess: 256f,
                reflectivity: 0.08f, transparency: 0.92f, ior: 1.5f));

            // Gold sphere — reflective, high shininess
            objects.Add(new Sphere(
                new Vec3(0f, -0.5f, 2.5f), 0.5f, new Vec3(1.0f, 0.72f, 0.08f),
                ambient: 0.1f, diffuse: 0.8f, specular: 1.0f, shininess: 256f, reflectivity: 0.35f));

            // Red sphere
            objects.Add(new Sphere(
                new Vec3(-3.8f, -0.5f, 3.5f), 0.5f, new Vec3(0.95f, 0.15f, 0.1f),
                ambient: 0.1f, diffuse: 0.85f, specular: 0.7f, shininess: 64f));

            // Green sphere
            objects.Add(new Sphere(
                new Vec3(3.8f, -0.5f, 3.5f), 0.5f, new Vec3(0.1f, 0.82f, 0.2f),
                ambient: 0.1f, diffuse: 0.85f, specular: 0.7f, shininess: 64f));

            // Purple sphere
            objects.Add(new Sphere(
                new Vec3(-3.2f, -0.65f, 5.5f), 0.35f, new Vec3(0.4f, 0.1f, 0.9f),
                ambient: 0.1f, diffuse: 0.85f, specular: 0.6f, shininess: 48f));

            // Cyan sphere
            objects.Add(new Sphere(
                new Vec3(3.2f, -0.65f, 5.5f), 0.35f, new Vec3(0.1f, 0.8f, 0.9f),
                ambient: 0.1f, diffuse: 0.85f, specular: 0.8f, shininess: 96f));

            // === Object type 3: Triangle (orange cube = 12 triangles) ===
            objects.AddRange(CreateCube(
                new Vec3(0f, -0.4f, 6.8f), 1.2f, new Vec3(0.9f, 0.38f, 0.08f),
                ambient: 0.1f, diffuse: 0.85f, specular: 0.5f, shininess: 48f));

            // Camera: elevated, looking forward into the scene
            var camera = new Camera(
                new Vec3(0f, 1.5f, -4f),
                new Vec3(0f, 0.3f, 3f),
                new Vec3(0f, 1f, 0f),
                55f,
                (float)width / height);

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = data.Stride;
                byte[] buffer = new byte[stride * height];

                Parallel.For(0, height, y =>
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        Ray ray = camera.GetRay(x, y, width, height);
                        Vec3 col = Trace(ray, objects, lights, bg, 0);

                        int i = row + x * 4;
                        buffer[i + 0] = (byte)(Math.Clamp(col.Z, 0f, 1f) * 255);
                        buffer[i + 1] = (byte)(Math.Clamp(col.Y, 0f, 1f) * 255);
                        buffer[i + 2] = (byte)(Math.Clamp(col.X, 0f, 1f) * 255);
                        buffer[i + 3] = 255;
                    }
                });

                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }

        private Vec3 Trace(Ray ray, List<ISceneObject> objects, List<(Vec3 pos, Vec3 color)> lights, Vec3 bg, int depth)
        {
            if (depth > MAX_DEPTH)
                return bg;

            if (!RayTracer.TraceClosest(ray, objects, out Hit hit))
                return bg;

            Vec3 viewDir = (-ray.Direction).Normalized();

            // Ambient base
            Vec3 color = hit.Color * hit.Ambient;

            // Blinn-Phong from each light with shadow test
            foreach (var (lPos, lColor) in lights)
            {
                if (RayTracer.IsInShadow(hit.Position, lPos, objects))
                    continue;

                Vec3 lightDir = (lPos - hit.Position).Normalized();
                float ndotl = MathF.Max(0f, Vec3.Dot(hit.Normal, lightDir));
                Vec3 halfDir = (lightDir + viewDir).Normalized();
                float spec = MathF.Pow(MathF.Max(0f, Vec3.Dot(hit.Normal, halfDir)), hit.Shininess);

                color = color
                    + hit.Color * lColor * (ndotl * hit.Diffuse)
                    + lColor * (spec * hit.Specular);
            }

            // Reflection
            if (hit.Reflectivity > 0f)
            {
                Vec3 reflDir = ray.Direction - hit.Normal * (2f * Vec3.Dot(ray.Direction, hit.Normal));
                Vec3 reflColor = Trace(new Ray(hit.Position + reflDir * 0.001f, reflDir), objects, lights, bg, depth + 1);
                color = color * (1f - hit.Reflectivity) + reflColor * hit.Reflectivity;
            }

            // Refraction
            if (hit.Transparency > 0f && Refract(ray.Direction, hit.Normal, hit.IOR, out Vec3 refrDir))
            {
                Vec3 refrColor = Trace(new Ray(hit.Position + refrDir * 0.001f, refrDir), objects, lights, bg, depth + 1);
                color = color * (1f - hit.Transparency) + refrColor * hit.Transparency;
            }

            return Vec3.Clamp(color, 0f, 1f);
        }

        private static bool Refract(Vec3 I, Vec3 N, float ior, out Vec3 refracted)
        {
            float cosi = Math.Clamp(Vec3.Dot(I, N), -1f, 1f);
            float etai = 1f, etat = ior;
            Vec3 n = N;

            if (cosi < 0f)
                cosi = -cosi;
            else
            {
                (etai, etat) = (etat, etai);
                n = -N;
            }

            float eta = etai / etat;
            float k = 1f - eta * eta * (1f - cosi * cosi);

            if (k < 0f) { refracted = default; return false; }

            refracted = I * eta + n * (eta * cosi - MathF.Sqrt(k));
            return true;
        }

        private static List<ISceneObject> CreateCube(Vec3 center, float size, Vec3 color,
            float ambient, float diffuse, float specular, float shininess)
        {
            float s = size / 2f;
            Vec3[] v =
            {
                center + new Vec3(-s,-s,-s), center + new Vec3( s,-s,-s),
                center + new Vec3( s, s,-s), center + new Vec3(-s, s,-s),
                center + new Vec3(-s,-s, s), center + new Vec3( s,-s, s),
                center + new Vec3( s, s, s), center + new Vec3(-s, s, s),
            };

            Triangle T(Vec3 a, Vec3 b, Vec3 c) =>
                new Triangle(a, b, c, color, ambient, diffuse, specular, shininess);

            return new List<ISceneObject>
            {
                T(v[4],v[5],v[6]), T(v[4],v[6],v[7]), // front
                T(v[0],v[2],v[1]), T(v[0],v[3],v[2]), // back
                T(v[0],v[7],v[3]), T(v[0],v[4],v[7]), // left
                T(v[1],v[2],v[6]), T(v[1],v[6],v[5]), // right
                T(v[3],v[7],v[6]), T(v[3],v[6],v[2]), // top
                T(v[0],v[1],v[5]), T(v[0],v[5],v[4]), // bottom
            };
        }
    }
}
