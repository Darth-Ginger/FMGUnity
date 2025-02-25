using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace GimmeDOTSGeometry
{
    // Raycasts within the BVHs in Gimme DOTS Geometry are technically just intersection calculations
    // and do not "cast" rays. They are named Raycasts, as they provide most of the same functionality,
    // which makes it clear for the user what the methods are made for.
    // But as to not have name-clashes with RaycastHit3D, I named the return structure
    // IntersectionHit3D instead
    public struct IntersectionHit3D<T> where T : unmanaged, IBoundingVolume
    {

        /// <summary>
        /// Positions where the bounding volume was hit (at most 2)
        /// </summary>
        public FixedList32Bytes<float3> hitPoints;

        /// <summary>
        /// Reference to the bounding volume - the "collider" - that was hit
        /// </summary>
        public T boundingVolume;


        public struct RayComparer : IComparer<IntersectionHit3D<T>>
        {
            public float3 rayOrigin;
            public float epsilon;

            public int Compare(IntersectionHit3D<T> x, IntersectionHit3D<T> y)
            {
                float closestX = float.PositiveInfinity;
                float closestY = float.PositiveInfinity;

                for (int i = 0; i < x.hitPoints.Length; i++)
                {
                    float distSq = math.distancesq(this.rayOrigin, x.hitPoints[i]);
                    if (distSq < closestX) closestX = distSq;
                }

                for (int i = 0; i < y.hitPoints.Length; i++)
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
