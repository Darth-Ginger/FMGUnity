using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class Vector2Extension
    {

        public static bool ApproximatelyEquals(this Vector2 vec, Vector2 other, float epsilon = 10e-5f)
        {
            return math.abs(math.distancesq(vec, other)) < epsilon;
        }

        public static Vector3 AsVector3(this Vector2 vec, CardinalPlane cardinalPlane)
        {
            var indices = cardinalPlane.GetAxisIndices();
            var newVec = new Vector3();
            newVec[indices.x] = vec.x;
            newVec[indices.y] = vec.y;

            return newVec;
        }
    }
}
