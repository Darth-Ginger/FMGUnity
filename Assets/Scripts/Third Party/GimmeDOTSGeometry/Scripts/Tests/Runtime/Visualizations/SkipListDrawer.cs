using UnityEngine;
using static Unity.Collections.NativeSortExtension;

namespace GimmeDOTSGeometry
{
    public unsafe class SkipListDrawer : MonoBehaviour
    {

        public bool finished = false;

        public float ySize = 500.0f;

        public NativeSortedList<int, DefaultComparer<int>> list;
    }
}
