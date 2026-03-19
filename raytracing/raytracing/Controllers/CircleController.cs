using Microsoft.AspNetCore.Mvc;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace raytracing.Controllers
{
    public class CircleController : Controller
    {
        [HttpGet("/circle")]
        public IActionResult GetRasterCircle(
            int width = 2560,
            int height = 1440,
            int radius = 150)
        {
            using Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            int cx = width / 2;
            int cy = height / 2;
            int r2 = radius * radius;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;

                    // Kreisgleichung
                    if (dx * dx + dy * dy <= r2)
                    {
                        bmp.SetPixel(x, y, Color.White);   // innerhalb Kreis
                    }
                    else
                    {
                        bmp.SetPixel(x, y, Color.Black);   // Hintergrund
                    }
                }
            }

            using MemoryStream ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            return File(ms.ToArray(), "image/png");
        }
    }
}
