using UnityEngine;

namespace GimmeDOTSGeometry
{
    public interface IBoundingRect : IBoundingArea
    {

        public Rect Bounds { get; set; }

    }
}
