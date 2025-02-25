using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class GradientExtension
    {

        public unsafe static GraphicsBuffer ToGraphicsBuffer(this Gradient gradient, int length)
        {
            var graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, length, sizeof(float4));

            var arr = new Color[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = gradient.Evaluate(i / (float)length);
            }
            graphicsBuffer.SetData(arr);
            return graphicsBuffer;
        }

        public static NativeArray<Color> ToNativeArray(this Gradient gradient, int length, Allocator allocator = Allocator.TempJob)
        {
            var gradientArray = new NativeArray<Color>(length, allocator);
            for (int i = 0; i < length; i++)
            {
                gradientArray[i] = gradient.Evaluate(i / (float)length);
            }
            return gradientArray;
        }

    }
}
