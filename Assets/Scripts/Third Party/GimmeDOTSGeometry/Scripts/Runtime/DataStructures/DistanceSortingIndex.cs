using System.Collections.Generic;

namespace GimmeDOTSGeometry
{
    /// <summary>
    /// A common data structure, used to identify the references again after sorting by distance (the element identified by the index)
    /// </summary>
    public struct DistanceSortingIndex 
    {
        public float distance;
        public int index;
    }

    public struct DistanceSortingIndexComparer : IComparer<DistanceSortingIndex>
    {
        public int Compare(DistanceSortingIndex x, DistanceSortingIndex y)
        {
            return x.distance.CompareTo(y.distance);
        }

        private static DistanceSortingIndexComparer defaultComparer;

        static DistanceSortingIndexComparer() {
            defaultComparer = new DistanceSortingIndexComparer();
        }

        public static DistanceSortingIndexComparer Default => defaultComparer;
    }
}
