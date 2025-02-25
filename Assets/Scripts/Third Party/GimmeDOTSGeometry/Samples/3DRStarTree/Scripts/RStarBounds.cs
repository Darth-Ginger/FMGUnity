using System;
using UnityEngine;

namespace GimmeDOTSGeometry.Samples
{
    public struct RStarBounds : IBoundingBox, IIdentifiable, IEquatable<RStarBounds>
    {
        public Bounds Bounds { get; set; }

        public int ID { get; set; }

        public bool Equals(RStarBounds other)
        {
            return this.ID == other.ID;
        }

        public override int GetHashCode()
        {
            return this.ID;
        }
    }
}
