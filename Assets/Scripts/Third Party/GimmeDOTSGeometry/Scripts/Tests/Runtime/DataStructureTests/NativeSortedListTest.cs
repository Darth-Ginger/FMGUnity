using NUnit.Framework;
using System;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using static Unity.Collections.NativeSortExtension;

namespace GimmeDOTSGeometry
{
    public class NativeSortedListTest 
    {


        private void Setup()
        {
            var cam = RuntimeTestUtility.CreateCamera();
            cam.transform.position = Vector3.up * 7.0f;

            RuntimeTestUtility.CreateDirectionalLight();
        }

        [UnityTest]
        public IEnumerator Search()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);

            sortedList.Insert(1);
            sortedList.Insert(3);
            sortedList.Insert(2);

            int idx = sortedList.Search(2);

            Assert.IsTrue(idx >= 0);
            Assert.IsTrue(idx == 1);

            sortedList.Insert(5);

            idx = sortedList.Search(4);

            Assert.IsTrue(idx == -4); //Insert at index 3

            sortedList.Insert(4);
            idx = sortedList.Search(4);

            Assert.IsTrue(idx == 3);

            for(int i = 6; i < 32; i++)
            {
                sortedList.Insert(i);
            }
            idx = sortedList.Search(15);

            Assert.IsTrue(idx == 14);

            //Beginning
            idx = sortedList.Search(1);
            Assert.IsTrue(idx == 0);

