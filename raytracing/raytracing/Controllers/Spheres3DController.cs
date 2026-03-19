using Microsoft.AspNetCore.Mvc;
using raytracing.Model;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace raytracing.Controllers
{
    public class Spheres3DController : Controller
    {
        // Example:
        // https://localhost:7143/spheres-3d?width=900&height=600&outerR=1&innerR=0.65&innerOffset=0.55
        [HttpGet("/spheres-3d")]
        public IActionResult Render(
            int width = 900,
            int height = 600,
            float outerR = 1.0f,
            float innerR = 0.65f,
            float innerOffset = 0.55f,
            float fovDegrees = 55f)
        {
            if (width <= 0 || height <= 0) return BadRequest("width/height must be > 0");
            if (outerR <= 0 || innerR <= 0) return BadRequest("outerR/innerR must be > 0");

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            // Camera
            Vec3 camPos = new Vec3(0f, 0f, -3.2f);
            float aspect = (float)width / height;
            float fovRad = fovDegrees * (MathF.PI / 180f);
            float scale = MathF.Tan(fovRad * 0.5f);

            // Scene spheres (outer at origin, inner shifted so it "pokes out")
            Vec3 outerC = Vec3.Zero;
            Vec3 innerC = new Vec3(innerOffset, 0f, 0f);

            // Light
            Vec3 lightDir = new Vec3(-0.6f, 0.9f, -0.4f).Normalized(); // directional light

            // Colors (linear 0..1)
            Vec3 outerBase = new Vec3(0.9f, 0.9f, 0.95f); // pale
            Vec3 innerBase = new Vec3(0.15f, 0.7f, 1.0f); // bluish (change if you want)

            var rect = new Rectangle(0, 0, width, height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = data.Stride;
                int bytes = stride * height;
                byte[] buffer = new byte[bytes];

                // Opaque black background
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        buffer[row + x * 4 + 3] = 255; // A
                    }
                }

                for (int y = 0; y < height; y++)
                {
                    float py = (1f - 2f * ((y + 0.5f) / height)) * scale; // flip Y
                    int row = y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        float px = (2f * ((x + 0.5f) / width) - 1f) * scale * aspect;

                        // Ray through pixel in camera space (z forward)
                        Vec3 dir = new Vec3(px, py, 1f).Normalized();
                        Ray ray = new Ray(camPos, dir);

                        // Intersect both spheres, pick closest hit
                        bool hitOuter = RaySphere(ray, outerC, outerR, out float tOuter);
                        bool hitInner = RaySphere(ray, innerC, innerR, out float tInner);

                        if (!hitOuter && !hitInner)
                            continue;

                        bool useInner;
                        float tHit;

                        if (hitOuter && hitInner)
                        {
                            useInner = tInner < tOuter;
                            tHit = useInner ? tInner : tOuter;
                        }
                        else if (hitInner)
                        {
                            useInner = true;
                            tHit = tInner;
                        }
                        else
                        {
                            useInner = false;
                            tHit = tOuter;
                        }

                        Vec3 hitPos = ray.At(tHit);
                        Vec3 normal = (useInner ? (hitPos - innerC) : (hitPos - outerC)).Normalized();

                        // Simple Lambert + small ambient
                        float ndotl = MathF.Max(0f, Vec3.Dot(normal, -lightDir));
                        float ambient = 0.12f;

                        Vec3 baseCol = useInner ? innerBase : outerBase;
                        Vec3 col = baseCol * (ambient + 0.95f * ndotl);

                        // Optional: a little rim-light for depth
                        float rim = MathF.Pow(1f - MathF.Max(0f, Vec3.Dot(normal, -ray.Direction)), 2.2f);
                        col = col + new Vec3(0.15f, 0.15f, 0.15f) * rim;

                        col = Vec3.Clamp(col, 0f, 1f);

                        // Write BGRA (gamma to sRGB-ish)
                        int i = row + x * 4;
                        buffer[i + 0] = ToByteSRGB(col.Z); // B
                        buffer[i + 1] = ToByteSRGB(col.Y); // G
                        buffer[i + 2] = ToByteSRGB(col.X); // R
                        buffer[i + 3] = 255;
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

        // Ray-sphere intersection: returns nearest positive t
        private static bool RaySphere(Ray ray, Vec3 center, float radius, out float tHit)
        {
            Vec3 oc = ray.Origin - center;
            float a = Vec3.Dot(ray.Direction, ray.Direction);      // ~1 if normalized
            float b = 2f * Vec3.Dot(oc, ray.Direction);
            float c = Vec3.Dot(oc, oc) - radius * radius;

            float disc = b * b - 4f * a * c;
            if (disc < 0f) { tHit = 0f; return false; }

            float s = MathF.Sqrt(disc);
            float t0 = (-b - s) / (2f * a);
            float t1 = (-b + s) / (2f * a);

            const float eps = 0.0001f;
            if (t0 > eps) { tHit = t0; return true; }
            if (t1 > eps) { tHit = t1; return true; }

            tHit = 0f;
            return false;
        }

        // Simple gamma (approx sRGB). Input 0..1
        private static byte ToByteSRGB(float v)
        {
            v = MathF.Min(MathF.Max(v, 0f), 1f);
            v = MathF.Pow(v, 1f / 2.2f);
            return (byte)(v * 255f + 0.5f);
        }
    }
}
