using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class NativeListExtension
    {

        public static void Swap<T>(this NativeList<T> arr, int a, int b) where T : unmanaged
        {
            var elemA = arr[a];
            var elemB = arr[b];
            AlgoUtil.Swap(ref elemA, ref elemB);
            arr[a] = elemA;
            arr[b] = elemB;
        }

        public static unsafe void Insert<T>(this NativeList<T> list, int idx, T element) where T : unmanaged
        {
            if(idx < 0 || idx > list.Length)
            {
                Debug.LogError($"[Gimme DOTS Geometry]: Tried to insert an element at index {idx}. List has length of {list.Length}");
                return;
            }
            else if(idx == list.Length)
            {
                list.Add(element);
                return;
            }


            list.Add(element);

            var ptr = (T*)list.GetUnsafePtr();
            T* ptrSource = ptr + idx;
            T* ptrDest = ptr + idx + 1;

            UnsafeUtility.MemMove(ptrDest, ptrSource, (list.Length - 1 - idx) * UnsafeUtility.SizeOf<T>());

            list[idx] = element;
            
        }

        public static void DisposeIfCreated<T>(this NativeList<T> list) where T : unmanaged
        {
            if (list.IsCreated) list.Dispose();
        }



        
    }
}
