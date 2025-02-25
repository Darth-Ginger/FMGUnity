using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Native Array implementation of a priority queue (based on a binary heap).
    /// <para> </para>
    /// <para>-Enqueue: O(log(n))</para>
    /// <para>-Dequeue(min/max): O(log(n))/</para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U">A comparison struct used to compare the inserted elements with each other</typeparam>
    public struct NativePriorityQueue<T, U> : IDisposable where T : unmanaged where U : struct, IComparer<T>
    {

        #region Public Variables

        public U comparer;

        [NoAlias]
        public NativeList<T> elements;

        #endregion

        public int Length { get => this.elements.Length; set => this.elements.Length = value; }


        public NativePriorityQueue(U comparer, Allocator allocator) {

            this.comparer = comparer;
            this.elements = new NativeList<T>(1, allocator);
        }

        //Aggressive Inlining - Burst should do this automatically, so this is just in case
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetParentIndex(int childIndex)
        {
            return math.select((childIndex - 1) / 2, -1, childIndex == 0);
        }

        //Aggressive Inlining - Burst should do this automatically, so this is just in case
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetLeftChildIndex(int parentIndex)
        {
            return math.mad(parentIndex, 2, 1);
        }

        //Aggressive Inlining - Burst should do this automatically, so this is just in case
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRightChildIndex(int parentIndex)
        {
            return math.mad(parentIndex, 2, 2);
        }

        private void Swap(int idx0, int idx1)
        {
            T elem1 = this.elements[idx1];
            this.elements[idx1] = this.elements[idx0];
            this.elements[idx0] = elem1;
        }

        private void BubbleUp(int childIndex, int parentIndex)
        {
            while(Hint.Likely(parentIndex >= 0))
            {
                var childElem = this.elements[childIndex];
                var parentElem = this.elements[parentIndex];

                int cmp = this.comparer.Compare(childElem, parentElem);

                if(cmp >= 0)
                {
                    this.elements[parentIndex] = childElem;
                    this.elements[childIndex] = parentElem;

                    childIndex = parentIndex;
                    parentIndex = this.GetParentIndex(parentIndex);

                } else
                {
                    break;
                }
            }
        }

        private void BubbleDown(int parentIndex)
        {
            int leftChildIndex = this.GetLeftChildIndex(parentIndex);
            int rightChildIndex = this.GetRightChildIndex(parentIndex);

            int swapChild = parentIndex;
            int count = this.elements.Length;

            //We only need to check the left side for count, as right is always bigger
            while (Hint.Likely(leftChildIndex < count))
            {

                if (leftChildIndex < count && this.comparer.Compare(this.elements[swapChild], this.elements[leftChildIndex]) < 0)
                {
                    swapChild = leftChildIndex;
                }

                if (rightChildIndex < count && this.comparer.Compare(this.elements[swapChild], this.elements[rightChildIndex]) < 0)
                {
                    swapChild = rightChildIndex;
                }

                if (swapChild != parentIndex)
                {
                    this.Swap(parentIndex, swapChild);

                    parentIndex = swapChild;
                    leftChildIndex = this.GetLeftChildIndex(parentIndex);
                    rightChildIndex = this.GetRightChildIndex(parentIndex);
                }
                else
                {
                    break;
                }
            }
        }

        public void Enqueue(T value)
        {
            this.elements.Add(value);

            int childIndex = this.elements.Length - 1;
            int parentIndex = this.GetParentIndex(childIndex);

            this.BubbleUp(childIndex, parentIndex);
        }

        public T Peek()
        {
            return this.elements.Length > 0 ? this.elements[0] : default(T);
        }

        public T Dequeue()
        {
            if(this.elements.Length == 0) return default(T);

            var root = this.elements[0];

            this.elements[0] = this.elements[this.elements.Length - 1];
            // = Remove back
            this.elements.Length--;

            this.BubbleDown(0);

            return root;
        }

        public bool IsEmpty() => this.elements.Length == 0;

        public bool IsCreated => this.elements.IsCreated;

        public void Dispose()
        {
            if (this.elements.IsCreated)
            {
                this.elements.Dispose();
            }
        }
    }
}
