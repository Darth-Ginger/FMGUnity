using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    public static class NativeSelection
    {
        //Note: If users request even more performance from KD-Tree Construction, consider implementing IntroSelect
        
        private static int RandomPartition<T, U>(ref NativeList<T> list, U comparer, ref Random rnd, int left, int right) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            int pivot = rnd.NextInt(left, right);
            int pivotLocation = left;
            var pivotElement = list[pivot];

            list.Swap(pivot, right - 1);

            for(int i = left; i < right - 1; i++)
            {
                if (comparer.Compare(list[i], pivotElement) < 0)
                {
                    list.Swap(i, pivotLocation);
                    pivotLocation++;
                }
            }
            list.Swap(right - 1, pivotLocation);

            return pivotLocation;
        }

        /// <summary>
        /// Finds the kth smallest element in a list in O(n) - smallest being defined by a comparer (i.e. you can also use it to find the kth largest element)
        /// The list is changed during this process, such that the kth smallest element is at position k of the list, and all smaller elements are to the left,
        /// and all larger element are to the right of k.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="list"></param>
        /// <param name="comparer"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public unsafe static void QuickSelect<T, U>(ref NativeList<T> list, U comparer, int k) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            QuickSelect(ref list, comparer, k, 0, list.Length);
        }

        /// <summary>
        /// Finds the kth smallest element in a subset of a list in O(n) in the range [left, right) - smallest being defined by a comparer 
        /// (i.e. you can also use it to find the kth largest element)
        /// The list is changed during this process, such that the kth smallest element is at position k + left of the list, and all smaller elements are to the left,
        /// and all larger element are to the right of k in the interval [left, right).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="list"></param>
        /// <param name="comparer"></param>
        /// <param name="k"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        public unsafe static void QuickSelect<T, U>(ref NativeList<T> list, U comparer, int k, int left, int right) where T : unmanaged where U : unmanaged, IComparer<T>
        {
            Random rnd = new Random();
            rnd.InitState();

            if (left == right) return;

            k += left;
            int pivot = RandomPartition(ref list, comparer, ref rnd, left, right);
            while (pivot != k)
            {
                if (pivot < k)
                {
                    left = pivot + 1;
                }
                else
                {
                    right = pivot;
                }

                if (left == right) return;

                pivot = RandomPartition(ref list, comparer, ref rnd, left, right);
            }
        }

    }
}
