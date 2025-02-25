
namespace GimmeDOTSGeometry
{
    public abstract class APriorityQueue<T>
    {
        public abstract void Enqueue(T element);

        public abstract T Dequeue();

        public abstract T Peek();

        public abstract bool IsEmpty();

        public abstract int Count();
    }
}
