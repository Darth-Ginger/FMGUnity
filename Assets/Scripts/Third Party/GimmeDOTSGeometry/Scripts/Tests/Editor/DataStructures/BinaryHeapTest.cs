using NUnit.Framework;
using System.Collections.Generic;

namespace GimmeDOTSGeometry
{
    public class BinaryHeapTest
    {
        [Test]
        public void Empty()
        {
            var binaryHeap = new BinaryHeap<int>(Comparer<int>.Default);

            Assert.IsTrue(binaryHeap.IsEmpty());
            Assert.IsTrue(binaryHeap.Peek() == default(int));

            binaryHeap.Insert(1);
            Assert.IsTrue(!binaryHeap.IsEmpty());
            Assert.IsTrue(binaryHeap.Peek() == 1);

            Assert.IsTrue(binaryHeap.Remove() == 1);
            Assert.IsTrue(binaryHeap.IsEmpty());
        }

        [Test]
        public void Insertion()
        {
            var binaryHeap = new BinaryHeap<int>(Comparer<int>.Default);

            for(int i = 0; i < 100; i++)
            {
                binaryHeap.Insert(i);
            }

            Assert.IsTrue(binaryHeap.Peek() == 99);
            
            for(int i = 0; i < 50; i++)
            {
                binaryHeap.Remove();
            }

            Assert.IsTrue(binaryHeap.Peek() == 49);

            while (!binaryHeap.IsEmpty()) binaryHeap.Remove();

            Assert.IsTrue(binaryHeap.IsEmpty());
        }

    }
}
