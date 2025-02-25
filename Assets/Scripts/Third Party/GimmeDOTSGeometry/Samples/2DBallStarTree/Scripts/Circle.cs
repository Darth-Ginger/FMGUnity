using System;
using Unity.Mathematics;

namespace GimmeDOTSGeometry.Samples
{
    //The test scene just contains circles i.e. this is the bare minimum struct
    //However, for your use cases, you can and should adapt the struct to hold
    //the data you want!
    public struct Circle : IBoundingCircle, IIdentifiable, IEquatable<Circle>
    {
        public float RadiusSq { get; set; }
        public float2 Center { get; set; }

        public int ID { get; set; }

        public bool Equals(Circle other)
        {
            return this.ID == other.ID;
        }

        public override int GetHashCode()
        {
            return this.ID;
        }
    }
}
