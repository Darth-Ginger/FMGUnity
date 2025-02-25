using NUnit.Framework;
using Unity.Collections;
using static Unity.Collections.NativeSortExtension;

namespace GimmeDOTSGeometry
{
    public class RBTreeTest
    {
        [Test]
        public void Insertion()
        {

            var rbTree = new NativeRBTree<int, DefaultComparer<int>>(new DefaultComparer<int>(), Allocator.TempJob);

            rbTree.Insert(5);

            rbTree.Insert(2);
            rbTree.Insert(8);

            rbTree.Insert(1);
            rbTree.Insert(9);

            rbTree.Dispose();
        }

        [Test]
        public void RemoveRoot()
        {
            var rbTree = new NativeRBTree<int, DefaultComparer<int>>(new DefaultComparer<int>(), Allocator.TempJob);

            rbTree.Insert(3);

            rbTree.Remove(3);

            Assert.IsTrue(rbTree.IsEmpty());

            rbTree.Insert(5);

            Assert.IsTrue(!rbTree.IsEmpty());

            rbTree.Dispose();
        }

        [Test]
        public void Remove()
        {
            var rbTree = new NativeRBTree<int, DefaultComparer<int>>(new DefaultComparer<int>(), Allocator.TempJob);

            for(int i = 0; i < 8; i++)
            {
                rbTree.Insert(i);
            }

           // Assert.IsTrue(rbTree.Elements.Length == 30);

            for(int i = 0; i < 8; i++)
            {
                rbTree.Remove(i);
            }

            Assert.IsTrue(rbTree.IsEmpty());

            rbTree.Dispose();
        }
    }
}
