using UnityEngine;

namespace GimmeDOTSGeometry
{
    public struct RStarNode3D : IBoundingBox
    {
        public Bounds Bounds { get; set; }

        public int children;

        public int left;
        public int right;
        public int parent;
    }
}
