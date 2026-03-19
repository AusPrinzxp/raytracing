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
    public class SphereFaceController : Controller
    {
        private LightSource lightSource = new LightSource(new Vec3(-2.0f, 2.2f, -2.0f), new Vec3(0.0f, 0.0f, 1.0f));
        private LightSource lightSource2 = new LightSource(new Vec3(2.0f, 2.2f, -2.0f), new Vec3(1.0f, 0.0f, 0.0f));

        private Vec3 backgroundColor = new Vec3(0.0f, 0.0f, 0.0f);

        [HttpGet("/sphere-face")]
        public IActionResult Render(int width = 2560, int height = 1440)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            Vec3 camPos = new Vec3(0f, 0f, -3.3f);
            float aspect = (float)width / height;
            float scale = 1.0f;

            var spheres = new List<ISceneObject>
            {
                new Sphere(new Vec3(0f,-0.05f,0.9f),1.25f,new Vec3(0.15f,0.18f,0.95f)), // head

                new Sphere(new Vec3(-0.55f,0.55f,0.25f),0.55f,new Vec3(0.65f,0.95f,1f)), // left eye
                new Sphere(new Vec3( 0.55f,0.55f,0.25f),0.55f,new Vec3(0.65f,0.95f,1f)), // right eye

                new Sphere(new Vec3(-0.70f,0.65f,-0.05f),0.26f,new Vec3(0.55f,0.95f,0.15f)), // left pupil
                new Sphere(new Vec3( 0.70f,0.65f,-0.05f),0.26f,new Vec3(0.55f,0.95f,0.15f)), // right pupil

                new Sphere(new Vec3(0f,0.08f,-0.05f),0.38f,new Vec3(0.95f,0.15f,0.10f)), // nose
            };

            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = data.Stride;
                byte[] buffer = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        float px = (2f * ((x + 0.5f) / width) - 1f) * aspect * scale;
                        float py = (1f - 2f * ((y + 0.5f) / height)) * scale;

                        Vec3 dir = new Vec3(px, py, 1f).Normalized();
                        Ray ray = new Ray(camPos, dir);

                        Vec3 col;
                        if (!RayTracer.TraceClosest(ray, spheres, out Hit hit))
                        {
                            col = backgroundColor;
                        }
                        else
                        {
                            Vec3 s1 = (lightSource.Position - hit.Position).Normalized();
                            float ndotl1 = Vec3.Dot(s1, hit.Normal);
                            if (ndotl1 < 0f) ndotl1 = 0f;

                            Vec3 s2 = (lightSource2.Position - hit.Position).Normalized();
                            float ndotl2 = Vec3.Dot(s2, hit.Normal);
                            if (ndotl2 < 0f) ndotl2 = 0f;

                            // Diffuse contributions add up
                            Vec3 diffuse =
                                (hit.Color * lightSource.Color) * ndotl1 +
                                (hit.Color * lightSource2.Color) * ndotl2;

                            float ambient = 0.10f;
                            col = Vec3.Clamp(diffuse, 0f, 1f);
                        }

                        int i = row + x * 4;

                        buffer[i + 0] = (byte)(col.Z * 255); // B
                        buffer[i + 1] = (byte)(col.Y * 255); // G
                        buffer[i + 2] = (byte)(col.X * 255); // R
                        buffer[i + 3] = 255;                 // A
                    }
                }

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

        private static bool RaySphere(Ray ray, Vec3 center, float radius, out float tHit)
        {
            Vec3 oc = ray.Origin - center;

            float a = Vec3.Dot(ray.Direction, ray.Direction);
            float b = 2f * Vec3.Dot(oc, ray.Direction);
            float c = Vec3.Dot(oc, oc) - radius * radius;

            float disc = b * b - 4 * a * c;

            if (disc < 0) { tHit = 0; return false; }

            float s = MathF.Sqrt(disc);

            float t0 = (-b - s) / (2 * a);
            float t1 = (-b + s) / (2 * a);

            if (t0 > 0) { tHit = t0; return true; }
            if (t1 > 0) { tHit = t1; return true; }

            tHit = 0;
            return false;
        }
    }
}