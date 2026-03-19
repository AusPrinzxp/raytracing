using Microsoft.AspNetCore.Mvc;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace raytracing.Controllers
{
    public class RandomImageController : Controller
    {
        [HttpGet("/random-image")]
        public IActionResult GetRandomImage(int width = 800, int height = 600)
        {
            using var bmp = new Bitmap(width, height);
            Random rand = new Random();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color randomColor = Color.FromArgb(
                        255,
                        rand.Next(256),
                        rand.Next(256),
                        rand.Next(256));

                    bmp.SetPixel(x, y, randomColor);
                }
            }

            using MemoryStream ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            return File(ms.ToArray(), "image/png");
        }
    }
}

