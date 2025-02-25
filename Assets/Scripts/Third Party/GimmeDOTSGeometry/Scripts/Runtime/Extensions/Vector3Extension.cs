using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class Vector3Extension
    {
        public static bool ApproximatelyEquals(this Vector3 vec, Vector3 other, float epsilon = 10e-5f)
        {
            return math.abs(math.distancesq(vec, other)) < epsilon;
        }

    }
}
