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
            Vec3 toLight = lightPos - point;
            float lightDistance = toLight.Length();
            Vec3 lightDir = toLight.Normalized();

            // Small offset so the ray does not immediately hit the same surface again
            const float epsilon = 0.0001f;
            Ray shadowRay = new Ray(point + lightDir * epsilon, lightDir);

            if (!TraceClosest(shadowRay, objects, out Hit shadowHit))
                return false;

            return shadowHit.T < lightDistance;
        }
    }
}
