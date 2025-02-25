using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace GimmeDOTSGeometry
{
    public struct AttributeMap
    {
        public NativeParallelHashMap<int, UnsafeList<int>> map;
    }
}
