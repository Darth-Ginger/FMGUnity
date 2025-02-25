using UnityEngine;

namespace GimmeDOTSGeometry
{
    public struct RStarNode2D : IBoundingRect
    {
        public Rect Bounds { get; set; }

        public int children;

        public int left;
        public int right;
        public int parent;
    }
}
