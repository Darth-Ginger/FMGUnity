using System;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct PositionWithIndex3D : IPosition3D, IIdentifiable, IEquatable<PositionWithIndex3D>
    {
        public float3 Position { get; set; }

        public int ID { get; set; }

        public PositionWithIndex3D(float3 position, int id)
        {
            this.Position = position;
            this.ID = id;
        }

        public bool Equals(PositionWithIndex3D other)
        {
            return this.ID == other.ID;
        }

        public override int GetHashCode()
        {
            return this.ID;
        }
    }
}
