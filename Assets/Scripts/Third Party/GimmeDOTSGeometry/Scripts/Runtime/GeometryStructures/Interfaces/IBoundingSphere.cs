using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public interface IBoundingSphere : IBoundingVolume

    {
        public float RadiusSq { get; set; }
        public float3 Center { get; set; }
    }
}
