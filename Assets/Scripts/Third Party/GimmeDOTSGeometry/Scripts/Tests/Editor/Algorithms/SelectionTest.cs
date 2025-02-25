using NUnit.Framework;
using Unity.Collections;
using static Unity.Collections.NativeSortExtension;

namespace GimmeDOTSGeometry
{
    public class SelectionTest
    {
        private void TestList(NativeList<int> list, int k)
        {
            var elem = list[k];
            for(int i = 0; i < k; i++)
            {
                Assert.IsTrue(list[i] <= elem);
            }

            for(int i = k + 1; k < list.Length; k++)
            {
                Assert.IsTrue(list[i] >= elem);
            }
        }

        [Test]
        public void QSelectList0()
        {
            var list = new NativeList<int>(5, Allocator.TempJob) { 4, 1, 9, 7, 3 };

            NativeSelection.QuickSelect(ref list, new DefaultComparer<int>(), 2);

            Assert.IsTrue(list[2] == 4);
            TestList(list, 2);

            list.Dispose();
        }

        [Test]
        public void QSelectList1()
        {
            var list = new NativeList<int>(8, Allocator.TempJob)
            {
                10, 1, 9, 2, 7, 3, 6, 5
            };

            NativeSelection.QuickSelect(ref list, new DefaultComparer<int>(), 3);

            Assert.IsTrue(list[3] == 5);
            TestList(list, 3);

            list.Dispose();
        }
    }
}
