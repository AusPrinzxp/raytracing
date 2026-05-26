using Microsoft.AspNetCore.Mvc;
using raytracing.Model;
using Svg;
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
        //public IActionResult Render(int width = 2400, int height = 1350)
        public IActionResult Render(int width = 1920, int height = 1080)
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
            var  camera    = new Camera(cameraPos, lookAt, new Vec3(0f, 1f, 0f), 38f, (float)width / height);

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
                (tlLightLeft,   tlColor,       1.0f, omni,        -1f),        // left taillight (omni)
                (tlLightRight,  tlColor,       1.0f, omni,        -1f),        // right taillight (omni)


                //(ceilLampPos,      ceilLampColor, 5.0f, omni, -1f),              // ceiling lamp (omni)
            };

            // ── Toolbox prop ──────────────────────────────────────────────────
            void AddBox(Vec3 lo, Vec3 hi, Vec3 col, float amb, float dif, float spec, float shin, float refl = 0f)
            {
                void T(Vec3 a, Vec3 b, Vec3 c) => triangles.Add(new Triangle(a, b, c, col, amb, dif, spec, shin, refl));
                Vec3 lll = new Vec3(lo.X, lo.Y, lo.Z), hll = new Vec3(hi.X, lo.Y, lo.Z);
                Vec3 hlh = new Vec3(hi.X, lo.Y, hi.Z), llh = new Vec3(lo.X, lo.Y, hi.Z);
                Vec3 lhl = new Vec3(lo.X, hi.Y, lo.Z), hhl = new Vec3(hi.X, hi.Y, lo.Z);
                Vec3 hhh = new Vec3(hi.X, hi.Y, hi.Z), lhh = new Vec3(lo.X, hi.Y, hi.Z);
                T(lll, hll, hlh); T(lll, hlh, llh); // bottom
                T(lhh, hhh, hhl); T(lhh, hhl, lhl); // top
                T(llh, hlh, hhh); T(llh, hhh, lhh); // front
                T(hll, lll, lhl); T(hll, lhl, hhl); // back
                T(lll, llh, lhh); T(lll, lhh, lhl); // left
                T(hlh, hll, hhl); T(hlh, hhl, hhh); // right
            }

            {
                // Place against the right wall, near the front of the car
                float tbW  = carW * 0.13f;
                float tbH  = carH * 0.20f;
                float tbD  = carW * 0.09f;
                float tbHi = maxX + carW * 0.57f;  // flush against right wall
                float tbLo = tbHi - tbW;
                float tbZ0 = maxZ - carL * 0.15f;
                float tbZ1 = tbZ0 + tbD;
                float tbY0 = minY;
                float tbY1 = tbY0 + tbH;
                float tbLid = tbY1 - carH * 0.025f; // thin lid strip at top

                Vec3 boxRed  = new Vec3(0.72f, 0.07f, 0.07f);
                Vec3 boxDark = new Vec3(0.18f, 0.18f, 0.20f);

                // Main body
                AddBox(new Vec3(tbLo, tbY0, tbZ0), new Vec3(tbHi, tbLid, tbZ1),
                    boxRed, 0.08f, 0.7f, 0.9f, 64f, 0.15f);
                // Lid
                AddBox(new Vec3(tbLo, tbLid, tbZ0), new Vec3(tbHi, tbY1, tbZ1),
                    boxRed, 0.10f, 0.7f, 1.0f, 128f, 0.2f);
                // Drawer strip
                float dsH = tbH * 0.08f;
                float dsY0 = tbY0 + tbH * 0.35f;
                AddBox(new Vec3(tbLo - 0.001f, dsY0, tbZ0 - 0.001f), new Vec3(tbHi + 0.001f, dsY0 + dsH, tbZ1 + 0.001f),
                    boxDark, 0.05f, 0.5f, 0.8f, 96f, 0.1f);
                dsY0 = tbY0 + tbH * 0.55f;
                AddBox(new Vec3(tbLo - 0.001f, dsY0, tbZ0 - 0.001f), new Vec3(tbHi + 0.001f, dsY0 + dsH, tbZ1 + 0.001f),
                    boxDark, 0.05f, 0.5f, 0.8f, 96f, 0.1f);
            }

            // ── Workbench prop ────────────────────────────────────────────────
            {
                float wbHiX  = maxX + carW * 0.6f;       // flush against right wall
                float wbDepX = carW * 0.18f;
                float wbLoX  = wbHiX - wbDepX;
                float wbZ1   = maxZ - carL * 0.16f;      // front edge
                float wbZ0   = maxZ - carL * 0.54f;      // back edge — long bench
                float wbH    = carH * 0.40f;
                float topT   = carH * 0.030f;
                float legW   = carW * 0.016f;
                float shelfY = minY + wbH * 0.38f;
                float shelfT = carH * 0.020f;

                Vec3 wbWood  = new Vec3(0.52f, 0.36f, 0.16f);
                Vec3 wbMetal = new Vec3(0.20f, 0.20f, 0.22f);

                // Table top
                AddBox(new Vec3(wbLoX, minY + wbH, wbZ0),
                       new Vec3(wbHiX, minY + wbH + topT, wbZ1),
                       wbWood, 0.08f, 0.70f, 0.55f, 48f, 0.06f);

                // Lower shelf
                AddBox(new Vec3(wbLoX, shelfY, wbZ0),
                       new Vec3(wbHiX, shelfY + shelfT, wbZ1),
                       wbWood, 0.06f, 0.60f, 0.30f, 16f);

                // Four legs
                AddBox(new Vec3(wbLoX,        minY, wbZ0),         new Vec3(wbLoX + legW, minY + wbH, wbZ0 + legW), wbMetal, 0.06f, 0.5f, 0.8f, 64f);
                AddBox(new Vec3(wbLoX,        minY, wbZ1 - legW),   new Vec3(wbLoX + legW, minY + wbH, wbZ1),        wbMetal, 0.06f, 0.5f, 0.8f, 64f);
                AddBox(new Vec3(wbHiX - legW, minY, wbZ0),         new Vec3(wbHiX,        minY + wbH, wbZ0 + legW), wbMetal, 0.06f, 0.5f, 0.8f, 64f);
                AddBox(new Vec3(wbHiX - legW, minY, wbZ1 - legW),   new Vec3(wbHiX,        minY + wbH, wbZ1),        wbMetal, 0.06f, 0.5f, 0.8f, 64f);
            }
            // ── Second workbench (front side of toolbox) ─────────────────────
            {
                float wbHiX  = maxX + carW * 0.6f;
                float wbDepX = carW * 0.18f;
                float wbLoX  = wbHiX - wbDepX;
                float wbZ0   = maxZ - carL * 0.13f;      // just in front of toolbox
                float wbZ1   = wbZ0 + carL * 0.38f;      // same length as first bench
                float wbH    = carH * 0.40f;
                float topT   = carH * 0.030f;
                float legW   = carW * 0.016f;
                float shelfY = minY + wbH * 0.38f;
                float shelfT = carH * 0.020f;

                Vec3 wbWood  = new Vec3(0.52f, 0.36f, 0.16f);
                Vec3 wbMetal = new Vec3(0.20f, 0.20f, 0.22f);

                AddBox(new Vec3(wbLoX, minY + wbH, wbZ0),
                       new Vec3(wbHiX, minY + wbH + topT, wbZ1),
                       wbWood, 0.08f, 0.70f, 0.55f, 48f, 0.06f);

                AddBox(new Vec3(wbLoX, shelfY, wbZ0),
                       new Vec3(wbHiX, shelfY + shelfT, wbZ1),
                       wbWood, 0.06f, 0.60f, 0.30f, 16f);

                AddBox(new Vec3(wbLoX,        minY, wbZ0),        new Vec3(wbLoX + legW, minY + wbH, wbZ0 + legW), wbMetal, 0.06f, 0.5f, 0.8f, 64f);
                AddBox(new Vec3(wbLoX,        minY, wbZ1 - legW), new Vec3(wbLoX + legW, minY + wbH, wbZ1),        wbMetal, 0.06f, 0.5f, 0.8f, 64f);
                AddBox(new Vec3(wbHiX - legW, minY, wbZ0),        new Vec3(wbHiX,        minY + wbH, wbZ0 + legW), wbMetal, 0.06f, 0.5f, 0.8f, 64f);
                AddBox(new Vec3(wbHiX - legW, minY, wbZ1 - legW), new Vec3(wbHiX,        minY + wbH, wbZ1),        wbMetal, 0.06f, 0.5f, 0.8f, 64f);
            }
            // ── Ceiling fluorescent tubes ─────────────────────────────────────
            {
                Vec3  tubeCol  = new Vec3(0.55f, 0.80f, 1.0f);  // cold neon blue
                Vec3  houseCol = new Vec3(0.22f, 0.22f, 0.24f);  // dark metal housing
                float ceilY    = maxY + carH * 0.5f;
                float tubeThk  = carH * 0.012f;
                float tubeW    = carW * 0.025f;
                float tubeLen  = carL * 0.68f;
                float tubeY0   = ceilY - tubeThk;
                float tubeY1   = ceilY - 0.001f;
                float tubeZ0   = center.Z - tubeLen * 0.5f;
                float tubeZ1   = center.Z + tubeLen * 0.5f;
                float houseW   = tubeW + carW * 0.012f;  // slightly wider housing
                float houseY0  = ceilY - tubeThk * 1.8f;

                float lightY = tubeY0 - carH * 0.01f; // point lights just below the tube face
                float[] offsets = { -carW * 0.14f, carW * 0.14f };
                foreach (float ox in offsets)
                {
                    // Housing (dark metal strip)
                    AddBox(new Vec3(center.X + ox - houseW * 0.5f, houseY0, tubeZ0 - carL * 0.01f),
                           new Vec3(center.X + ox + houseW * 0.5f, ceilY,   tubeZ1 + carL * 0.01f),
                           houseCol, 0.06f, 0.5f, 0.6f, 32f, 0.1f);
                    // Glowing tube
                    AddBox(new Vec3(center.X + ox - tubeW * 0.5f, tubeY0, tubeZ0),
                           new Vec3(center.X + ox + tubeW * 0.5f, tubeY1, tubeZ1),
                           tubeCol, 1.0f, 0f, 0f, 1f);
                    // Three point lights, each centred in its third of the tube
                    foreach (float frac in new[] { 1f/6f, 3f/6f, 5f/6f })
                        lights.Add((new Vec3(center.X + ox, lightY, tubeZ0 + tubeLen * frac),
                                    tubeCol, 0.5f, omni, -1f));
                }
            }
            // ── Mirror frame ──────────────────────────────────────────────────
            {
                float mirZ   = minZ - carL * 0.35f;
                float frameW = carH * 0.05f;   // border thickness
                float frameD = carH * 0.025f;  // border depth
                float mL     = minX - carW * 2.3f;
                float mR     = maxX + carW * 0.6f;
                float mB     = minY;
                float mT     = maxY + carH * 0.5f;
                float fZ0    = mirZ + 0.001f;
                float fZ1    = fZ0 + frameD;
                Vec3  fc     = new Vec3(0.04f, 0.04f, 0.04f); // near-black

                AddBox(new Vec3(mL - frameW, mT,          fZ0), new Vec3(mR + frameW, mT + frameW, fZ1), fc, 0.05f, 0.3f, 0.8f, 64f, 0.2f); // top
                AddBox(new Vec3(mL - frameW, mB - frameW, fZ0), new Vec3(mR + frameW, mB,          fZ1), fc, 0.05f, 0.3f, 0.8f, 64f, 0.2f); // bottom
                AddBox(new Vec3(mL - frameW, mB,          fZ0), new Vec3(mL,          mT,          fZ1), fc, 0.05f, 0.3f, 0.8f, 64f, 0.2f); // left
                AddBox(new Vec3(mR,          mB,          fZ0), new Vec3(mR + frameW, mT,          fZ1), fc, 0.05f, 0.3f, 0.8f, 64f, 0.2f); // right
            }
            // ─────────────────────────────────────────────────────────────────

            // BVH over all car triangles + props
            var bvh = BVHNode.Build(new List<ISceneObject>(triangles));

            var objects = new List<ISceneObject> { bvh };

            // Puddle — front-left of car, organic irregular shape
            objects.Add(new Puddle(
                new Vec3(minX - carW * 0.30f, minY + 0.001f, maxZ + carL * 0.04f),
                carW * 0.28f));

            // Dark studio floor
            objects.Add(new Plane(
                new Vec3(0f, minY - 0.001f, 0f), new Vec3(0f, 1f, 0f),
                new Vec3(0.07f, 0.07f, 0.09f),
                ambient: 0.04f, diffuse: 0.4f, specular: 0.04f, shininess: 8f));

            // Mirror wall — foggy/dirty: lower reflectivity, non-reflected light absorbed (dark)
            objects.Add(new Plane(
                new Vec3(center.X, center.Y, minZ - carL * 0.35f), new Vec3(0f, 0f, 1f),
                new Vec3(0.02f, 0.02f, 0.02f),
                ambient: 0f, diffuse: 0f, specular: 0f, shininess: 1f, reflectivity: 0.42f));

            // Headlight C-shaped DRL arcs — 7 small orbs in a 270° arc, gap facing inward
            Vec3  hlOrb   = new Vec3(1.0f, 0.97f, 0.90f);
            float orbR    = carH * 0.013f;   // small orb radius
            float arcR    = hlSphR * 0.85f;  // radius of the C arc

            void AddCarc(Vec3 pos, float startDeg)
            {
                const int N = 16;
                const float sweepDeg = 270f;
                for (int ci = 0; ci < N; ci++)
                {
                    float angleDeg = startDeg + ci * (sweepDeg / (N - 1));
                    float angleRad = angleDeg * MathF.PI / 180f;
                    Vec3 orbPos = new Vec3(
                        pos.X + arcR * MathF.Cos(angleRad),
                        pos.Y + arcR * MathF.Sin(angleRad),
                        pos.Z);
                    objects.Add(new Sphere(orbPos, orbR, hlOrb, ambient: 1.0f, diffuse: 0f, specular: 0f));
                }
            }

            // Left side: gap faces +X (inward) → start at 45°
            AddCarc(hl1InPos,  45f);
            AddCarc(hl1OutPos, 45f);
            // Right side: gap faces -X (inward) → start at 225°
            AddCarc(hl2InPos,  225f);
            AddCarc(hl2OutPos, 225f);

            // Taillight emissive orbs (red, rear face = minZ)
            float tlSphR = carH * 0.038f;
            objects.Add(new Sphere(tlLeft,  tlSphR, tlColor, ambient: 1.0f, diffuse: 0f, specular: 0f, transparency: 1.0f));
            objects.Add(new Sphere(tlRight, tlSphR, tlColor, ambient: 1.0f, diffuse: 0f, specular: 0f, transparency: 1.0f));

            // Ceiling lamp emissive bulb
            //objects.Add(new Sphere(ceilLampPos, carH * 0.04f, ceilLampColor, ambient: 1.0f, diffuse: 0f, specular: 0f, transparency: 1.0f)); // ceiling lamp bulb

            // Rasterize graffiti SVG for right wall
            Vec3[]? grafTex   = null;
            int  grafTexW = 0, grafTexH = 0;
            Vec3 grafTexOrigin = new Vec3(0f, 0f, 0f);
            float grafTexWorldW = 0f, grafTexWorldH = 0f;
            {
                const string grafPath = @"C:\HSLU\Semester 6\RAYTRACING\assets2\graffiti.svg";
                if (System.IO.File.Exists(grafPath))
                {
                    int tw = 695, th = 410;
                    var svgDoc = SvgDocument.Open(grafPath);
                    using var grafBmp = svgDoc.Draw(tw, th);
                    if (grafBmp != null)
                    {
                        var bd = grafBmp.LockBits(new Rectangle(0, 0, tw, th), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        byte[] px = new byte[bd.Stride * th];
                        Marshal.Copy(bd.Scan0, px, 0, px.Length);
                        int stride = bd.Stride;
                        grafBmp.UnlockBits(bd);

                        grafTex = new Vec3[tw * th];
                        for (int j = 0; j < th; j++)
                            for (int i = 0; i < tw; i++)
                            {
                                int idx = j * stride + i * 4;
                                grafTex[j * tw + i] = new Vec3(px[idx+2]/255f, px[idx+1]/255f, px[idx]/255f);
                            }

                        grafTexW      = tw;
                        grafTexH      = th;
                        grafTexWorldH = carH * 1.5f;
                        grafTexWorldW = grafTexWorldH * (tw / (float)th);
                        grafTexOrigin = new Vec3(maxX + carW * 0.6f,
                                                 minY,
                                                 center.Z - grafTexWorldW * 0.5f);
                    }
                }
            }

            // Garage box walls and ceiling (floor + mirror back wall already added above)
            Vec3  wallCol    = new Vec3(0.92f, 0.92f, 0.92f);
            Vec3  stripeCol  = new Vec3(0.70f, 0.70f, 0.72f);
            float stripeSize = carH * 0.12f;
            objects.Add(new Plane(  // right wall
                new Vec3(maxX + carW * 0.6f, center.Y, center.Z), new Vec3(-1f, 0f, 0f),
                wallCol, ambient: 0.06f, diffuse: 0.5f, specular: 0.05f, shininess: 8f,
                colorB: stripeCol, tileSize: stripeSize, groutFraction: 0.06f, verticalStripes: true,
                texture: grafTex, texW: grafTexW, texH: grafTexH,
                texOrigin: grafTexOrigin, texWorldW: grafTexWorldW, texWorldH: grafTexWorldH));
            objects.Add(new Plane(  // left wall
                new Vec3(minX - carW * 2.3f, center.Y, center.Z), new Vec3(1f, 0f, 0f),
                wallCol, ambient: 0.06f, diffuse: 0.5f, specular: 0.05f, shininess: 8f,
                colorB: stripeCol, tileSize: stripeSize, groutFraction: 0.06f, verticalStripes: true));
            objects.Add(new Plane(  // ceiling — plain white, no pattern
                new Vec3(center.X, maxY + carH * 0.5f, center.Z), new Vec3(0f, -1f, 0f),
                wallCol, ambient: 0.06f, diffuse: 0.5f, specular: 0.05f, shininess: 8f));

            using var bmp  = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var  rect      = new Rectangle(0, 0, width, height);
            var  data      = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int    stride = data.Stride;
                byte[] buffer = new byte[stride * height];

                Vec3 Shade(Ray ray)
                {
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

                        return col;
                }

                Parallel.For(0, height, y =>
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        // 2×2 stratified jitter — 4 samples per pixel
                        Vec3 col = new Vec3(0f, 0f, 0f);
                        for (int sy = 0; sy < 2; sy++)
                            for (int sx = 0; sx < 2; sx++)
                            {
                                float px = x + (sx + Random.Shared.NextSingle()) * 0.5f;
                                float py = y + (sy + Random.Shared.NextSingle()) * 0.5f;
                                col = col + Shade(camera.GetRay(px, py, width, height));
                            }
                        col = col * 0.25f;

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
