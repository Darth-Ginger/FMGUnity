using System;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    [Serializable]
    public struct Line3D 
    {

        public float3 point;
        public float3 direction;


        public static readonly Line3D Invalid = new Line3D() { direction = float.NaN, point = float.NaN };

        public Line3D(float3 point, float3 direction)
        {
            this.point = point;
            this.direction = direction;
        }



    }
}
