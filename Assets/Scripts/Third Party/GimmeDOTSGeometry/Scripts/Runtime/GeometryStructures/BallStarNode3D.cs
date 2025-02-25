using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct BallStarNode3D : IBoundingSphere
    {
        public float RadiusSq { get; set; }
        public float3 Center { get; set; }

        public int children;

        public int left;
        public int right;
        public int parent;
    }
}
