using System;
using Unity.Collections;

namespace GimmeDOTSGeometry
{
    public static class NativeParallelHashSetExtension
    {
        public static void DisposeIfCreated<T>(this NativeParallelHashSet<T> hashSet) where T : unmanaged, IEquatable<T>
        {
            if (hashSet.IsCreated) hashSet.Dispose();
        }
    }
}
