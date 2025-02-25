using System;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public struct Position2D : IPosition2D, IEquatable<Position2D>
    {
        public float2 Position { get; set; }

        public Position2D(float2 position)
        {
            this.Position = position;
        }

        public bool Equals(Position2D other)
        {
            return math.all(this.Position == other.Position);
        }


        public override int GetHashCode()
        {
            return this.Position.GetHashCode();
        }
    
    }
}
