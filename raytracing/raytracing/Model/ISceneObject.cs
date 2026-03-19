namespace raytracing.Model
{
    public interface ISceneObject
    {
        bool TryIntersect(Ray ray, out Hit hit);
    }
}
