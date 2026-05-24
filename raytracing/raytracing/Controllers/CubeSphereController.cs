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
    public class CubeSphereController : Controller
    {
        [HttpGet("/cube-sphere")]
        public IActionResult Render(int width = 1920, int height = 1080)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            float aspect = (float)width / height;

            Camera camera = new Camera(
                position: new Vec3(0f, 1.2f, -8f),
                lookAt: new Vec3(0f, 0f, 3.5f),
                worldUp: new Vec3(0f, 1f, 0f),
                fovDegrees: 90f,
                aspectRatio: (float)width / height
            );

            Vec3 lightPos = new Vec3(2f, 3f, 4f);
            Vec3 lightColor = new Vec3(1f, 1f, 1f);

            Vec3 backgroundColor = new Vec3(0f, 0f, 0f);

            var objects = new List<ISceneObject>
            {
                new Sphere(
                    new Vec3(0.0f, -0.4f, 4.2f),
                    1.8f,
                    new Vec3(0.2f, 0.6f, 1.0f),
                    ambient: 0.1f,
                    diffuse: 0.9f,
                    specular: 0.8f,
                    shininess: 64f)
            };

            objects.AddRange(CreateCube(new Vec3(-2.2f, 3f, 0.0f), 1.3f, new Vec3(1.0f, 0.25f, 0.25f)));

            objects.Add(
                new Plane(
                    new Vec3(0f, -2.2f, 0f),
                    new Vec3(0f, 1f, 0f),
                    new Vec3(0.7f, 0.7f, 0.7f),
                    ambient: 0.1f,
                    diffuse: 0.9f,
                    specular: 0.1f,
                    shininess: 8f)
            );

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

                        Vec3 col;

                        if (!RayTracer.TraceClosest(ray, objects, out Hit hit))
                        {
                            col = backgroundColor;
                        }
                        else
                        {
                            Vec3 lightDir = (lightPos - hit.Position).Normalized();
                            Vec3 viewDir = (camera.Position - hit.Position).Normalized();

                            bool inShadow = RayTracer.IsInShadow(hit.Position, lightPos, objects);

                            float ambient = hit.Ambient;
                            float diffuse = 0f;
                            float specular = 0f;

                            if (!inShadow)
                            {
                                diffuse = hit.Diffuse * MathF.Max(0f, Vec3.Dot(hit.Normal, lightDir));

                                Vec3 halfDir = (lightDir + viewDir).Normalized();
                                specular = hit.Specular * MathF.Pow(
                                    MathF.Max(0f, Vec3.Dot(hit.Normal, halfDir)),
                                    hit.Shininess
                                );
                            }

                            Vec3 ambientTerm = hit.Color * ambient;
                            Vec3 diffuseTerm = (hit.Color * lightColor) * diffuse;
                            Vec3 specularTerm = lightColor * specular;

                            col = ambientTerm + diffuseTerm + specularTerm;
                            col = Vec3.Clamp(col, 0f, 1f);
                        }

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

        // Creates cube from 12 triangles
        private static List<ISceneObject> CreateCube(Vec3 center, float size, Vec3 color)
        {
            float s = size / 2f;

            Vec3[] v =
            {
                center + new Vec3(-s,-s,-s),
                center + new Vec3( s,-s,-s),
                center + new Vec3( s, s,-s),
                center + new Vec3(-s, s,-s),
                center + new Vec3(-s,-s, s),
                center + new Vec3( s,-s, s),
                center + new Vec3( s, s, s),
                center + new Vec3(-s, s, s)
            };

            return new List<ISceneObject>
            {
                // front
                new Triangle(v[4],v[5],v[6],color),
                new Triangle(v[4],v[6],v[7],color),

                // back
                new Triangle(v[0],v[2],v[1],color),
                new Triangle(v[0],v[3],v[2],color),

                // left
                new Triangle(v[0],v[7],v[3],color),
                new Triangle(v[0],v[4],v[7],color),

                // right
                new Triangle(v[1],v[2],v[6],color),
                new Triangle(v[1],v[6],v[5],color),

                // top
                new Triangle(v[3],v[7],v[6],color),
                new Triangle(v[3],v[6],v[2],color),

                // bottom
                new Triangle(v[0],v[1],v[5],color),
                new Triangle(v[0],v[5],v[4],color)
            };
        }
    }
}
