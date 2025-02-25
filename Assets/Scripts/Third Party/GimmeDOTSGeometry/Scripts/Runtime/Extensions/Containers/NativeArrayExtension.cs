using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class NativeArrayExtensions 
    {

        public static void Swap<T>(this NativeArray<T> arr, int a, int b) where T : unmanaged
        {
            var elemA = arr[a];
            var elemB = arr[b];
            AlgoUtil.Swap(ref elemA, ref elemB);
            arr[a] = elemA;
            arr[b] = elemB;
        }

        public static void DisposeIfCreated<T>(this NativeArray<T> arr) where T : unmanaged
        {
            if (arr.IsCreated) arr.Dispose();
        }

        //=============================================================================
        //These extensions are supposed to help people not so versed in Jobs converting
        //between the data types with ease
        //
        //Can be safely removed, if you do not like them
        //=============================================================================

        public static NativeArray<float3> ToFloatArray(this NativeArray<Vector3> arr)
        {
            return arr.Reinterpret<float3>();
        }

        public static NativeArray<float2> ToFloatArray(this NativeArray<Vector2> arr)
        {
            return arr.Reinterpret<float2>();
        }

        public static NativeArray<Vector3> ToVectorArray(this NativeArray<float3> arr)
        {
            return arr.Reinterpret<Vector3>();
        }

        public static NativeArray<Vector2> ToVectorArray(this NativeArray<float2> arr)
        {
            return arr.Reinterpret<Vector2>();
        }
    }
}
