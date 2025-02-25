using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class ShapeLocation 
    {
        public static bool IsInsideCircle(float2 center, float radiusSq, float2 point)
        {
            float2 dir = point - center;
            return math.dot(dir, dir) < radiusSq;
        }

        public static bool IsInsideSphere(float3 center, float radiusSq, float3 point)
        {
            float3 dir = point - center;
            return math.dot(dir, dir) < radiusSq;
        }



    }
}
