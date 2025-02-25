using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public struct Cone 
    {

        public float radius;
        public float height;

        public Matrix4x4 localToWorld;

        public Cone(float radius, float height)
        {
            this.radius = radius;
            this.height = height;
            this.localToWorld = Matrix4x4.identity;
        }

        public Cone(float radius, float height, Matrix4x4 localToWorld)
        {
            this.radius = radius;
            this.height = height;
            this.localToWorld = localToWorld;
        }
        public float Area()
        {
            return this.radius * this.radius * math.PI + this.radius * math.PI * this.height;
        }

        public float Volume()
        {
            return this.radius * this.radius * math.PI * this.height / 3.0f;
        }
    }
}
