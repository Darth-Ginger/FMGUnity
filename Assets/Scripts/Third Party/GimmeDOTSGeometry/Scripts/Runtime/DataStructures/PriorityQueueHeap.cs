using System.Collections.Generic;


namespace GimmeDOTSGeometry
{

    public class PriorityQueueHeap<T> : APriorityQueue<T>
    {

        private BinaryHeap<T> heap;


        public PriorityQueueHeap(IComparer<T> comparer)
        {
            this.heap = new BinaryHeap<T>(comparer);
        }

        public override void Enqueue(T element)
        {
            this.heap.Insert(element);
        }

        public override T Dequeue()
        {
            return this.heap.Remove();
        }

        public override T Peek()
        {
            return this.heap.Peek();
        }

        public override bool IsEmpty()
        {
            return this.heap.IsEmpty();
        }

        public override int Count()
        {
            return this.heap.Count();
        }
    }
}