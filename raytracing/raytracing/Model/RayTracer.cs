namespace raytracing.Model
{
    public static class RayTracer
    {
        public static bool TraceClosest(Ray ray, List<ISceneObject> objects, out Hit closestHit)
        {
            closestHit = default;
            bool found = false;
            float bestT = float.PositiveInfinity;

            foreach (ISceneObject obj in objects)
            {
                if (!obj.TryIntersect(ray, out Hit hit))
                    continue;

                if (hit.T < bestT)
                {
                    bestT = hit.T;
                    closestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        public static bool IsInShadow(Vec3 point, Vec3 lightPos, List<ISceneObject> objects)
        {
            Vec3  toLight       = lightPos - point;
            float lightDistance = toLight.Length();
            Vec3  lightDir      = toLight.Normalized();

            const float epsilon = 0.0001f;
            Vec3 origin = point + lightDir * epsilon;
            float traveled = epsilon;

            // Walk through transparent surfaces; stop at the first opaque blocker
            for (int bounce = 0; bounce < 8; bounce++)
            {
                Ray shadowRay = new Ray(origin, lightDir);
                if (!TraceClosest(shadowRay, objects, out Hit h))
                    return false;

                float hitDist = traveled + h.T;
                if (hitDist >= lightDistance)
                    return false;

                if (h.Transparency < 0.5f)
                    return true;

                // Transparent hit — step past it and keep going
                origin   = h.Position + lightDir * epsilon;
                traveled = hitDist + epsilon;
            }

            return false;
        }
    }
}
