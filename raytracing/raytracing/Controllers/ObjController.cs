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
    public class ObjController : Controller
    {
        private static readonly string ObjPath =
            @"C:\HSLU\Semester 6\RAYTRACING\assets2\source\r34nismo.obj";

        [HttpGet("/obj")]
        //public IActionResult Render(int width = 800, int height = 450)
        public IActionResult Render(int width = 2400, int height = 1350)
        {
            var groups = ObjLoader.LoadGrouped(ObjPath);

            // Bounding box from raw vertices
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var faces in groups.Values)
                foreach (var (A, B, C) in faces)
                    foreach (var v in new[] { A, B, C })
                    {
                        if (v.X < minX) minX = v.X;  if (v.X > maxX) maxX = v.X;
                        if (v.Y < minY) minY = v.Y;  if (v.Y > maxY) maxY = v.Y;
                        if (v.Z < minZ) minZ = v.Z;  if (v.Z > maxZ) maxZ = v.Z;
                    }

            float carW = maxX - minX;
            float carH = maxY - minY;
            float carL = maxZ - minZ;
            Vec3 center = new Vec3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);

            // ── Wheel-center detection from RIM centroids ─────────────────────
            var rimCentroids = new List<Vec3>();
            if (groups.ContainsKey("RIM"))
                foreach (var (rA, rB, rC) in groups["RIM"])
                    rimCentroids.Add(new Vec3((rA.X+rB.X+rC.X)/3f,(rA.Y+rB.Y+rC.Y)/3f,(rA.Z+rB.Z+rC.Z)/3f));

            var   wheelCenters = new Vec3[4];
            float tireRadius   = carH * 0.27f;
            float tireHalfW    = carW * 0.065f;
            bool  hasWheels    = false;

            if (rimCentroids.Count > 8)
            {
                rimCentroids.Sort((a, b) => a.X.CompareTo(b.X));
                float medX = rimCentroids[rimCentroids.Count / 2].X;

                var lRim = new List<Vec3>(); var rRim = new List<Vec3>();
                foreach (var c in rimCentroids) (c.X < medX ? lRim : rRim).Add(c);

                lRim.Sort((a, b) => a.Z.CompareTo(b.Z));
                rRim.Sort((a, b) => a.Z.CompareTo(b.Z));
                float mZL = lRim[lRim.Count / 2].Z, mZR = rRim[rRim.Count / 2].Z;

                Vec3 WheelCentroid(List<Vec3> pts)
                {
                    float sx = 0, sy = 0, sz = 0;
                    foreach (var p in pts) { sx += p.X; sy += p.Y; sz += p.Z; }
                    return new Vec3(sx / pts.Count, sy / pts.Count, sz / pts.Count);
                }

                var lF = new List<Vec3>(); var lRear = new List<Vec3>();
                var rF = new List<Vec3>(); var rRear = new List<Vec3>();
                foreach (var c in lRim) (c.Z >= mZL ? lF : lRear).Add(c);
                foreach (var c in rRim) (c.Z >= mZR ? rF : rRear).Add(c);

                if (lF.Count > 0 && lRear.Count > 0 && rF.Count > 0 && rRear.Count > 0)
                {
                    wheelCenters[0] = WheelCentroid(lF);
                    wheelCenters[1] = WheelCentroid(lRear);
                    wheelCenters[2] = WheelCentroid(rF);
                    wheelCenters[3] = WheelCentroid(rRear);

                    // Rim outer radius in YZ → scale up to include tire rubber
                    float maxRimR = 0f;
                    foreach (var rc in rimCentroids)
                    {
                        float best = float.MaxValue;
                        foreach (var wc2 in wheelCenters)
                        {
                            float dy = rc.Y - wc2.Y, dz = rc.Z - wc2.Z;
                            float d  = MathF.Sqrt(dy*dy + dz*dz);
                            if (d < best) best = d;
                        }
                        if (best > maxRimR) maxRimR = best;
                    }
                    tireRadius = maxRimR * 1.28f;

                    float lMinX = float.MaxValue, lMaxX = float.MinValue;
                    foreach (var c in lRim) { if (c.X < lMinX) lMinX = c.X; if (c.X > lMaxX) lMaxX = c.X; }
                    tireHalfW = (lMaxX - lMinX) * 0.75f;
                    hasWheels = true;
                }
            }

            bool IsTireFace(Vec3 fc)
            {
                if (!hasWheels) return false;
                foreach (var wc2 in wheelCenters)
                {
                    float dy = fc.Y - wc2.Y, dz = fc.Z - wc2.Z;
                    if (MathF.Sqrt(dy*dy + dz*dz) < tireRadius && MathF.Abs(fc.X - wc2.X) < tireHalfW)
                        return true;
                }
                return false;
            }
            // ─────────────────────────────────────────────────────────────────

            // Build triangles with per-material colors
            var triangles = new List<Triangle>();

            foreach (var (mat, faces) in groups)
            {
                if (faces.Count == 0) continue;

                Vec3  color;
                float ambient, diffuse, specular, shininess, refl;
                float transp = 0f, ior = 1f;

                switch (mat)
                {
                    case "GLASS":
                        color = new Vec3(0.05f, 0.08f, 0.10f);
                        ambient = 0.02f; diffuse = 0.02f; specular = 1.0f; shininess = 256f; refl = 0.15f;
                        transp = 0.85f; ior = 1.5f;
                        break;
                    case "CARBON":
                        color = new Vec3(0.13f, 0.44f, 0.93f);
                        ambient = 0.14f; diffuse = 0.85f; specular = 1.0f; shininess = 160f; refl = 0.2f;
                        break;
                    case "RIM":
                        color = new Vec3(0.05f, 0.05f, 0.05f);
                        ambient = 0.05f; diffuse = 0.4f; specular = 1.0f; shininess = 200f; refl = 0.3f;
                        break;
                    case "rearlight":
                        color = new Vec3(0.9f, 0.05f, 0.05f);
                        ambient = 0.5f; diffuse = 0.3f; specular = 0.8f; shininess = 64f; refl = 0.1f;
                        break;
                    case "Scene_-_Root":
                    {
                        Vec3 tireCol = new Vec3(0.03f, 0.03f, 0.03f);
                        Vec3 bodyCol = new Vec3(0.13f, 0.44f, 0.93f);
                        foreach (var (A, B, C) in faces)
                        {
                            Vec3 fc = new Vec3((A.X+B.X+C.X)/3f,(A.Y+B.Y+C.Y)/3f,(A.Z+B.Z+C.Z)/3f);
                            if (IsTireFace(fc))
                                triangles.Add(new Triangle(A, B, C, tireCol, 0.05f, 0.4f, 0.1f, 16f, 0f));
                            else
                                triangles.Add(new Triangle(A, B, C, bodyCol, 0.14f, 0.85f, 0.6f, 60f, 0.2f));
                        }
                        continue;
                    }
                    default: // PAINT = Bayside Blue body
                        color = new Vec3(0.13f, 0.44f, 0.93f);
                        ambient = 0.14f; diffuse = 0.85f; specular = 0.6f; shininess = 60f; refl = 0.2f;
                        break;
                }

                foreach (var (A, B, C) in faces)
                    triangles.Add(new Triangle(A, B, C, color, ambient, diffuse, specular, shininess, refl, transp, ior));
            }

            // Camera: low (headlight height), centered, close — matches reference shot
            Vec3 lookAt    = new Vec3(center.X + 0.5f - carW * 0.5f, minY + carH * 0.60f, maxZ - carL * 0.05f);
            Vec3 cameraPos = new Vec3(center.X - carW * 2.1f,        minY + carH * 0.44f, maxZ + carL * 0.62f);
            var  camera    = new Camera(cameraPos, lookAt, new Vec3(0f, 1f, 0f), 52f, (float)width / height);

            // Headlight emissive sphere positions — inner and outer per side
            float hlYhi = minY + carH * 0.47f;   // upper lamp row
            float hlYlo = minY + carH * 0.464f;   // lower lamp row
            float hlZ   = maxZ - carL * 0.042f;
            float hlZ2   = maxZ - carL * 0.055f;
            float hlSphR = carH * 0.04f;

            // Left side (inner = closer to centre, outer = further out, same height)
            Vec3 hl1InPos  = new Vec3(center.X - carW * 0.26f, hlYlo, hlZ);
            Vec3 hl1OutPos = new Vec3(center.X - carW * 0.33f, hlYhi, hlZ2);
            // Right side
            Vec3 hl2InPos  = new Vec3(center.X + carW * 0.26f, hlYlo, hlZ);
            Vec3 hl2OutPos = new Vec3(center.X + carW * 0.33f, hlYhi, hlZ2);

            Vec3 offset = new Vec3(0f, 0f, carL * 0.06f);
            Vec3 hl1InLight  = hl1InPos  + offset;
            Vec3 hl1OutLight = hl1OutPos + offset;
            Vec3 hl2InLight  = hl2InPos  + offset;
            Vec3 hl2OutLight = hl2OutPos + offset;

            // Taillight orbs (visual) — sit where the user positioned them on the car
            float tlY        = minY + carH * 0.62f;
            float tlOrbZ     = minZ + carL * 0.035f;
            Vec3  tlLeft     = new Vec3(center.X - carW * 0.34f, tlY, tlOrbZ);
            Vec3  tlRight    = new Vec3(center.X + carW * 0.34f, tlY, tlOrbZ);
            // Point lights pushed behind the car so shadow rays aren't blocked by the body
            float tlLightZ   = minZ + carL * 0.03f;
            Vec3  tlLightLeft  = new Vec3(center.X - carW * 0.34f, tlY, tlLightZ);
            Vec3  tlLightRight = new Vec3(center.X + carW * 0.34f, tlY, tlLightZ);
            Vec3  tlColor    = new Vec3(1.0f, 0.04f, 0.04f);

            // spotDir = direction the light shines; spotCos = cos of half-angle (-1 = omnidirectional)
            Vec3  hlDir    = new Vec3(0f, -0.15f, 1f).Normalized();  // forward + slight downward tilt
            float hlSpot   = MathF.Cos(MathF.PI / 180f * 35f);       // 35° half-cone
            Vec3  omni     = new Vec3(0f, 0f, 0f);                    // unused for omni lights

            // Ceiling lamp — point light and emissive sphere share the same position.
            // The sphere is transparent (Transparency=1), so IsInShadow walks straight through it.
            Vec3  ceilLampPos   = new Vec3(center.X, maxY + carH * 0.45f, center.Z);
            Vec3  ceilLampColor = new Vec3(0.4f, 0.6f, 1.0f); // cool blue

            Vec3 hlColor = new Vec3(0.88f, 0.94f, 1.0f);
            var lights = new List<(Vec3 pos, Vec3 color, float intensity, Vec3 spotDir, float spotCos)>
            {
                (hl1InLight,    hlColor,       5.0f, hlDir,       hlSpot),     // left inner headlight
                (hl1OutLight,   hlColor,       5.0f, hlDir,       hlSpot),     // left outer headlight
                (hl2InLight,    hlColor,       5.0f, hlDir,       hlSpot),     // right inner headlight
                (hl2OutLight,   hlColor,       5.0f, hlDir,       hlSpot),     // right outer headlight
                (tlLightLeft,   tlColor,       2.0f, omni,        -1f),        // left taillight (omni)
                (tlLightRight,  tlColor,       2.0f, omni,        -1f),        // right taillight (omni)


                (ceilLampPos,      ceilLampColor, 5.0f, omni, -1f),              // ceiling lamp (omni)
            };

            // BVH over all car triangles
            var bvh = BVHNode.Build(new List<ISceneObject>(triangles));

            var objects = new List<ISceneObject> { bvh };

            // Dark studio floor
            objects.Add(new Plane(
                new Vec3(0f, minY - 0.001f, 0f), new Vec3(0f, 1f, 0f),
                new Vec3(0.07f, 0.07f, 0.09f),
                ambient: 0.04f, diffuse: 0.4f, specular: 0.04f, shininess: 8f));

            // Mirror wall behind the car — vertical plane, faces +Z toward the camera
            objects.Add(new Plane(
                new Vec3(center.X, center.Y, minZ - carL * 0.35f), new Vec3(0f, 0f, 1f),
                new Vec3(0.02f, 0.02f, 0.02f),
                ambient: 0f, diffuse: 0f, specular: 0f, shininess: 1f, reflectivity: 0.88f));

            // Headlight emissive orbs — inner and outer per side
            Vec3 hlOrb = new Vec3(1.0f, 0.97f, 0.90f);
            objects.Add(new Sphere(hl1InPos,  hlSphR, hlOrb, ambient: 1.0f, diffuse: 0f, specular: 0f));
            objects.Add(new Sphere(hl1OutPos, hlSphR, hlOrb, ambient: 1.0f, diffuse: 0f, specular: 0f));
            objects.Add(new Sphere(hl2InPos,  hlSphR, hlOrb, ambient: 1.0f, diffuse: 0f, specular: 0f));
            objects.Add(new Sphere(hl2OutPos, hlSphR, hlOrb, ambient: 1.0f, diffuse: 0f, specular: 0f));

            // Taillight emissive orbs (red, rear face = minZ)
            float tlSphR = carH * 0.038f;
            objects.Add(new Sphere(tlLeft,  tlSphR, tlColor, ambient: 1.0f, diffuse: 0f, specular: 0f, transparency: 1.0f));
            objects.Add(new Sphere(tlRight, tlSphR, tlColor, ambient: 1.0f, diffuse: 0f, specular: 0f, transparency: 1.0f));

            // Ceiling lamp emissive bulb
            objects.Add(new Sphere(ceilLampPos, carH * 0.04f, ceilLampColor, ambient: 1.0f, diffuse: 0f, specular: 0f, transparency: 1.0f)); // ceiling lamp bulb

            // Garage box walls and ceiling (floor + mirror back wall already added above)
            Vec3  wallCol   = new Vec3(0.92f, 0.92f, 0.92f);
            Vec3  groutCol  = new Vec3(0.55f, 0.55f, 0.55f);
            float tileSize  = carH * 0.18f;   // tile roughly 18% of car height
            objects.Add(new Plane(  // right wall
                new Vec3(maxX + carW * 0.6f, center.Y, center.Z), new Vec3(-1f, 0f, 0f),
                wallCol, ambient: 0.06f, diffuse: 0.5f, specular: 0.05f, shininess: 8f,
                colorB: groutCol, tileSize: tileSize));
            objects.Add(new Plane(  // left wall
                new Vec3(minX - carW * 2.3f, center.Y, center.Z), new Vec3(1f, 0f, 0f),
                wallCol, ambient: 0.06f, diffuse: 0.5f, specular: 0.05f, shininess: 8f,
                colorB: groutCol, tileSize: tileSize));
            objects.Add(new Plane(  // ceiling
                new Vec3(center.X, maxY + carH * 0.5f, center.Z), new Vec3(0f, -1f, 0f),
                wallCol, ambient: 0.06f, diffuse: 0.5f, specular: 0.05f, shininess: 8f,
                colorB: groutCol, tileSize: tileSize));

            using var bmp  = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var  rect      = new Rectangle(0, 0, width, height);
            var  data      = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int    stride = data.Stride;
                byte[] buffer = new byte[stride * height];

                Parallel.For(0, height, y =>
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        Ray  ray    = camera.GetRay(x, y, width, height);
                        bool didHit = RayTracer.TraceClosest(ray, objects, out Hit hit);
                        Vec3 col;

                        if (!didHit)
                        {
                            // Background: near-black
                            float lx = Math.Clamp(-ray.Direction.X * 1.1f + 0.05f, 0f, 1f);
                            float uy = Math.Clamp( ray.Direction.Y * 1.4f + 0.35f, 0f, 1f);
                            col = Vec3.Lerp(
                                new Vec3(0.008f, 0.005f, 0.012f),
                                new Vec3(0.38f,  0.04f,  0.20f),
                                lx * uy * 0.9f);
                        }
                        else
                        {
                            col = hit.Color * hit.Ambient;

                            foreach (var (lPos, lColor, lIntensity, lSpotDir, lSpotCos) in lights)
                            {
                                if (RayTracer.IsInShadow(hit.Position, lPos, objects)) continue;

                                float spotFactor = 1f;
                                if (lSpotCos > -1f)
                                {
                                    Vec3  toHit    = (hit.Position - lPos).Normalized();
                                    float cosAngle = Vec3.Dot(toHit, lSpotDir);
                                    if (cosAngle < lSpotCos) continue;
                                    spotFactor = MathF.Pow((cosAngle - lSpotCos) / (1f - lSpotCos), 2f);
                                }

                                Vec3  lightDir = (lPos - hit.Position).Normalized();
                                Vec3  viewDir  = (cameraPos - hit.Position).Normalized();
                                float ndotl    = MathF.Max(0f, Vec3.Dot(hit.Normal, lightDir));
                                Vec3  halfDir  = (lightDir + viewDir).Normalized();
                                float spec     = hit.Specular * lIntensity * spotFactor *
                                    MathF.Pow(MathF.Max(0f, Vec3.Dot(hit.Normal, halfDir)), hit.Shininess);

                                col = col + hit.Color * lColor * (ndotl * hit.Diffuse * lIntensity * spotFactor)
                                          + lColor * spec;
                            }

                            // Single-bounce reflection
                            if (hit.Reflectivity > 0.01f)
                            {
                                float nDotD  = Vec3.Dot(hit.Normal, ray.Direction);
                                Vec3  reflDir = (ray.Direction - hit.Normal * (2f * nDotD)).Normalized();
                                Ray   reflRay = new Ray(hit.Position + hit.Normal * 0.001f, reflDir);

                                Vec3 reflCol = new Vec3(0f, 0f, 0f);
                                if (RayTracer.TraceClosest(reflRay, objects, out Hit rh))
                                {
                                    reflCol = rh.Color * rh.Ambient;
                                    Vec3 reflView = reflDir * -1f;
                                    foreach (var (lPos, lColor, lIntensity, lSpotDir, lSpotCos) in lights)
                                    {
                                        if (RayTracer.IsInShadow(rh.Position, lPos, objects)) continue;
                                        float sf = 1f;
                                        if (lSpotCos > -1f)
                                        {
                                            Vec3  th = (rh.Position - lPos).Normalized();
                                            float ca = Vec3.Dot(th, lSpotDir);
                                            if (ca < lSpotCos) continue;
                                            sf = MathF.Pow((ca - lSpotCos) / (1f - lSpotCos), 2f);
                                        }
                                        Vec3  ld    = (lPos - rh.Position).Normalized();
                                        float ndotl = MathF.Max(0f, Vec3.Dot(rh.Normal, ld));
                                        Vec3  hd    = (ld + reflView).Normalized();
                                        float sp    = rh.Specular * lIntensity * sf *
                                            MathF.Pow(MathF.Max(0f, Vec3.Dot(rh.Normal, hd)), rh.Shininess);
                                        reflCol = reflCol + rh.Color * lColor * (ndotl * rh.Diffuse * lIntensity * sf)
                                                          + lColor * sp;
                                    }
                                    reflCol = Vec3.Clamp(reflCol, 0f, 1f);
                                }
                                col = Vec3.Lerp(col, reflCol, hit.Reflectivity);
                            }

                            // Single-bounce refraction (Snell's law)
                            if (hit.Transparency > 0.01f)
                            {
                                float nDotD = Vec3.Dot(hit.Normal, ray.Direction);
                                Vec3  rn;
                                float eta;

                                if (nDotD < 0f)
                                {
                                    rn  = hit.Normal;           // entering: air → glass
                                    eta = 1.0f / hit.IOR;
                                }
                                else
                                {
                                    rn  = hit.Normal * -1f;     // exiting: glass → air
                                    eta = hit.IOR;
                                    nDotD = -nDotD;
                                }

                                float cosI = -nDotD;
                                float k    = 1.0f - eta * eta * (1.0f - cosI * cosI);

                                if (k >= 0f)
                                {
                                    Vec3 refractDir = (ray.Direction * eta + rn * (eta * cosI - MathF.Sqrt(k))).Normalized();
                                    Ray  refractRay = new Ray(hit.Position - rn * 0.001f, refractDir);

                                    Vec3 refractCol = new Vec3(0f, 0f, 0f);
                                    if (RayTracer.TraceClosest(refractRay, objects, out Hit rth))
                                    {
                                        refractCol = rth.Color * rth.Ambient;
                                        Vec3 refractView = refractDir * -1f;
                                        foreach (var (lPos, lColor, lIntensity, lSpotDir, lSpotCos) in lights)
                                        {
                                            if (RayTracer.IsInShadow(rth.Position, lPos, objects)) continue;
                                            float sf = 1f;
                                            if (lSpotCos > -1f)
                                            {
                                                Vec3  th = (rth.Position - lPos).Normalized();
                                                float ca = Vec3.Dot(th, lSpotDir);
                                                if (ca < lSpotCos) continue;
                                                sf = MathF.Pow((ca - lSpotCos) / (1f - lSpotCos), 2f);
                                            }
                                            Vec3  ld     = (lPos - rth.Position).Normalized();
                                            float ndotlr = MathF.Max(0f, Vec3.Dot(rth.Normal, ld));
                                            Vec3  hdr    = (ld + refractView).Normalized();
                                            float spr    = rth.Specular * lIntensity * sf *
                                                MathF.Pow(MathF.Max(0f, Vec3.Dot(rth.Normal, hdr)), rth.Shininess);
                                            refractCol = refractCol + rth.Color * lColor * (ndotlr * rth.Diffuse * lIntensity * sf)
                                                                    + lColor * spr;
                                        }
                                        refractCol = Vec3.Clamp(refractCol, 0f, 1f);
                                    }
                                    else
                                    {
                                        float lx = Math.Clamp(-refractDir.X * 1.1f + 0.05f, 0f, 1f);
                                        float uy = Math.Clamp( refractDir.Y * 1.4f + 0.35f, 0f, 1f);
                                        refractCol = Vec3.Lerp(
                                            new Vec3(0.008f, 0.005f, 0.012f),
                                            new Vec3(0.38f,  0.04f,  0.20f),
                                            lx * uy * 0.9f);
                                    }

                                    col = Vec3.Lerp(col, refractCol, hit.Transparency);
                                }
                            }

                            col = Vec3.Clamp(col, 0f, 1f);
                        }

                        int i = row + x * 4;
                        buffer[i + 0] = (byte)(col.Z * 255);
                        buffer[i + 1] = (byte)(col.Y * 255);
                        buffer[i + 2] = (byte)(col.X * 255);
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

    }
}
