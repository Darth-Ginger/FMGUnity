using System;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public struct RStarRect : IBoundingRect, IIdentifiable, IEquatable<RStarRect>
    {
        public Rect Bounds { get; set; }
        public int ID { get; set; }

        public bool Equals(RStarRect other)
        {
            return this.ID == other.ID;
        }

        public override int GetHashCode()
        {
            return this.ID;
        }
    }
}
