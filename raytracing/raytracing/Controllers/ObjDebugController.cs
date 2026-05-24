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
    // Renders each OBJ material group in a unique color so you can identify which material is the windows.
    // Hit /obj-debug and look at what color the windows are, then check the console output for the material name.
    public class ObjDebugController : Controller
    {
        private static readonly string ObjPath =
            @"C:\HSLU\Semester 6\RAYTRACING\assets2\source\r34nismo.obj";

        // Distinct colors for up to 20 material groups
        private static readonly Vec3[] DebugColors =
        {
            new Vec3(1f, 0.1f, 0.1f),   // 0  red
            new Vec3(0.1f, 1f, 0.1f),   // 1  green
            new Vec3(1f, 1f, 0.1f),     // 2  yellow
            new Vec3(1f, 0.1f, 1f),     // 3  magenta
            new Vec3(0.1f, 1f, 1f),     // 4  cyan
            new Vec3(1f, 0.5f, 0.1f),   // 5  orange
            new Vec3(0.5f, 0.1f, 1f),   // 6  purple
            new Vec3(0.1f, 0.5f, 1f),   // 7  sky blue
            new Vec3(1f, 0.5f, 0.5f),   // 8  pink
            new Vec3(0.5f, 1f, 0.1f),   // 9  lime
            new Vec3(0.1f, 0.5f, 0.5f), // 10 teal
            new Vec3(0.8f, 0.6f, 0.1f), // 11 gold
            new Vec3(1f, 1f, 1f),       // 12 white
            new Vec3(0.5f, 0.5f, 0.5f), // 13 gray
            new Vec3(0.5f, 0.1f, 0.1f), // 14 dark red
            new Vec3(0.1f, 0.5f, 0.1f), // 15 dark green
            new Vec3(0.6f, 0.4f, 0.2f), // 16 brown
            new Vec3(0.8f, 0.8f, 0.4f), // 17 sand
            new Vec3(0.4f, 0.1f, 0.6f), // 18 violet
            new Vec3(0.1f, 0.8f, 0.5f), // 19 mint
        };

        [HttpGet("/obj-debug")]
        public IActionResult Render(int width = 800, int height = 450)
        {
            var groups = ObjLoader.LoadGrouped(ObjPath);

            var triangles = new List<Triangle>();
            int idx = 0;

            // Print material mapping to console for identification
            Console.WriteLine("=== OBJ Material Groups ===");
            foreach (var (mat, faces) in groups)
            {
                Vec3 col = DebugColors[idx % DebugColors.Length];
                Console.WriteLine($"  [{idx}] {mat,20}  faces={faces.Count,5}  color=({col.X:F1},{col.Y:F1},{col.Z:F1})");

                foreach (var (A, B, C) in faces)
                    triangles.Add(new Triangle(A, B, C, col, ambient: 1.0f, diffuse: 0f, specular: 0f));

                idx++;
            }
            Console.WriteLine("===========================");

            // Bounding box
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var tri in triangles)
                foreach (var v in new[] { tri.A, tri.B, tri.C })
                {
                    if (v.X < minX) minX = v.X;  if (v.X > maxX) maxX = v.X;
                    if (v.Y < minY) minY = v.Y;  if (v.Y > maxY) maxY = v.Y;
                    if (v.Z < minZ) minZ = v.Z;  if (v.Z > maxZ) maxZ = v.Z;
                }

            float carW = maxX - minX, carH = maxY - minY, carL = maxZ - minZ;
            Vec3 center = new Vec3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);

            Vec3 lookAt    = new Vec3(center.X, minY + carH * 0.40f, maxZ - carL * 0.05f);
            Vec3 cameraPos = new Vec3(center.X - carW * 0.45f, minY + carH * 0.24f, maxZ + carL * 0.62f);
            var  camera    = new Camera(cameraPos, lookAt, new Vec3(0f, 1f, 0f), 52f, (float)width / height);

            var bvh     = BVHNode.Build(new List<ISceneObject>(triangles));
            var objects = new List<ISceneObject> { bvh };

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
                        Vec3 col = new Vec3(0.05f, 0.05f, 0.05f);

                        if (RayTracer.TraceClosest(ray, objects, out Hit hit))
                            col = hit.Color;

                        int i = row + x * 4;
                        buffer[i + 0] = (byte)(Math.Clamp(col.Z, 0f, 1f) * 255);
                        buffer[i + 1] = (byte)(Math.Clamp(col.Y, 0f, 1f) * 255);
                        buffer[i + 2] = (byte)(Math.Clamp(col.X, 0f, 1f) * 255);
                        buffer[i + 3] = 255;
                    }
                });

                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally { bmp.UnlockBits(data); }

            // Also return the color legend as a response header
            idx = 0;
            foreach (var (mat, faces) in groups)
            {
                Vec3 col = DebugColors[idx % DebugColors.Length];
                Response.Headers.Append($"X-Mat-{idx}", $"{mat} ({faces.Count} faces)");
                idx++;
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return File(ms.ToArray(), "image/png");
        }
    }
}
