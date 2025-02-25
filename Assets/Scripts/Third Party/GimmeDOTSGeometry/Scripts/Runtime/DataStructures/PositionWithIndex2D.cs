using System;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct PositionWithIndex2D : IPosition2D, IIdentifiable, IEquatable<PositionWithIndex2D>
    {
        public float2 Position { get; set; }

        public int ID { get; set; }

        public PositionWithIndex2D(float2 position, int id)
        {
            this.Position = position;
            this.ID = id;
        }

        public bool Equals(PositionWithIndex2D other)
        {
            return this.ID == other.ID;
        }

        public override int GetHashCode()
        {
            return this.ID;
        }
    }
}
