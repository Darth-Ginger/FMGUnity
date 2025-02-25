using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace GimmeDOTSGeometry
{
    public static class NativeParallelMultiHashMapJobs

    {

        /// <summary>
        /// When a native parallel multi hash map is resized, the order is sometimes mingled up for some items. I suspect, this is
        /// because of hash collisions and actually depends on GetHashCode() from your T-Type (UnsafeParallelHashMap -> ReallocateHashMap, Line 197,
        /// as of writing this comment, if you want to take a look at it yourself). The larger your hash map, the more collisions, so my theory... 
        /// But I could be wrong!
        /// 
        /// This change of order affects some / most algorithms, so this method remedies this by creating a new hash map and copying each
        /// element one by one, so the order remains unchanged! (this works regardless of the cause)
        /// </summary>
        [BurstCompile]
        public struct CopyParallelMultiHashMapJob<T, U> : IJob
            where T : unmanaged, IEquatable<T>, IComparable<T>
            where U : unmanaged
        {
            public NativeParallelMultiHashMap<T, U> source;
            public NativeParallelMultiHashMap<T, U> destination;

            public void Execute()
            {
                var keyArrayResult = this.source.GetUniqueKeyArray(Allocator.Temp);
                var keyArray = keyArrayResult.Item1;
                int uniqueItems = keyArrayResult.Item2;

                for (int i = 0; i < uniqueItems; i++)
                {
                    T key = keyArray[i];

                    if(this.source.TryGetFirstValue(key, out U item, out var it))
                    {
                        this.destination.Add(key, item);
                        while(this.source.TryGetNextValue(out item, ref it))
                        {
                            this.destination.Add(key, item);
                        }
                    }
                }
            }
        }

        //Clearing a hash map can take a long time -> with this job, you can at least do it in parallel to other things
        [BurstCompile]
        public struct ClearParallelMultiHashMapJob<T, U> : IJob
            where T : unmanaged, IEquatable<T>, IComparable<T>
            where U : unmanaged
        {
            [NoAlias]
            public NativeParallelMultiHashMap<T, U> hashMap;

            public void Execute()
            {
                this.hashMap.Clear();
            }
        }
    }
}
