using System;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct Position3D : IPosition3D, IEquatable<Position3D>
    {
        public float3 Position { get; set; }

        public Position3D(float3 position)
        {
            this.Position = position;
        }

        public bool Equals(Position3D other)
        {
            return math.all(this.Position == other.Position);
        }

        public override int GetHashCode()
        {
            return this.Position.GetHashCode();
        }
    }
}
