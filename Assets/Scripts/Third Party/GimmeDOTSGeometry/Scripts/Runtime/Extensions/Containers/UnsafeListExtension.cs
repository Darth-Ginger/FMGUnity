using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class UnsafeListExtension
    {

        public static unsafe T[] ToArray<T>(ref this UnsafeList<T> list) where T : unmanaged
        {
            T[] arr = new T[list.Length];

            for(int i = 0; i < list.Length; i++)
            {
                arr[i] = list[i];
            }

            return arr;
        }

        public static unsafe void Insert<T>(ref this UnsafeList<T> list, int idx, T element) where T : unmanaged
        {
            if (idx < 0 || idx > list.Length)
            {
                Debug.LogError($"[Gimme DOTS Geometry]: Tried to insert an element at index {idx}. List has length of {list.Length}");
                return;
            }
            else if (idx == list.Length)
            {
                list.Add(element);
                return;
            }


            list.Add(element);

            var ptr = list.Ptr;
            T* ptrSource = ptr + idx;
            T* ptrDest = ptr + idx + 1;

            UnsafeUtility.MemMove(ptrDest, ptrSource, (list.Length - 1 - idx) * UnsafeUtility.SizeOf<T>());

            list[idx] = element;

        }

        //I didn't know this was possible, but I tried and it worked... ref this... I love C#!
        //(the reason we need this is so that the length gets copied back to the caller as well)
        public static unsafe void CopyFrom<T>(ref this UnsafeList<T> unsafeList, NativeList<T> nativeList) where T : unmanaged
        {
            unsafeList.Resize(nativeList.Length);
            UnsafeUtility.MemCpy(unsafeList.Ptr, nativeList.GetUnsafePtr(), nativeList.Length * UnsafeUtility.SizeOf<T>());
        }

        public static unsafe void CopyFrom<T>(ref this UnsafeList<T> unsafeList, NativeArray<T> nativeArray) where T : unmanaged
        {
            unsafeList.Resize(nativeArray.Length);
            UnsafeUtility.MemCpy(unsafeList.Ptr, nativeArray.GetUnsafePtr(), nativeArray.Length * UnsafeUtility.SizeOf<T>());
        }
    }
}
