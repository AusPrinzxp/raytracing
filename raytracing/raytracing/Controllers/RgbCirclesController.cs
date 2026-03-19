using Microsoft.AspNetCore.Mvc;
using raytracing.Model;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace raytracing.Controllers
{
    public class RgbCirclesController : Controller
    {
        // Example:
        // https://localhost:7143/rgb-circles?width=800&height=600&radius=140&spacing=160
        [HttpGet("/rgb-circles")]
        public IActionResult GetRgbCircles(int width = 1920, int height = 1080, int radius = 300, int spacing = 200)
        {
            if (width <= 0 || height <= 0) return BadRequest("width/height must be > 0");
            if (radius <= 0) return BadRequest("radius must be > 0");

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            Vec2i center = new Vec2i(width / 2, height / 2);

            // Triangle layout (top, bottom-left, bottom-right)
            Vec2i cR = center + new Vec2i(0, -spacing);
            Vec2i cG = center + new Vec2i(-spacing, spacing / 2);
            Vec2i cB = center + new Vec2i(spacing, spacing / 2);

            int r2 = radius * radius;

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
                        int i = row + x * 4;
                        buffer[i + 3] = 255; // A
                    }
                }

                // Rasterize with additive RGB overlaps
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        Vec2i p = new Vec2i(x, y);

                        bool inR = InsideCircle(p, cR, r2);
                        bool inG = InsideCircle(p, cG, r2);
                        bool inB = InsideCircle(p, cB, r2);

                        if (!(inR | inG | inB)) continue;

                        int i = row + x * 4; // BGRA

                        if (inR) buffer[i + 2] = SatAdd(buffer[i + 2], 255); // R
                        if (inG) buffer[i + 1] = SatAdd(buffer[i + 1], 255); // G
                        if (inB) buffer[i + 0] = SatAdd(buffer[i + 0], 255); // B
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

        private static bool InsideCircle(Vec2i p, Vec2i center, int radiusSquared)
        {
            Vec2i d = p - center;
            return d.LengthSquared() <= radiusSquared;
        }

        private static byte SatAdd(byte a, int b)
        {
            int sum = a + b;
            return (byte)(sum > 255 ? 255 : sum);
        }
    }
}