using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct BallStarNode2D : IBoundingCircle
    {
        public float RadiusSq { get; set; }
        public float2 Center { get; set; }

        public int children;

        public int left;
        public int right;
        public int parent;
    }
}
