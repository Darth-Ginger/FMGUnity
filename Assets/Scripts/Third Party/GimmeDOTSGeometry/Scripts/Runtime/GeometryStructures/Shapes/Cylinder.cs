using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public struct Cylinder 
    {
        public float radius;
        public float height;

        public Matrix4x4 localToWorld;

        public Cylinder(float radius, float height)
        {
            this.radius = radius;
            this.height = height;
            this.localToWorld = Matrix4x4.identity;
        }

        public Cylinder(float radius, float height, Matrix4x4 localToWorld)
        {
            this.radius = radius;
            this.height = height;
            this.localToWorld = localToWorld;
        }

        public float Area()
        {
            return 2.0f * this.radius * this.radius * math.PI + 2.0f * this.radius * math.PI * this.height;  
        }

        public float Volume()
        {
            return this.radius * this.radius * math.PI * this.height;
        }

    }
}
