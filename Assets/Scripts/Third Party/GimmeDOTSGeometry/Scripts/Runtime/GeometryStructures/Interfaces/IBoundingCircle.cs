using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public interface IBoundingCircle : IBoundingArea

    {
        public float RadiusSq { get; set; }
        public float2 Center { get; set; }
    }
}
