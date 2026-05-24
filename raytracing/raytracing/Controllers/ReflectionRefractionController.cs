using Microsoft.AspNetCore.Mvc;
using raytracing.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace raytracing.Controllers
{
    public class ReflectionRefractionController : Controller
    {
        private const int MAX_DEPTH = 3;

        [HttpGet("/reflection-refraction")]
        public IActionResult Render(int width = 800, int height = 450)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            Camera camera = new Camera(
                new Vec3(0f, 1f, -6f),
                new Vec3(0f, 0f, 3f),
                new Vec3(0f, 1f, 0f),
                60f,
                (float)width / height
            );

            Vec3 lightPos = new Vec3(-4f, 5f, -2f);
            Vec3 lightColor = new Vec3(1f, 1f, 1f);

            Vec3 background = new Vec3(0f, 0f, 0f);

            var objects = new List<ISceneObject>
            {
                // reflective sphere
                new Sphere(new Vec3(-1.5f, 0f, 4f), 1f, new Vec3(0.2f,0.6f,1f), 0.1f,1f,0.9f,64f, reflectivity:0.7f),

                // refractive sphere (glass)
                new Sphere(new Vec3(1.5f, 0f, 4f), 1f, new Vec3(0.9f,0.9f,1f), 0.1f,1f,0.2f,32f, reflectivity:0.1f, transparency:0.9f, ior:1.5f),

                // floor
                new Plane(new Vec3(0f,-1.5f,0f), new Vec3(0f,1f,0f), new Vec3(0.7f,0.7f,0.7f))
            };

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

                        Vec3 col = Trace(ray, objects, lightPos, lightColor, background, 0);

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

        private Vec3 Trace(Ray ray, List<ISceneObject> objects, Vec3 lightPos, Vec3 lightColor, Vec3 background, int depth)
        {
            if (depth > MAX_DEPTH)
                return background;

            if (!RayTracer.TraceClosest(ray, objects, out Hit hit))
                return background;

            Vec3 viewDir = (-ray.Direction).Normalized();
            Vec3 lightDir = (lightPos - hit.Position).Normalized();

            bool inShadow = RayTracer.IsInShadow(hit.Position, lightPos, objects);

            float ndotl = MathF.Max(0f, Vec3.Dot(hit.Normal, lightDir));

            // Blinn-Phong
            Vec3 halfDir = (lightDir + viewDir).Normalized();
            float spec = MathF.Pow(MathF.Max(0f, Vec3.Dot(hit.Normal, halfDir)), hit.Shininess);

            Vec3 color =
                hit.Color * hit.Ambient +
                (!inShadow ? (hit.Color * ndotl * hit.Diffuse) : Vec3.Zero) +
                (!inShadow ? (lightColor * spec * hit.Specular) : Vec3.Zero);

            // --- Reflection ---
            if (hit.Reflectivity > 0f)
            {
                Vec3 reflectDir = Reflect(ray.Direction, hit.Normal);
                Ray reflectRay = new Ray(hit.Position + reflectDir * 0.001f, reflectDir);

                Vec3 reflectColor = Trace(reflectRay, objects, lightPos, lightColor, background, depth + 1);

                color = color * (1 - hit.Reflectivity) + reflectColor * hit.Reflectivity;
            }

            // --- Refraction ---
            if (hit.Transparency > 0f)
            {
                if (Refract(ray.Direction, hit.Normal, hit.IOR, out Vec3 refractDir))
                {
                    Ray refractRay = new Ray(hit.Position + refractDir * 0.001f, refractDir);

                    Vec3 refractColor = Trace(refractRay, objects, lightPos, lightColor, background, depth + 1);

                    color = color * (1 - hit.Transparency) + refractColor * hit.Transparency;
                }
            }

            return Vec3.Clamp(color, 0f, 1f);
        }

        private Vec3 Reflect(Vec3 I, Vec3 N)
        {
            return I - N * 2f * Vec3.Dot(I, N);
        }

        private bool Refract(Vec3 I, Vec3 N, float ior, out Vec3 refracted)
        {
            float cosi = Math.Clamp(Vec3.Dot(I, N), -1f, 1f);
            float etai = 1f, etat = ior;
            Vec3 n = N;

            if (cosi < 0)
                cosi = -cosi;
            else
            {
                (etai, etat) = (etat, etai);
                n = -N;
            }

            float eta = etai / etat;
            float k = 1 - eta * eta * (1 - cosi * cosi);

            if (k < 0)
            {
                refracted = default;
                return false; // total internal reflection
            }

            refracted = I * eta + n * (eta * cosi - MathF.Sqrt(k));
            return true;
        }
    }
}
