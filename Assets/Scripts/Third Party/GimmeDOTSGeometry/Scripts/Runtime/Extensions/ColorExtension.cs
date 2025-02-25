using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class ColorExtension
    {


        public static float4 ToFloat4(this Color color)
        {
            return new float4(color.r, color.g, color.b, color.a);
        }

    }
}
