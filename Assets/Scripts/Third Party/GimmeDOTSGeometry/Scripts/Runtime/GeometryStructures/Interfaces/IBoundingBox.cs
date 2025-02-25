using UnityEngine;

namespace GimmeDOTSGeometry
{
    public interface IBoundingBox : IBoundingVolume
    {
        public Bounds Bounds { get; set; }
    }
}
