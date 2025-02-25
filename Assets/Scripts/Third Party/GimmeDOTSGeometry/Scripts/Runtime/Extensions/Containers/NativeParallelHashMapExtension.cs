using System;
using Unity.Collections;

namespace GimmeDOTSGeometry
{
    public static class NativeParallelHashMapExtension
    {
        public static void DisposeIfCreated<T, U>(this NativeParallelHashMap<T, U> hashMap) where T : unmanaged, IEquatable<T> where U : unmanaged
        {
            if (hashMap.IsCreated) hashMap.Dispose();
        }
    }
}
