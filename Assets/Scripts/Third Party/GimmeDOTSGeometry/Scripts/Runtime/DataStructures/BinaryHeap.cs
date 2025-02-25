using System.Collections.Generic;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// Binary heap, using an array implementation
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BinaryHeap<T> : AHeap<T>
    {
        #region Private Variables

        private List<T> elements;

        #endregion

        private int GetParentIndex(int childIndex)
        {
            if (childIndex == 0) return -1;
            return (childIndex - 1) / 2;
        }

        private int GetLeftChildIndex(int parentIndex)
        {
            return parentIndex * 2 + 1;
        }

        private int GetRightChildIndex(int parentIndex)
        {
            return parentIndex * 2 + 2;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="comparer">Compares each element for heap ordering. If you use default-sorting, the structure is a max-heap. If you reverse
        /// the comparison function it becomes a min-heap</param>
        public BinaryHeap(IComparer<T> comparer) : base(comparer)
        {
            this.elements = new List<T>();
        }

        public override int Count()
        {
            return this.elements.Count;
        }

        private void Swap(int idx0, int idx1)
        {
            var element0 = this.elements[idx0];
            var element1 = this.elements[idx1];

            this.elements[idx0] = element1;
            this.elements[idx1] = element0;
        }

        private void BubbleUp(int childIndex, int parentIndex)
        {
            if (parentIndex >= 0)
            {
                var childElement = this.elements[childIndex];
                var parentElement = this.elements[parentIndex];

                int compValue = this.comparer.Compare(childElement, parentElement);

                if (compValue >= 0)
                {
                    this.elements[parentIndex] = childElement;
                    this.elements[childIndex] = parentElement;
                    this.BubbleUp(parentIndex, this.GetParentIndex(parentIndex));
                }
            }
        }

        private void BubbleDown(int parentIndex)
        {
            int leftChildIndex = this.GetLeftChildIndex(parentIndex);
            int rightChildIndex = this.GetRightChildIndex(parentIndex);

            int swapChild = parentIndex;
            if (leftChildIndex < this.Count() && this.comparer.Compare(this.elements[swapChild], this.elements[leftChildIndex]) < 0)
            {
                swapChild = leftChildIndex;
            }

            if (rightChildIndex < this.Count() && this.comparer.Compare(this.elements[swapChild], this.elements[rightChildIndex]) < 0)
            {
                swapChild = rightChildIndex;
            }

            if (swapChild != parentIndex)
            {
                this.Swap(parentIndex, swapChild);
                this.BubbleDown(swapChild);
            }
        }


        public override void Insert(T value)
        {

            this.elements.Add(value);

            int childIndex = this.Count() - 1;
            int parentIndex = this.GetParentIndex(childIndex);

            this.BubbleUp(childIndex, parentIndex);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns the value of the root-node of the tree without removing it</returns>
        public override T Peek()
        {
            if (this.elements.Count > 0)
            {
                return this.elements[0];
            }
            else
            {
                return default(T);
            }
        }


        /// <summary>
        /// Removes the root-node of the tree and restructures the tree accordingly.
        /// </summary>
        /// <returns>The value stored at the root-node (same as Peek())</returns>
        public override T Remove()
        {
            if (this.elements.Count > 0)
            {

                var root = this.elements[0];

                if (this.elements.Count > 1)
                {
                    this.elements[0] = this.elements[this.elements.Count - 1];
                    this.elements.RemoveAt(this.elements.Count - 1);

                    this.BubbleDown(0);

                }
                else
                {
                    this.elements.Clear();
                }

                return root;

            }
            else
            {
                return default(T);
            }
        }

        public override bool IsEmpty()
        {
            return this.elements.Count == 0;
        }
    }
}