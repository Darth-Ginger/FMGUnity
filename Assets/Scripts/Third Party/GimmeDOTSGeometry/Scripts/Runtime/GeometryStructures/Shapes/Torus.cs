using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public struct Torus
    {
        public float minorRadius;
        public float majorRadius;

        public Matrix4x4 localToWorld;

        public Torus(float minorRadius, float majorRadius)
        {
            this.minorRadius = minorRadius;
            this.majorRadius = majorRadius;
            this.localToWorld = Matrix4x4.identity;
        }

        public Torus(float minorRadius, float majorRadius, Matrix4x4 localToWorld)
        {
            this.minorRadius = minorRadius;
            this.majorRadius = majorRadius;
            this.localToWorld = localToWorld;
        }

        public float Area()
        {
            return 4 * this.minorRadius * this.majorRadius * math.PI * math.PI;
        }

        public float Volume()
        {
            return 2 * this.majorRadius * this.minorRadius * this.minorRadius * math.PI * math.PI;
        }
    }
}
