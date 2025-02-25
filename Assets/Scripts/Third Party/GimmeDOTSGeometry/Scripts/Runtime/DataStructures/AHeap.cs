using System.Collections.Generic;

namespace GimmeDOTSGeometry
{
    public abstract class AHeap<T>
    {
        protected IComparer<T> comparer;

        public AHeap(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }


        public abstract void Insert(T value);
        public abstract T Peek();
        public abstract T Remove();

        public abstract bool IsEmpty();
        public abstract int Count();

    }
}