            //End
            idx = sortedList.Search(31);
            Assert.IsTrue(idx == 30);

            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 4000.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
        }

        [UnityTest]
        public IEnumerator RandomAccess()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);

            Assert.Throws<IndexOutOfRangeException>(() => { int tmp = sortedList[0]; });

            sortedList.Insert(1);
            sortedList.Insert(2);
            sortedList.Insert(3);

            Assert.Throws<IndexOutOfRangeException>(() => { int tmp = sortedList[3]; });

            int elem = sortedList[0];
            Assert.IsTrue(elem == 1);
            elem = sortedList[1];
            Assert.IsTrue(elem == 2);
            elem = sortedList[2];
            Assert.IsTrue(elem == 3);


            for(int i = 4; i < 16; i++)
            {
                sortedList.Insert(i);
            }

            elem = sortedList[8];
            Assert.IsTrue(elem == 9);

            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 600.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
        }



        [UnityTest]
        public IEnumerator Enumerator()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);

            for(int i = 0; i < 16; i++)
            {
                sortedList.Insert(i);
            }

            int counter = 0;
            foreach(var elem in sortedList)
            {
                Assert.IsTrue(elem == counter);
                counter++;
            }
            Assert.IsTrue(counter == sortedList.Length);
            
            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 300.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
        }



        [UnityTest]
        public IEnumerator Remove()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);

            sortedList.Insert(1);
            sortedList.Remove(1);

            Assert.IsTrue(sortedList.Length == 0);

            //Making sure the internal states are correct
            sortedList.Insert(1);

            Assert.IsTrue(sortedList.Length == 1);

            sortedList.Remove(1);

            Assert.IsTrue(sortedList.Length == 0);

            sortedList.Insert(1);
            sortedList.Insert(2);
            sortedList.Insert(3);
            
            sortedList.Remove(2);

            Assert.IsTrue(sortedList.Length == 2);
            Assert.IsTrue(sortedList[0] == 1);
            Assert.IsTrue(sortedList[1] == 3);
            
            sortedList.Insert(2);

            //Scaling it up
            sortedList.Insert(4);
            sortedList.Insert(5);
            sortedList.Insert(6);
            sortedList.Insert(7);
            
            sortedList.Remove(4);
            sortedList.Remove(6);
            sortedList.Remove(7);
            
            Assert.IsFalse(sortedList.Remove(7));
            Assert.IsFalse(sortedList.Remove(-1));
            
            Assert.IsTrue(sortedList.Length == 4);
            Assert.IsTrue(sortedList[0] == 1);
            Assert.IsTrue(sortedList[1] == 2);
            Assert.IsTrue(sortedList[2] == 3);
            Assert.IsTrue(sortedList[3] == 5);
            
            //Removing everything again - in ~reverse
            sortedList.Remove(5);
            sortedList.Remove(3);
            sortedList.Remove(1);
            sortedList.Remove(2);

            Assert.IsFalse(sortedList.Remove(2));

            //One more to make sure
            for (int i = 0; i < 16; i++)
            {
                sortedList.Insert(i);
            }

            
            for(int i = 0; i < 8; i++)
            {
                sortedList.Remove(i * 2);
            }

            Assert.IsTrue(sortedList.Length == 8);
            Assert.IsTrue(sortedList.Search(3) >= 0);
            Assert.IsTrue(sortedList[0] == 1);
            

            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 1200.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
        }

        [BurstCompile]
        private struct ParallelSumTestJob : IJobParallelFor
        {
            [NoAlias, NativeDisableParallelForRestriction]
            public NativeArray<int> sum;

            [NoAlias, ReadOnly]
            public NativeSortedList<int, DefaultComparer<int>> sortedList;

            [NativeSetThreadIndex]
            public int threadIdx;

            public void Execute(int index)
            {
                this.sum[this.threadIdx] += this.sortedList[index];
            }
        }

        [UnityTest]
        public IEnumerator ParallelJob()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);
            var sum = new NativeArray<int>(JobsUtility.MaxJobThreadCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < 16; i++)
            {
                sortedList.Insert(i + 1);
            }

            var parallelSumJob = new ParallelSumTestJob()
            {
                sortedList = sortedList,
                sum = sum,
            };

            var jobHandle = parallelSumJob.Schedule(sortedList.Length, 4);
            jobHandle.Complete();

            int parallelSum = 0;
            for(int i = 0; i < sum.Length; i++)
            {
                parallelSum += sum[i];
            }

            //Gauss => (n * (n + 1)) / 2
            Assert.IsTrue(parallelSum == (((16) * (16 + 1)) / 2));

            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 1200.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
            sum.Dispose();
        }

        [UnityTest]
        public IEnumerator SearchRange()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);
            var resultList = new NativeList<int>(Allocator.Persistent);

            for(int i = 0; i < 16; i++)
            {
                sortedList.Insert(i);
            }

            var searchRangeJob = sortedList.SearchRange(ref resultList, 5, 10);
            searchRangeJob.Complete();

            Assert.IsTrue(resultList.Length == 5);
            Assert.IsTrue(resultList.Contains(5));
            Assert.IsTrue(resultList.Contains(9));
            Assert.IsFalse(resultList.Contains(10));

            searchRangeJob = sortedList.SearchRange(ref resultList, -2, 5);
            searchRangeJob.Complete();

            Assert.IsTrue(resultList.Length == 5);
            Assert.IsTrue(resultList.Contains(0));
            Assert.IsTrue(resultList.Contains(4));
            Assert.IsFalse(resultList.Contains(5));

            searchRangeJob = sortedList.SearchRange(ref resultList, 11, 25);
            searchRangeJob.Complete();

            Assert.IsTrue(resultList.Length == 5);
            Assert.IsTrue(resultList.Contains(11));
            Assert.IsTrue(resultList.Contains(15));

            searchRangeJob = sortedList.SearchRange(ref resultList, -1, 25);
            searchRangeJob.Complete();

            Assert.IsTrue(resultList.Length == sortedList.Length);
            Assert.IsTrue(resultList.Contains(0));
            Assert.IsTrue(resultList.Contains(15));

            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 1200.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
            resultList.Dispose();
        }


        [UnityTest]
        public IEnumerator InsertBeforeHeader()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);

            Assert.IsTrue(sortedList.Length == 0);

            //Large element inserted first
            sortedList.Insert(15000);

            Assert.IsTrue(sortedList.Length == 1);

            sortedList.Insert(15);
            sortedList.Insert(25);
            sortedList.Insert(5);

            Assert.IsTrue(sortedList.Length == 4);

            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 300.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
        }

        [UnityTest]
        public IEnumerator RandomInsertion()
        {
            this.Setup();

            var sortedList = new NativeSortedList<int, DefaultComparer<int>>(default, Allocator.Persistent);


            Assert.IsTrue(sortedList.Length == 0);

            sortedList.Insert(1);

            Assert.IsTrue(sortedList.Length == 1);

            for(int i = 0; i < 64; i++)
            {
                sortedList.Insert(UnityEngine.Random.Range(2, 64));
            }

            Assert.IsTrue(sortedList.Length == 65);

            var sortedListDrawerGO = new GameObject("Sorted List Drawer");
            var drawer = sortedListDrawerGO.AddComponent<SkipListDrawer>();
            drawer.list = sortedList;
            drawer.ySize = 8000.0f;

            while (!drawer.finished)
            {
                yield return null;
            }

            sortedList.Dispose();
        }


    }
}
