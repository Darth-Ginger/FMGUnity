using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class Float2Extension 
    {
        public static readonly float2 Left = new float2(-1.0f, 0.0f);
        public static readonly float2 Right = new float2(1.0f, 0.0f);
        public static readonly float2 Up = new float2(0.0f, 1.0f);
        public static readonly float2 Down = new float2(0.0f, -1.0f);

        public static bool ApproximatelyEquals(this float2 vec, float2 other, float epsilon = 10e-5f)
        {
            return math.all(math.abs(vec - other) < epsilon);
        }

        /// <summary>
        /// Returns the perpendicular vector to the left (rotated counter-clockwise) 
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static float2 Perpendicular(this float2 vec)
        {
            return new float2(-vec.y, vec.x);
        }

        public static float3 AsFloat3(this float2 vec, CardinalPlane cardinalPlane)
        {
            var indices = cardinalPlane.GetAxisIndices();
            var newVec = new float3();
            newVec[indices.x] = vec.x;
            newVec[indices.y] = vec.y;
            
            return newVec;
        }
    }
}
