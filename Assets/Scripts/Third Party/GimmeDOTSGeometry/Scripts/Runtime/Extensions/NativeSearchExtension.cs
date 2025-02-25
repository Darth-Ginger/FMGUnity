using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GimmeDOTSGeometry
{
    public static class NativeSearchExtension
    {

        private unsafe static int BinarySearch<T>(T* ptr, T value, int min, int max) where T : unmanaged, IComparable<T> {

            int left = min;
            int right = max;
            int compResult = -1;
            int index = 0;

            while (left < right)
            {
                index = (int)((left + right) / 2);
                compResult = ptr[index].CompareTo(value);
                if (compResult > 0)
                {
                    right = index;
                }
                else 
                {
                    left = index + 1;
                }
            }

            if (compResult == 0) return right - 1;
            return ~right;

        }

        /// <summary>
        /// Same as NativeSortExtension BinarySearch, but search can be applied within a range of an array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nativeArray"></param>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static unsafe int BinarySearch<T>(this NativeArray<T> nativeArray, T value, int min, int max) where T : unmanaged, IComparable<T>
        {
            return BinarySearch<T>((T*)nativeArray.GetUnsafePtr(), value, min, max);
        }


        /// <summary>
        /// Same as NativeSortExtension BinarySearch, but search can be applied within a range of a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nativeList"></param>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static unsafe int BinarySearch<T>(this NativeList<T> nativeList, T value, int min, int max) where T : unmanaged, IComparable<T>
        {
            return BinarySearch<T>((T*)nativeList.GetUnsafePtr(), value, min, max);
        }

    }
}
