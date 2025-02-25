using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    // Raycasts within the BVHs in Gimme DOTS Geometry are technically just intersection calculations
    // and do not "cast" rays. They are named Raycasts, as they provide most of the same functionality,
    // which makes it clear for the user what the methods are made for.
    // But as to not have name-clashes with RaycastHit2D, I named the return structure
    // IntersectionHit2D instead
    public struct IntersectionHit2D<T> where T : unmanaged, IBoundingArea
    {
        /// <summary>
        /// Positions where the bounding area was intersected (at most 2)
        /// </summary>
        public FixedList32Bytes<float2> hitPoints;

        /// <summary>
        /// Reference to the bounding area - the "collider" - that was hit
        /// </summary>
        public T boundingArea;


        public struct RayComparer : IComparer<IntersectionHit2D<T>>
        {
            public float2 rayOrigin;
            public float epsilon;

            public int Compare(IntersectionHit2D<T> x, IntersectionHit2D<T> y)
            {
                float closestX = float.PositiveInfinity;
                float closestY = float.PositiveInfinity;

                for(int i = 0; i < x.hitPoints.Length; i++)
                {
                    float distSq = math.distancesq(this.rayOrigin, x.hitPoints[i]);
                    if (distSq < closestX) closestX = distSq;
                }

                for(int i = 0; i < y.hitPoints.Length; i++)
                {
                    float distSq = math.distancesq(this.rayOrigin, y.hitPoints[i]);
                    if (distSq < closestY) closestY = distSq;
                }

                float diff = closestX - closestY;
                return (diff > this.epsilon ? 1 : 0) - (diff < -this.epsilon ? 1 : 0);
            }
        }

    }
}
