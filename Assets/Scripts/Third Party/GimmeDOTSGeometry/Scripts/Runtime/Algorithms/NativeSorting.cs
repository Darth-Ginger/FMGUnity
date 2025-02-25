using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GimmeDOTSGeometry
{
    public static class NativeSorting
    {

        private unsafe static void BubbleSort<T, U>(T* data, int length, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            bool swapped = true;
            while (swapped)
            {
                swapped = false;
                T elem = data[0];

                for (int i = 1; i < length; i++)
                {
                    var nextElem = data[i];

                    if (comparer.Compare(elem, data[i]) < 0)
                    {
                        data[i] = elem;
                        data[i - 1] = nextElem;
                        swapped = true;
                    }
                    else
                    {
                        elem = nextElem;
                    }
                }

            }
        }



        internal unsafe static void InsertionSort<T, U>(T* data, int start, int end, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            for (int i = start + 1; i < end; i++)
            {
                int j = i - 1;
                T nextElem = data[i];
                if(comparer.Compare(nextElem, data[j]) < 0)
                {
                    do
                    {
                        data[j + 1] = data[j];
                        j--;

                    } while (j >= start && comparer.Compare(nextElem, data[j]) < 0);

                    data[j + 1] = nextElem;
                }
            }
        }

        private unsafe static void SelectionSort<T, U>(T* data, int length, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            for (int i = 0; i < length - 1; i++)
            {
                T elem = data[i];
                int idx = i;
                for (int j = i + 1; j < length; j++)
                {
                    if (comparer.Compare(data[j], elem) < 0)
                    {
                        elem = data[j];
                        idx = j;
                    }
                }
                data[idx] = data[i];
                data[i] = elem;
            }

        }

        public unsafe static void BubbleSort<T, U>(ref NativeArray<T> arr, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            BubbleSort((T*)arr.GetUnsafePtr(), arr.Length, comparer);
        }

        /// <summary>
        /// Sorts the given array with insertion sort. This is useful and fast for small arrays,
        /// and can be done in-place (no additional memory is used or has to be allocated)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        public unsafe static void InsertionSort<T, U>(ref NativeArray<T> arr, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            InsertionSort((T*)arr.GetUnsafePtr(), 0, arr.Length, comparer);
        }

        /// <summary>
        /// Sorts the given array with insertion sort. This is useful and fast for small arrays,
        /// and can be done in-place (no additional memory is used or has to be allocated)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        public unsafe static void InsertionSort<T, U>(ref NativeList<T> list, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            InsertionSort((T*)list.GetUnsafePtr(), 0, list.Length, comparer);
        }

        /// <summary>
        /// Sorts the given array with selection sort. This is useful and fast for small arrays,
        /// and can be done in-place (no additional memory is used or has to be allocated)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        public unsafe static void SelectionSort<T, U>(ref NativeArray<T> arr, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            SelectionSort((T*)arr.GetUnsafePtr(), arr.Length, comparer);
        }

        /// <summary>
        /// Sorts the given array with selection sort. This is useful and fast for small arrays,
        /// and can be done in-place (no additional memory is used or has to be allocated)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        public unsafe static void SelectionSort<T, U>(ref NativeList<T> list, U comparer) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            SelectionSort((T*)list.GetUnsafePtr(), list.Length, comparer);
        }

    }
}
