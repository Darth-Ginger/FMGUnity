using Unity.Collections;

namespace GimmeDOTSGeometry
{
    public struct OctreeNode
    {
        public FixedList64Bytes<int> children;
    }
}
