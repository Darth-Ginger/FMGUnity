using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GimmeDOTSGeometry
{
    public static class BoundsExtension
    {
        /// <summary>
        /// Returns the bounds that enclose both these bounds and the other ones
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static Bounds CombineWith(this Bounds bounds, Bounds other)
        {
            float3 min = math.min(bounds.min, other.min);
            float3 max = math.max(bounds.max, other.max);
            
            return new Bounds((min + max) * 0.5f, max - min);
        }

        public static float Area(this Bounds bounds)
        {
            var size = bounds.size;

            return 2 * (size.x * size.y + size.x * size.z + size.y * size.z);
        }



        public static float Volume(this Bounds bounds)
        {
            var size = bounds.size;
            return size.x * size.y * size.z;
        }

        public static bool Overlaps(this Bounds bounds, Bounds other)
        {
            float3 min = bounds.min;
            float3 max = bounds.max;

            float3 otherMin = other.min;
            float3 otherMax = other.max;

            return otherMax.x > min.x && otherMin.x < max.x
                && otherMax.y > min.y && otherMin.y < max.y
                && otherMax.z > min.z && otherMin.z < max.z;
        }

        public static FixedList128Bytes<float3> GetCornerPoints(this Bounds bounds)
        {
            FixedList128Bytes<float3> cornerPoints = new FixedList128Bytes<float3>();

            var min = bounds.min;
            var max = bounds.max;

            cornerPoints.Add(min);
            cornerPoints.Add(new float3(min.x, min.y, max.z));
            cornerPoints.Add(new float3(min.x, max.y, min.z));
            cornerPoints.Add(new float3(min.x, max.y, max.z));
            cornerPoints.Add(new float3(max.x, min.y, min.z));
            cornerPoints.Add(new float3(max.x, min.y, max.z));
            cornerPoints.Add(new float3(max.x, max.y, min.z));
            cornerPoints.Add(max);

            return cornerPoints;
        }


        public static float OverlapVolume(this Bounds bounds0, Bounds bounds1)
        {
            if (!bounds0.Overlaps(bounds1)) return 0.0f;

            if (bounds0.Contains(bounds1)) return bounds1.Volume();
            if (bounds1.Contains(bounds0)) return bounds0.Volume();

            float3 bounds0Min = bounds0.min;
            float3 bounds0Max = bounds0.max;

            float3 bounds1Min = bounds1.min;
            float3 bounds1Max = bounds1.max;

            float3 min = math.max(bounds0Min, bounds1Min);
            float3 max = math.min(bounds0Max, bounds1Max);

            return (max.x - min.x) * (max.y - min.y) * (max.z - min.z);
        }

        /// <summary>
        /// Returns true if the other bounds are completely contained in the these bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool Contains(this Bounds bounds, Bounds other)
        {
            return math.all((float3)bounds.min <= (float3)other.min) 
                && math.all((float3)bounds.max >= (float3)other.max);
        }

        

        /// <summary>
        /// Divides the boundary into eight smaller boundaries (like in an octree). The results
        /// are ordered in a Z-Curve 
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static Bounds[] Subdivide(this Bounds bounds)
        {
            var subBounds = new Bounds[8];

            var min = bounds.min;
            var halfScale = bounds.extents;

            var bottomLeftDownCenter = min + halfScale * 0.5f;
            var bottomRightDownCenter = bottomLeftDownCenter + new Vector3(halfScale.x, 0.0f, 0.0f);
            var topLeftDownCenter = bottomLeftDownCenter + new Vector3(0.0f, 0.0f, halfScale.z);
            var topRightDownCenter = bottomLeftDownCenter + new Vector3(halfScale.x, 0.0f, halfScale.z);

            var bottomLeftUpCenter = bottomLeftDownCenter + new Vector3(0.0f, halfScale.y, 0.0f);
            var bottomRightUpCenter = bottomLeftDownCenter + new Vector3(halfScale.x, halfScale.y, 0.0f);
            var topLeftUpCenter = bottomLeftDownCenter + new Vector3(0.0f, halfScale.y, halfScale.z);
            var topRightUpCenter = bottomLeftDownCenter + halfScale;

            subBounds[0] = new Bounds(bottomLeftDownCenter, halfScale);
            subBounds[1] = new Bounds(bottomRightDownCenter, halfScale);
            subBounds[2] = new Bounds(topLeftDownCenter, halfScale);
            subBounds[3] = new Bounds(topRightDownCenter, halfScale);

            subBounds[4] = new Bounds(bottomLeftUpCenter, halfScale);
            subBounds[5] = new Bounds(bottomRightUpCenter, halfScale);
            subBounds[6] = new Bounds(topLeftUpCenter, halfScale);
            subBounds[7] = new Bounds(topRightUpCenter, halfScale);

            return subBounds;
        }


        /// <summary>
        /// Divides the boundary into eight smaller boundaries (like in an octree). The results
        /// are ordered in a Z-Curve 
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static void Subdivide(this Bounds bounds, ref NativeList<Bounds> subdividedBounds)
        {
            var min = bounds.min;
            var halfScale = bounds.extents;

            var bottomLeftDownCenter = min + halfScale * 0.5f;
            var bottomRightDownCenter = bottomLeftDownCenter + new Vector3(halfScale.x, 0.0f, 0.0f);
            var topLeftDownCenter = bottomLeftDownCenter + new Vector3(0.0f, 0.0f, halfScale.z);
            var topRightDownCenter = bottomLeftDownCenter + new Vector3(halfScale.x, 0.0f, halfScale.z);

            var bottomLeftUpCenter = bottomLeftDownCenter + new Vector3(0.0f, halfScale.y, 0.0f);
            var bottomRightUpCenter = bottomLeftDownCenter + new Vector3(halfScale.x, halfScale.y, 0.0f);
            var topLeftUpCenter = bottomLeftDownCenter + new Vector3(0.0f, halfScale.y, halfScale.z);
            var topRightUpCenter = bottomLeftDownCenter + halfScale;

            subdividedBounds.Add(new Bounds(bottomLeftDownCenter, halfScale));
            subdividedBounds.Add(new Bounds(bottomRightDownCenter, halfScale));
            subdividedBounds.Add(new Bounds(topLeftDownCenter, halfScale));
            subdividedBounds.Add(new Bounds(topRightDownCenter, halfScale));
            subdividedBounds.Add(new Bounds(bottomLeftUpCenter, halfScale));
            subdividedBounds.Add(new Bounds(bottomRightUpCenter, halfScale));
            subdividedBounds.Add(new Bounds(topLeftUpCenter, halfScale));
            subdividedBounds.Add(new Bounds(topRightUpCenter, halfScale));
        }
    }
}